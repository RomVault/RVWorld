using System;
using System.Collections.Generic;
using RomVaultCore.FixFile.FixAZipCore;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using SortMethods;

namespace RomVaultCore.FixFile;

/// <summary>
/// Fix routine for CHD container nodes.
/// </summary>
/// <remarks>
/// This is used when a DAT rule enables <c>DiscArchiveAsCHD</c> and the set is configured to be stored as a single
/// <c>.chd</c> rather than individual disc source files.
/// 
/// Fix strategy:
/// - Prefer an existing disc source (CHD/CUE/GDI/ISO) that can satisfy the expected CHD.
/// - If sources are track-only audio sets, optionally synthesize a CUE and build a CHD.
/// - Do not create CHDs for partial sets (silently skip) to avoid producing misleading results.
/// </remarks>
public static class FixAChd
{
    /// <summary>
    /// Attempts to fix a missing CHD container by creating it from available disc sources.
    /// </summary>
    /// <param name="chdDir">The expected CHD container node.</param>
    /// <param name="thisSelected">Whether this node was selected for fixing.</param>
    /// <param name="fileProcessQueue">Queue of files involved in fix operations for cleanup/state updates.</param>
    /// <param name="totalFixed">Counter of successful fixes.</param>
    /// <param name="errorMessage">Error description on failure.</param>
    /// <returns>A <see cref="ReturnCode"/> indicating success or failure.</returns>
    public static ReturnCode FixChd(RvFile chdDir, bool thisSelected, List<RvFile> fileProcessQueue, ref int totalFixed, out string errorMessage)
    {
        errorMessage = "";
        if (!thisSelected)
            return ReturnCode.Good;

        if (chdDir == null || chdDir.FileType != FileType.CHD)
        {
            errorMessage = "Invalid CHD container node passed to FixChd.";
            return ReturnCode.LogicError;
        }

        if (chdDir.GotStatus == GotStatus.Got)
            return ReturnCode.Good;

        RvFile source = FindBestDiscSource(chdDir);
        if (source == null)
        {
            if (TryBuildChdFromAudioTracks(chdDir, fileProcessQueue, ref totalFixed, out errorMessage))
                return ReturnCode.Good;

            if (string.Equals(errorMessage, "__SKIP_NO_SOURCES__", StringComparison.Ordinal) ||
                string.Equals(errorMessage, "__SKIP_PARTIAL_SET__", StringComparison.Ordinal))
            {
                errorMessage = "";
                return ReturnCode.Good; // silently skip: only build CHD if we have all required disc files
            }

            return ReturnCode.FileSystemError;
        }

        try
        {
            string fixDir = chdDir.Parent?.FullName ?? "";
            string fixFile = chdDir.Name ?? "";
            string sourceDir = source.Parent?.FullName ?? "";
            string sourceZip = source.FileType == FileType.FileZip || source.FileType == FileType.FileSevenZip ? source.Parent?.Name ?? "" : "";
            string sourceFile = source.Name ?? "";
            Report.ReportProgress(new bgwShowFix(fixDir, "", fixFile, null, "<--CHD", sourceDir, sourceZip, sourceFile));
        }
        catch
        {
        }

        if (!FixFileUtils.TryCreateChdFromDiscSource(source, chdDir, out ReturnCode rc, out string chdError, out List<RvFile> usedFiles))
        {
            RomVaultCore.DatRule ruleInner = null;
            try
            {
                string ruleKey = (chdDir.Parent?.DatTreeFullName ?? "") + "\\";
                ruleInner = RomVaultCore.ReadDat.DatReader.FindDatRule(ruleKey);
            }
            catch
            {
            }

            if (ruleInner == null)
            {
                errorMessage = "CHD creation was not triggered because no matching DAT rule was found for this path.";
                return ReturnCode.FileSystemError;
            }

            if (!ruleInner.DiscArchiveAsCHD)
            {
                errorMessage = "CHD creation was not triggered because DiscArchiveAsCHD is disabled for this DAT rule.";
                return ReturnCode.FileSystemError;
            }

            string ext = System.IO.Path.GetExtension(source?.Name ?? "").ToLowerInvariant();
            errorMessage = $"CHD creation was not triggered (unsupported disc source extension '{ext}').";
            return ReturnCode.FileSystemError;
        }

        if (rc != ReturnCode.Good)
        {
            if (string.Equals(chdError, "__SKIP_NO_SOURCES__", StringComparison.Ordinal) ||
                string.Equals(chdError, "__SKIP_PARTIAL_SET__", StringComparison.Ordinal))
            {
                errorMessage = "";
                return ReturnCode.Good;
            }

            errorMessage = chdError;
            try
            {
                Report.ReportProgress(new bgwShowFixError(chdError));
            }
            catch
            {
            }
            return rc;
        }

        RomVaultCore.DatRule rule = null;
        try
        {
            string ruleKey = (chdDir.Parent?.DatTreeFullName ?? "") + "\\";
            rule = RomVaultCore.ReadDat.DatReader.FindDatRule(ruleKey);
        }
        catch
        {
        }

        if (rule != null && Settings.rvSettings.ChdKeepCueGdi)
        {
            List<RvFile> filteredUsed = new List<RvFile>();
            foreach (RvFile used in usedFiles)
            {
                string ext = System.IO.Path.GetExtension(used.Name).ToLowerInvariant();
                if (ext == ".cue" || ext == ".gdi")
                {
                    // Keep it: don't add to filteredUsed (which will be marked for delete)
                    // But we should ensure it's in the same folder as the CHD.
                    // If it's already there, great. If not, maybe we should move it.
                    // For now, let's just NOT delete it.
                    continue;
                }
                filteredUsed.Add(used);
            }
            FixFileUtils.CheckFilesUsedForFix(filteredUsed, fileProcessQueue, true);
        }
        else
        {
            FixFileUtils.CheckFilesUsedForFix(usedFiles, fileProcessQueue, true);
        }
        totalFixed++;
        return ReturnCode.Good;
    }

    /// <summary>
    /// Attempts to build an audio CHD from individual audio track sources when no disc descriptor source exists.
    /// </summary>
    /// <remarks>
    /// This is intentionally conservative:
    /// - If no audio tracks are expected, it skips.
    /// - If only some expected tracks are available, it skips (partial sets must not generate CHDs).
    ///
    /// Sources may come from:
    /// - normal fix sources for the expected members
    /// - direct files under the set directory
    /// - ToSort primary/cache by name or by hash
    /// </remarks>
    private static bool TryBuildChdFromAudioTracks(RvFile chdDir, List<RvFile> fileProcessQueue, ref int totalFixed, out string errorMessage)
    {
        errorMessage = "";
        if (chdDir == null || chdDir.FileType != FileType.CHD)
        {
            errorMessage = "CHD container is not valid.";
            return false;
        }

        bool isDreamcast = false;
        try
        {
            string ruleKey = (chdDir.Parent?.DatTreeFullName ?? "") + "\\";
            RomVaultCore.DatRule rule = RomVaultCore.ReadDat.DatReader.FindDatRule(ruleKey);
            if (rule != null && rule.DiscArchiveAsCHD && rule.ChdCompressionType == ChdCompressionType.Dreamcast)
            {
                isDreamcast = true;
                if (!Settings.rvSettings.ChdKeepCueGdi)
                {
                    errorMessage = "__SKIP_PARTIAL_SET__";
                    return false;
                }
            }
        }
        catch
        {
        }

        List<RvFile> toSortSearchRoots = GetToSortSearchRoots(chdDir);
        Dictionary<string, RvFile> toSortIndex = BuildFileNameIndex(toSortSearchRoots, 6);
        HashIndex toSortHashIndex = BuildHashIndex(toSortSearchRoots, 6);
        Dictionary<string, RvFile> toSortTrackIndex = null;
        if (isDreamcast)
        {
            string setName = chdDir.Name ?? "";
            if (setName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                setName = System.IO.Path.GetFileNameWithoutExtension(setName);

            List<RvFile> scopedDirs = new List<RvFile>();
            if (!string.IsNullOrWhiteSpace(setName))
            {
                for (int i = 0; i < toSortSearchRoots.Count; i++)
                {
                    RvFile d = toSortSearchRoots[i];
                    if (d == null || !d.IsDirectory)
                        continue;
                    if (string.Equals(d.Name, setName, StringComparison.OrdinalIgnoreCase))
                        scopedDirs.Add(d);
                }
            }

            toSortTrackIndex = BuildTrackNoIndex(scopedDirs.Count > 0 ? scopedDirs : toSortSearchRoots, 4);
        }
        List<(int trackNo, RvFile expected, RvFile source)> tracks = new List<(int, RvFile, RvFile)>();
        List<string> missingSources = new List<string>();
        int expectedAudioCount = 0;
        for (int i = 0; i < chdDir.ChildCount; i++)
        {
            RvFile expected = chdDir.Child(i);
            if (expected == null || !expected.IsFile)
                continue;
            if (!IsAudioTrackFileName(expected.Name))
                continue;

            expectedAudioCount++;
            List<RvFile> sources = FindSourceFile.GetFixFileList(expected);
            RvFile best = null;
            for (int j = 0; j < sources.Count; j++)
            {
                RvFile s = sources[j];
                if (s == null || !s.IsFile)
                    continue;
                if (s.GotStatus != GotStatus.Got)
                    continue;
                if (s.FileType != FileType.File &&
                    s.FileType != FileType.FileZip &&
                    s.FileType != FileType.FileSevenZip &&
                    s.FileType != FileType.FileCHD)
                    continue;
                if (!IsAudioTrackFileName(s.Name))
                    continue;
                best = s;
                break;
            }

            if (best == null)
            {
                RvFile parent = chdDir.Parent;
                if (parent != null && parent.IsDirectory)
                {
                    if (parent.ChildNameSearch(FileType.File, expected.Name, out int idx) == 0)
                    {
                        RvFile candidate = parent.Child(idx);
                        if (candidate != null && candidate.IsFile && candidate.FileType == FileType.File && candidate.GotStatus == GotStatus.Got)
                            best = candidate;
                    }
                }
            }

            if (best == null)
                toSortIndex.TryGetValue(expected.Name, out best);

            if (best == null)
                best = FindByHash(toSortHashIndex, expected);

            if (best == null && isDreamcast && toSortTrackIndex != null)
            {
                int trackNo = TryParseTrackNo(expected.Name);
                if (trackNo > 0)
                {
                    string ext = System.IO.Path.GetExtension(expected.Name).ToLowerInvariant();
                    string key = trackNo.ToString("D2") + ext;
                    toSortTrackIndex.TryGetValue(key, out best);
                }
            }

            if (best == null)
            {
                missingSources.Add(expected.Name);
                continue;
            }

            tracks.Add((TryParseTrackNo(expected.Name), expected, best));
        }

        if (expectedAudioCount == 0)
        {
            errorMessage = "__SKIP_NO_SOURCES__";
            return false;
        }

        if (tracks.Count == 0)
        {
            errorMessage = "__SKIP_NO_SOURCES__";
            return false;
        }

        if (!isDreamcast && tracks.Count != expectedAudioCount)
        {
            errorMessage = "__SKIP_PARTIAL_SET__";
            return false;
        }

        tracks.Sort((a, b) =>
        {
            int c = a.trackNo.CompareTo(b.trackNo);
            if (c != 0)
                return c;
            return Sorters.DirectoryNameCompareCase(a.expected.Name, b.expected.Name);
        });

        if (!FixFileUtils.TryCreateChdFromAudioTracks(tracks, chdDir, out ReturnCode rc, out string chdError))
        {
            errorMessage = "CHD creation was not triggered.";
            return false;
        }

        if (rc != ReturnCode.Good)
        {
            errorMessage = chdError;
            return false;
        }

        List<RvFile> used = new List<RvFile>();
        for (int i = 0; i < tracks.Count; i++)
            used.Add(tracks[i].source);
        FixFileUtils.CheckFilesUsedForFix(used, fileProcessQueue, true);
        if (!isDreamcast)
            totalFixed++;
        return true;
    }

    /// <summary>
    /// Finds the best available disc source that can be used to satisfy an expected CHD container.
    /// </summary>
    /// <remarks>
    /// Source selection prefers ToSort matches first (so ToSort can be drained), then file-group sources,
    /// then DAT-derived sources. Priority ordering is controlled by <see cref="SourcePriority(string)"/>.
    /// </remarks>
    private static RvFile FindBestDiscSource(RvFile chdDir)
    {
        RvFile best = null;
        int bestPriority = 0;
        bool requireGdi = false;
        try
        {
            string ruleKey = (chdDir.Parent?.DatTreeFullName ?? "") + "\\";
            RomVaultCore.DatRule rule = RomVaultCore.ReadDat.DatReader.FindDatRule(ruleKey);
            requireGdi = rule != null && rule.DiscArchiveAsCHD && rule.ChdCompressionType == ChdCompressionType.Dreamcast;
        }
        catch
        {
        }

        List<RvFile> toSortSearchRoots = GetToSortSearchRoots(chdDir);
        RvFile toSortDisc = FindDiscSourceInDirs(chdDir, toSortSearchRoots);
        if (toSortDisc != null)
        {
            if (requireGdi)
            {
                string ext = System.IO.Path.GetExtension(toSortDisc.Name ?? "").ToLowerInvariant();
                if (ext != ".gdi" && ext != ".chd")
                    toSortDisc = null;
            }
        }
        if (toSortDisc != null)
            return toSortDisc;

        // Migration assist: when sidecar folder layout is enabled, existing CHDs may still sit one level up
        // (e.g. "<category>/<set>.chd" while expected is "<category>/<set>/<set>.chd").
        RvFile siblingDisc = FindSiblingDiscSource(chdDir);
        if (siblingDisc != null)
        {
            if (requireGdi)
            {
                string ext = System.IO.Path.GetExtension(siblingDisc.Name ?? "").ToLowerInvariant();
                if (ext != ".gdi" && ext != ".chd")
                    siblingDisc = null;
            }
        }
        if (siblingDisc != null)
            return siblingDisc;

        if (chdDir?.FileGroup?.Files != null)
        {
            for (int i = 0; i < chdDir.FileGroup.Files.Count; i++)
            {
                RvFile src = chdDir.FileGroup.Files[i];
                if (src == null || !IsUsableDiscSourceNode(src))
                    continue;
                if (src.GotStatus != GotStatus.Got)
                    continue;
                if (requireGdi)
                {
                    string ext = System.IO.Path.GetExtension(src.Name ?? "").ToLowerInvariant();
                    if (ext != ".gdi" && ext != ".chd")
                        continue;
                }
                int pr = SourcePriority(src.Name);
                if (pr > bestPriority)
                {
                    bestPriority = pr;
                    best = src;
                }
            }
            if (best != null)
                return best;
        }

        for (int i = 0; i < chdDir.ChildCount; i++)
        {
            RvFile expected = chdDir.Child(i);
            if (expected == null || !expected.IsFile)
                continue;

            int pr = SourcePriority(expected.Name);
            if (pr == 0)
                continue;

            List<RvFile> sources = FindSourceFile.GetFixFileList(expected);
            for (int j = 0; j < sources.Count; j++)
            {
                RvFile src = sources[j];
                if (src == null || !IsUsableDiscSourceNode(src))
                    continue;
                if (requireGdi)
                {
                    string ext = System.IO.Path.GetExtension(src.Name ?? "").ToLowerInvariant();
                    if (ext != ".gdi" && ext != ".chd")
                        continue;
                }
                if (!string.Equals(System.IO.Path.GetExtension(src.Name), System.IO.Path.GetExtension(expected.Name), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (pr > bestPriority)
                {
                    bestPriority = pr;
                    best = src;
                }
            }

            if (bestPriority < pr)
            {
                RvFile toSortMatch = FindFileByNameInDirs(toSortSearchRoots, expected.Name);
                if (toSortMatch != null)
                {
                    bestPriority = pr;
                    best = toSortMatch;
                }
            }
        }

        return best;
    }

    private static bool IsUsableDiscSourceNode(RvFile src)
    {
        if (src == null)
            return false;
        if (src.FileType == FileType.CHD)
            return true;
        return src.IsFile && (src.FileType == FileType.File || src.FileType == FileType.FileZip || src.FileType == FileType.FileSevenZip);
    }

    private static RvFile FindSiblingDiscSource(RvFile chdDir)
    {
        if (chdDir == null || chdDir.FileType != FileType.CHD)
            return null;
        RvFile setDir = chdDir.Parent;
        if (setDir == null || !setDir.IsDirectory)
            return null;
        RvFile categoryDir = setDir.Parent;
        if (categoryDir == null || !categoryDir.IsDirectory)
            return null;

        string want = chdDir.Name ?? "";
        if (string.IsNullOrWhiteSpace(want))
            return null;

        if (categoryDir.ChildNameSearch(FileType.CHD, want, out int idxChd) == 0)
        {
            RvFile f = categoryDir.Child(idxChd);
            if (f != null && f.FileType == FileType.CHD && f.GotStatus == GotStatus.Got)
                return f;
        }
        if (categoryDir.ChildNameSearch(FileType.File, want, out int idxFile) == 0)
        {
            RvFile f = categoryDir.Child(idxFile);
            if (f != null && f.IsFile && f.GotStatus == GotStatus.Got)
                return f;
        }
        return null;
    }

    /// <summary>
    /// Returns ToSort search roots relevant to a set, including primary ToSort and ToSortCache.
    /// </summary>
    private static List<RvFile> GetToSortSearchRoots(RvFile chdDir)
    {
        List<RvFile> dirs = new List<RvFile>();
        if (chdDir == null)
            return dirs;

        string setName = chdDir.Name ?? "";
        if (setName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            setName = System.IO.Path.GetFileNameWithoutExtension(setName);
        if (string.IsNullOrWhiteSpace(setName))
            return dirs;

        string categoryName = chdDir.Parent?.Name ?? "";
        AddToSortSearchRoots(DB.GetToSortPrimary(), categoryName, setName, dirs);
        AddToSortSearchRoots(DB.GetToSortCache(), categoryName, setName, dirs);
        return dirs;
    }

    private static void AddToSortSearchRoots(RvFile toSortRoot, string categoryName, string setName, List<RvFile> output)
    {
        if (toSortRoot == null || !toSortRoot.IsDirectory)
            return;

        output.Add(toSortRoot);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            if (toSortRoot.ChildNameSearch(FileType.Dir, categoryName, out int idxCat) == 0)
            {
                RvFile cat = toSortRoot.Child(idxCat);
                if (cat != null && cat.IsDirectory)
                {
                    output.Add(cat);
                    if (cat.ChildNameSearch(FileType.Dir, setName, out int idxSet) == 0)
                    {
                        RvFile setDir = cat.Child(idxSet);
                        if (setDir != null && setDir.IsDirectory)
                            output.Add(setDir);
                    }
                }
            }
        }

        if (toSortRoot.ChildNameSearch(FileType.Dir, setName, out int idxDirect) == 0)
        {
            RvFile setDir = toSortRoot.Child(idxDirect);
            if (setDir != null && setDir.IsDirectory)
                output.Add(setDir);
        }
    }

    private static RvFile FindFileByNameInDirs(List<RvFile> dirs, string fileName)
    {
        if (dirs == null || dirs.Count == 0 || string.IsNullOrWhiteSpace(fileName))
            return null;
        for (int i = 0; i < dirs.Count; i++)
        {
            RvFile dir = dirs[i];
            if (dir == null || !dir.IsDirectory)
                continue;
            RvFile f = FindFileByNameRecursive(dir, fileName, 4);
            if (f != null)
                return f;
        }
        return null;
    }

    private static RvFile FindFileByNameRecursive(RvFile dir, string fileName, int depthLeft)
    {
        if (dir == null || !dir.IsDirectory || depthLeft < 0)
            return null;
        if (dir.ChildNameSearch(FileType.File, fileName, out int idx) == 0)
        {
            RvFile f = dir.Child(idx);
            if (f != null && f.IsFile && f.GotStatus == GotStatus.Got)
                return f;
        }
        if (dir.ChildNameSearch(FileType.CHD, fileName, out int idxChd) == 0)
        {
            RvFile f = dir.Child(idxChd);
            if (f != null && f.FileType == FileType.CHD && f.GotStatus == GotStatus.Got)
                return f;
        }
        for (int i = 0; i < dir.ChildCount; i++)
        {
            RvFile c = dir.Child(i);
            if (c == null)
                continue;
            if (c.IsFile)
            {
                if (c.GotStatus == GotStatus.Got && string.Equals(c.Name, fileName, StringComparison.OrdinalIgnoreCase))
                    return c;
                continue;
            }
            if (c.IsDirectory)
            {
                if (c.FileType == FileType.CHD && c.GotStatus == GotStatus.Got && string.Equals(c.Name, fileName, StringComparison.OrdinalIgnoreCase))
                    return c;
                RvFile hit = FindFileByNameRecursive(c, fileName, depthLeft - 1);
                if (hit != null)
                    return hit;
            }
        }
        return null;
    }

    private static Dictionary<string, RvFile> BuildFileNameIndex(List<RvFile> roots, int maxDepth)
    {
        Dictionary<string, RvFile> index = new Dictionary<string, RvFile>(StringComparer.OrdinalIgnoreCase);
        if (roots == null)
            return index;
        for (int i = 0; i < roots.Count; i++)
        {
            RvFile r = roots[i];
            if (r == null || !r.IsDirectory)
                continue;
            BuildFileNameIndexRecursive(r, maxDepth, index);
        }
        return index;
    }

    private static Dictionary<string, RvFile> BuildTrackNoIndex(List<RvFile> roots, int maxDepth)
    {
        Dictionary<string, RvFile> index = new Dictionary<string, RvFile>(StringComparer.OrdinalIgnoreCase);
        if (roots == null)
            return index;
        for (int i = 0; i < roots.Count; i++)
        {
            RvFile r = roots[i];
            if (r == null || !r.IsDirectory)
                continue;
            BuildTrackNoIndexRecursive(r, maxDepth, index);
        }
        return index;
    }

    private static void BuildTrackNoIndexRecursive(RvFile dir, int depthLeft, Dictionary<string, RvFile> index)
    {
        if (dir == null || !dir.IsDirectory || depthLeft < 0)
            return;
        for (int i = 0; i < dir.ChildCount; i++)
        {
            RvFile c = dir.Child(i);
            if (c == null)
                continue;
            if (c.IsFile)
            {
                if (c.GotStatus != GotStatus.Got || string.IsNullOrWhiteSpace(c.Name))
                    continue;
                string ext = System.IO.Path.GetExtension(c.Name).ToLowerInvariant();
                if (ext != ".bin" && ext != ".raw" && ext != ".iso")
                    continue;
                int trackNo = TryParseTrackNo(c.Name);
                if (trackNo <= 0)
                    continue;
                string key = trackNo.ToString("D2") + ext;
                if (!index.ContainsKey(key))
                    index.Add(key, c);
                continue;
            }
            if (c.IsDirectory)
                BuildTrackNoIndexRecursive(c, depthLeft - 1, index);
        }
    }

    private static void BuildFileNameIndexRecursive(RvFile dir, int depthLeft, Dictionary<string, RvFile> index)
    {
        if (dir == null || !dir.IsDirectory || depthLeft < 0)
            return;
        for (int i = 0; i < dir.ChildCount; i++)
        {
            RvFile c = dir.Child(i);
            if (c == null)
                continue;
            if (c.IsFile)
            {
                if (c.GotStatus != GotStatus.Got || string.IsNullOrWhiteSpace(c.Name))
                    continue;
                if (!index.ContainsKey(c.Name))
                    index.Add(c.Name, c);
                continue;
            }
            if (c.IsDirectory)
                BuildFileNameIndexRecursive(c, depthLeft - 1, index);
        }
    }

    /// <summary>
    /// Lookup tables for quickly finding candidate files by verified hash value.
    /// </summary>
    private sealed class HashIndex
    {
        public readonly Dictionary<string, List<RvFile>> Sha1 = new Dictionary<string, List<RvFile>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<RvFile>> Md5 = new Dictionary<string, List<RvFile>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<RvFile>> Crc = new Dictionary<string, List<RvFile>>(StringComparer.OrdinalIgnoreCase);
    }

    private static HashIndex BuildHashIndex(List<RvFile> roots, int maxDepth)
    {
        HashIndex idx = new HashIndex();
        if (roots == null)
            return idx;
        for (int i = 0; i < roots.Count; i++)
        {
            RvFile r = roots[i];
            if (r == null || !r.IsDirectory)
                continue;
            BuildHashIndexRecursive(r, maxDepth, idx);
        }
        return idx;
    }

    private static void BuildHashIndexRecursive(RvFile dir, int depthLeft, HashIndex idx)
    {
        if (dir == null || !dir.IsDirectory || depthLeft < 0)
            return;
        for (int i = 0; i < dir.ChildCount; i++)
        {
            RvFile c = dir.Child(i);
            if (c == null)
                continue;
            if (c.IsFile)
            {
                if (c.GotStatus != GotStatus.Got)
                    continue;
                AddHash(idx.Sha1, c, c.FileStatusIs(FileStatus.SHA1Verified) ? c.SHA1 : null);
                AddHash(idx.Sha1, c, c.FileStatusIs(FileStatus.AltSHA1Verified) ? c.AltSHA1 : null);
                AddHash(idx.Md5, c, c.FileStatusIs(FileStatus.MD5Verified) ? c.MD5 : null);
                AddHash(idx.Md5, c, c.FileStatusIs(FileStatus.AltMD5Verified) ? c.AltMD5 : null);
                AddHash(idx.Crc, c, c.FileStatusIs(FileStatus.CRCVerified) ? c.CRC : null);
                AddHash(idx.Crc, c, c.FileStatusIs(FileStatus.AltCRCVerified) ? c.AltCRC : null);
                continue;
            }
            if (c.IsDirectory)
                BuildHashIndexRecursive(c, depthLeft - 1, idx);
        }
    }

    private static void AddHash(Dictionary<string, List<RvFile>> map, RvFile file, byte[] hash)
    {
        if (hash == null || hash.Length == 0 || file == null)
            return;
        string key = Convert.ToHexString(hash);
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<RvFile>();
            map.Add(key, list);
        }
        list.Add(file);
    }

    private static RvFile FindByHash(HashIndex idx, RvFile expected)
    {
        if (idx == null || expected == null)
            return null;

        if (expected.SHA1 != null && expected.SHA1.Length > 0)
        {
            string key = Convert.ToHexString(expected.SHA1);
            if (idx.Sha1.TryGetValue(key, out var list))
            {
                RvFile best = PickBestMatch(expected, list);
                if (best != null)
                    return best;
            }
        }

        if (expected.MD5 != null && expected.MD5.Length > 0)
        {
            string key = Convert.ToHexString(expected.MD5);
            if (idx.Md5.TryGetValue(key, out var list))
            {
                RvFile best = PickBestMatch(expected, list);
                if (best != null)
                    return best;
            }
        }

        if (expected.CRC != null && expected.CRC.Length > 0)
        {
            string key = Convert.ToHexString(expected.CRC);
            if (idx.Crc.TryGetValue(key, out var list))
            {
                RvFile best = PickBestMatch(expected, list);
                if (best != null)
                    return best;
            }
        }

        return null;
    }

    private static RvFile PickBestMatch(RvFile expected, List<RvFile> candidates)
    {
        if (expected == null || candidates == null || candidates.Count == 0)
            return null;
        for (int i = 0; i < candidates.Count; i++)
        {
            RvFile c = candidates[i];
            if (c == null || !c.IsFile || c.GotStatus != GotStatus.Got)
                continue;
            if (!IsStrongHashMatch(expected, c))
                continue;
            if (!DBHelper.CheckIfMissingFileCanBeFixedByGotFile(expected, c))
                continue;
            return c;
        }
        return null;
    }

    private static bool IsStrongHashMatch(RvFile expected, RvFile got)
    {
        if (expected == null || got == null)
            return false;
        if (expected.SHA1 != null && expected.SHA1.Length > 0)
        {
            if (got.FileStatusIs(FileStatus.SHA1Verified) && ArrByte.BCompare(expected.SHA1, got.SHA1))
                return true;
            if (got.FileStatusIs(FileStatus.AltSHA1Verified) && ArrByte.BCompare(expected.SHA1, got.AltSHA1))
                return true;
            return false;
        }
        if (expected.MD5 != null && expected.MD5.Length > 0)
        {
            if (got.FileStatusIs(FileStatus.MD5Verified) && ArrByte.BCompare(expected.MD5, got.MD5))
                return true;
            if (got.FileStatusIs(FileStatus.AltMD5Verified) && ArrByte.BCompare(expected.MD5, got.AltMD5))
                return true;
            return false;
        }
        if (expected.CRC != null && expected.CRC.Length > 0)
        {
            if (got.FileStatusIs(FileStatus.CRCVerified) && ArrByte.BCompare(expected.CRC, got.CRC))
                return true;
            if (got.FileStatusIs(FileStatus.AltCRCVerified) && ArrByte.BCompare(expected.CRC, got.AltCRC))
                return true;
            return false;
        }
        return false;
    }

    private static RvFile FindDiscSourceInDirs(RvFile chdDir, List<RvFile> dirs)
    {
        if (chdDir == null || dirs == null || dirs.Count == 0)
            return null;

        string setName = chdDir.Name ?? "";
        if (setName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            setName = System.IO.Path.GetFileNameWithoutExtension(setName);
        if (string.IsNullOrWhiteSpace(setName))
            return null;

        string[] preferred = new[]
        {
            setName + ".chd",
            setName + ".gdi",
            setName + ".cue",
            setName + ".iso"
        };
        for (int i = 0; i < preferred.Length; i++)
        {
            RvFile f = FindFileByNameInDirs(dirs, preferred[i]);
            if (f != null && f.GotStatus == GotStatus.Got)
                return f;
        }

        int bestPriority = 0;
        RvFile best = null;
        for (int i = 0; i < dirs.Count; i++)
        {
            RvFile dir = dirs[i];
            if (dir == null || !dir.IsDirectory)
                continue;

            if (!string.Equals(dir.Name, setName, StringComparison.OrdinalIgnoreCase))
                continue;

            for (int j = 0; j < dir.ChildCount; j++)
            {
                RvFile f = dir.Child(j);
                if (f == null || !f.IsFile || f.GotStatus != GotStatus.Got)
                    continue;
                int pr = SourcePriority(f.Name);
                if (pr > bestPriority)
                {
                    bestPriority = pr;
                    best = f;
                }
            }
        }
        if (best != null)
            return best;

        return null;
    }

    private static string BuildToSortSearchSummary(RvFile chdDir, List<RvFile> toSortSetDirs)
    {
        List<string> lines = new List<string>();
        string rom = chdDir?.Parent?.FullNameCase;
        if (!string.IsNullOrWhiteSpace(rom))
            lines.Add(rom);
        if (toSortSetDirs != null)
        {
            for (int i = 0; i < toSortSetDirs.Count; i++)
            {
                string p = toSortSetDirs[i]?.FullNameCase;
                if (!string.IsNullOrWhiteSpace(p))
                    lines.Add(p);
            }
        }
        if (lines.Count == 0)
            return "ROMRoot / ToSort";
        return string.Join("\n", lines);
    }

    private static int SourcePriority(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;
        string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        switch (ext)
        {
            case ".chd":
                return 4;
            case ".gdi":
                return 3;
            case ".cue":
                return 2;
            case ".iso":
                return 1;
            default:
                return 0;
        }
    }

    private static bool IsAudioTrackFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext == ".bin" || ext == ".raw";
    }

    private static int TryParseTrackNo(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;
        var m = System.Text.RegularExpressions.Regex.Match(name, @"\(Track\s*(\d+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int v))
            return v;
        m = System.Text.RegularExpressions.Regex.Match(name, @"track[\s_]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out v))
            return v;
        return 0;
    }
}
