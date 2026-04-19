using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CHDSharpLib;
using Compress;
using FileScanner;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;

namespace RomVaultCore.FixFile.Utils
{
    public static partial class FixFileUtils
    {
        /// <summary>
        /// Attempts to satisfy an expected CHD container from a disc source.
        /// </summary>
        /// <remarks>
        /// This supports multiple pathways:
        /// - CHD source: if the CHD's member track hashes match the destination set ("track parity"), the CHD can be moved/renamed directly.
        /// - CUE/GDI/ISO source: invoke <c>chdman createcd/createdvd</c> to create the output CHD.
        /// - Track-only audio source: a separate helper may synthesize a minimal CUE and create an audio CHD.
        ///
        /// When <c>ChdKeepCueGdi</c> is enabled, sidecar descriptors are preserved and moved alongside the CHD where applicable.
        /// </remarks>
        /// <param name="sourceFile">Disc source file (CHD/CUE/GDI/ISO, or an archive member).</param>
        /// <param name="destinationFile">Expected destination CHD node.</param>
        /// <param name="returnCode">Result code.</param>
        /// <param name="errorMessage">Error message on failure.</param>
        /// <param name="usedFiles">Files that should be treated as "used" for fix cleanup.</param>
        /// <returns>True if CHD handling was triggered; otherwise false.</returns>
        public static bool TryCreateChdFromDiscSource(RvFile sourceFile, RvFile destinationFile, out ReturnCode returnCode, out string errorMessage, out List<RvFile> usedFiles)
        {
            returnCode = ReturnCode.Good;
            errorMessage = "";
            usedFiles = new List<RvFile>();

            if (sourceFile == null || destinationFile == null)
                return false;
            if (!destinationFile.IsFile && destinationFile.FileType != FileType.CHD)
                return false;
            if (!destinationFile.Name.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!sourceFile.IsFile && sourceFile.FileType != FileType.CHD)
                return false;

            RomVaultCore.DatRule rule = DatReader.FindDatRule(destinationFile.Parent?.DatTreeFullName + "\\");
            if (rule == null || !rule.DiscArchiveAsCHD)
                return false;

            if (destinationFile.IsFile)
            {
                if (!DBHelper.IsChdCreationAllowedForSet(destinationFile, out string incompleteReason))
                {
                    returnCode = ReturnCode.LogicError;
                    errorMessage = incompleteReason;
                    return true;
                }
            }

            string sourceExt = Path.GetExtension(sourceFile.NameCase);
            if (string.Equals(sourceExt, ".chd", StringComparison.OrdinalIgnoreCase))
            {
                if (sourceFile.FileType != FileType.File &&
                    sourceFile.FileType != FileType.CHD &&
                    sourceFile.FileType != FileType.FileZip &&
                    sourceFile.FileType != FileType.FileSevenZip)
                {
                    return false;
                }

                List<string> chdTempPathsToDelete = new List<string>();
                string sourcePathChd = null;
                if (sourceFile.FileType == FileType.File || sourceFile.FileType == FileType.CHD)
                {
                    sourcePathChd = ResolveExistingFilePath(sourceFile.FullNameCase);
                }
                else
                {
                    if (sourceFile.Parent == null || (sourceFile.Parent.FileType != FileType.Zip && sourceFile.Parent.FileType != FileType.SevenZip))
                        return false;

                    string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
                    if (string.IsNullOrWhiteSpace(baseTempDir))
                        baseTempDir = Environment.CurrentDirectory;
                    string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdsrc." + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    chdTempPathsToDelete.Add(tempDir);

                    string fileNameOnly = System.IO.Path.GetFileName((sourceFile.NameCase ?? "").Replace('\\', '/'));
                    if (string.IsNullOrWhiteSpace(fileNameOnly))
                        fileNameOnly = "source.chd";

                    string extracted = System.IO.Path.Combine(tempDir, fileNameOnly);
                    ReturnCode extractRc = ExtractArchiveEntryToPath(sourceFile.Parent, sourceFile.ZipFileIndex, extracted, out string extractError);
                    if (extractRc != ReturnCode.Good)
                    {
                        CleanupTempPaths(chdTempPathsToDelete);
                        returnCode = extractRc;
                        errorMessage = extractError;
                        return true;
                    }
                    sourcePathChd = extracted;
                }

                ReturnCode hashRc = ReadChdInternalHashes(sourcePathChd, false, out uint? chdVersion, out byte[] srcSha1, out byte[] srcMd5, out string hashError);
                if (hashRc != ReturnCode.Good)
                {
                    CleanupTempPaths(chdTempPathsToDelete);
                    returnCode = hashRc;
                    errorMessage = hashError;
                    return true;
                }

                if (chdVersion != 5)
                {
                    CleanupTempPaths(chdTempPathsToDelete);
                    returnCode = ReturnCode.DestinationCheckSumMismatch;
                    errorMessage = $"CHD is not V5 (found V{chdVersion ?? 0}).";
                    return true;
                }

                bool shaOk = destinationFile.SHA1 == null || (srcSha1 != null && ArrByte.BCompare(destinationFile.SHA1, srcSha1));
                bool md5Ok = destinationFile.MD5 == null || (srcMd5 != null && ArrByte.BCompare(destinationFile.MD5, srcMd5));
                if (!shaOk || !md5Ok)
                {
                    CleanupTempPaths(chdTempPathsToDelete);
                    returnCode = ReturnCode.DestinationCheckSumMismatch;
                    errorMessage = "Source CHD does not match expected CHD (internal hash mismatch).";
                    return true;
                }

                string destinationPathChd = ResolveOutputFilePath(destinationFile.FullName);
                try
                {
                    string destDirPhysical = System.IO.Path.GetDirectoryName(destinationPathChd);
                    if (!string.IsNullOrEmpty(destDirPhysical) && !System.IO.Directory.Exists(destDirPhysical))
                        System.IO.Directory.CreateDirectory(destDirPhysical);
                }
                catch
                {
                }

                if (System.IO.File.Exists(destinationPathChd))
                {
                    ReturnCode verifyRc = VerifyAndMergeCreatedChd(destinationPathChd, destinationFile, "", out string verifyError);
                    if (verifyRc != ReturnCode.Good)
                    {
                        CleanupTempPaths(chdTempPathsToDelete);
                        returnCode = verifyRc;
                        errorMessage = verifyError;
                        return true;
                    }

                    CleanupTempPaths(chdTempPathsToDelete);

                    if (sourceFile.FileType != FileType.File && sourceFile.FileType != FileType.CHD)
                        usedFiles.Add(sourceFile);
                    else
                    {
                        try
                        {
                            string sp = System.IO.Path.GetFullPath(ResolveExistingFilePath(sourceFile.FullNameCase));
                            string dp = System.IO.Path.GetFullPath(destinationPathChd);
                            if (!string.Equals(sp, dp, StringComparison.OrdinalIgnoreCase))
                                usedFiles.Add(sourceFile);
                        }
                        catch
                        {
                            usedFiles.Add(sourceFile);
                        }
                    }
                    return true;
                }

                if (sourceFile.FileType == FileType.FileZip || sourceFile.FileType == FileType.FileSevenZip)
                {
                    CleanupTempPaths(chdTempPathsToDelete);

                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(destinationPathChd), "", Path.GetFileName(destinationPathChd), sourceFile.Size, "<--Extract (CHD Internal Hash)", sourceFile.Parent?.FullName, sourceFile.Parent?.Name ?? "", sourceFile.Name));

                    ReturnCode extractRc = ExtractArchiveEntryToPath(sourceFile.Parent, sourceFile.ZipFileIndex, destinationPathChd, out string extractError);
                    if (extractRc != ReturnCode.Good)
                    {
                        returnCode = extractRc;
                        errorMessage = extractError;
                        return true;
                    }

                    ReturnCode verifyRc = VerifyAndMergeCreatedChd(destinationPathChd, destinationFile, "", out string verifyError);
                    if (verifyRc != ReturnCode.Good)
                    {
                        returnCode = verifyRc;
                        errorMessage = verifyError;
                        return true;
                    }

                    usedFiles.Add(sourceFile);
                    return true;
                }

                CleanupTempPaths(chdTempPathsToDelete);

                Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(destinationPathChd), "", Path.GetFileName(destinationPathChd), sourceFile.Size, "<--Move (CHD Internal Hash)", Path.GetDirectoryName(sourcePathChd), "", Path.GetFileName(sourcePathChd)));

                returnCode = MoveFile(sourceFile, destinationFile, destinationPathChd, out bool moved, out errorMessage, forceMove: true, skipDatValidation: true);
                if (returnCode == ReturnCode.Good && moved)
                {
                    ReturnCode verifyRc = VerifyAndMergeCreatedChd(destinationPathChd, destinationFile, "", out string verifyError);
                    if (verifyRc != ReturnCode.Good)
                    {
                        returnCode = verifyRc;
                        errorMessage = verifyError;
                        return true;
                    }

                    if (Settings.rvSettings.ChdKeepCueGdi)
                    {
                        MoveChdSidecarDescriptors(sourcePathChd, destinationPathChd);
                        MarkSidecarDescriptorChildrenGot(destinationFile, destinationPathChd);
                    }
                    usedFiles.Add(sourceFile);
                    return true;
                }

                return true;
            }

            if (!IsDiscSourceExtension(sourceExt) && sourceFile.FileType != FileType.CHD)
                return false;

            if (sourceFile.FileType == FileType.CHD)
            {
                if (CheckChdTrackParity(sourceFile, destinationFile))
                {
                    string sourcePathChd = ResolveExistingFilePath(sourceFile.FullNameCase);
                    string destinationPathChd = ResolveOutputFilePath(destinationFile.FullName);
                    try
                    {
                        string destDirPhysical = System.IO.Path.GetDirectoryName(destinationPathChd);
                        if (!string.IsNullOrEmpty(destDirPhysical) && !System.IO.Directory.Exists(destDirPhysical))
                            System.IO.Directory.CreateDirectory(destDirPhysical);
                    }
                    catch
                    {
                    }

                    Report.ReportProgress(new bgwShowFix(Path.GetDirectoryName(destinationPathChd), "", Path.GetFileName(destinationPathChd), sourceFile.Size, "<--Move (Track Parity)", Path.GetDirectoryName(sourcePathChd), "", Path.GetFileName(sourcePathChd)));

                    returnCode = MoveFile(sourceFile, destinationFile, destinationPathChd, out bool moved, out errorMessage, forceMove: true, skipDatValidation: true);
                    if (returnCode == ReturnCode.Good && moved)
                    {
                        ApplyChdMemberParity(sourceFile, destinationFile);
                        if (Settings.rvSettings.ChdKeepCueGdi)
                        {
                            MoveChdSidecarDescriptors(sourcePathChd, destinationPathChd);
                            MarkSidecarDescriptorChildrenGot(destinationFile, destinationPathChd);
                        }
                        usedFiles.Add(sourceFile);
                        return true;
                    }
                }
                // If tracks don't match, we can't use this CHD as a direct source for another CHD
                return false;
            }

            string sourcePath = null;
            if (sourceFile.FileType == FileType.File)
            {
                sourcePath = ResolveExistingFilePath(sourceFile.FullNameCase);
                if (!File.Exists(sourcePath))
                {
                    string tfc = sourceFile.TreeFullNameCase ?? "";
                    if (tfc.StartsWith("ToSort", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string remainder = tfc.Length > 7 && tfc[6] == System.IO.Path.DirectorySeparatorChar
                                ? tfc.Substring(7)
                                : tfc.Length > 6 && tfc[6] == '/' ? tfc.Substring(7) : tfc;
                            string root = DB.GetToSortPrimary()?.FullNameCase;
                            if (!string.IsNullOrWhiteSpace(root))
                            {
                                string attempt = System.IO.Path.Combine(root, remainder);
                                attempt = ResolveExistingFilePath(attempt);
                                if (File.Exists(attempt))
                                {
                                    sourcePath = attempt;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                    if (!File.Exists(sourcePath))
                    {
                        returnCode = ReturnCode.FileSystemError;
                        errorMessage = "Disc image source file not found on disk.";
                        return true;
                    }
                }
            }
            else if (sourceFile.FileType != FileType.FileZip && sourceFile.FileType != FileType.FileSevenZip)
            {
                returnCode = ReturnCode.LogicError;
                errorMessage = "Disc image source is not a supported file type.";
                return true;
            }

            string destinationPath = ResolveOutputFilePath(destinationFile.FullName);
            string destinationDir = System.IO.Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !System.IO.Directory.Exists(destinationDir))
                System.IO.Directory.CreateDirectory(destinationDir);

            if (RequiresGdiSource(rule.ChdCompressionType))
            {
                string srcExt = (sourceExt ?? "").ToLowerInvariant();
                if (srcExt != ".gdi")
                {
                    if (TryStageDescriptorSetToDestination(sourceFile, destinationFile, sourcePath, destinationDir, out string stageError))
                    {
                        returnCode = ReturnCode.FileSystemError;
                        errorMessage = "__SKIP_PARTIAL_SET__";
                        return true;
                    }

                    returnCode = ReturnCode.FileSystemError;
                    errorMessage = "__SKIP_PARTIAL_SET__";
                    return true;
                }
            }

            string inputPath;
            string workingDir;
            List<string> tempPathsToDelete;
            returnCode = MaterializeDiscInput(sourceFile, destinationFile, rule, sourcePath, out inputPath, out workingDir, out tempPathsToDelete, out errorMessage);
            if (returnCode != ReturnCode.Good)
            {
                if (RequiresGdiSource(rule.ChdCompressionType) &&
                    string.Equals(errorMessage, "__SKIP_PARTIAL_SET__", StringComparison.Ordinal))
                {
                    if (TryStageDescriptorSetToDestination(sourceFile, destinationFile, sourcePath, destinationDir, out string stageError))
                    {
                        CleanupFailedChd(destinationPath);
                        CleanupTempPaths(tempPathsToDelete);
                        returnCode = ReturnCode.FileSystemError;
                        errorMessage = "__SKIP_PARTIAL_SET__";
                        return true;
                    }
                }
                CleanupFailedChd(destinationPath);
                CleanupTempPaths(tempPathsToDelete);
                return true;
            }

            string inputExt = Path.GetExtension(inputPath);

            string command = GetChdmanCommand(inputExt);
            if (command == null)
            {
                returnCode = ReturnCode.LogicError;
                errorMessage = "Disc image type not supported for CHD creation.";
                CleanupTempPaths(tempPathsToDelete);
                return true;
            }

            if (File.Exists(destinationPath))
            {
                try
                {
                    File.SetAttributes(destinationPath, RVIO.FileAttributes.Normal);
                }
                catch
                {
                }
                try
                {
                    File.Delete(destinationPath);
                }
                catch
                {
                }
            }

            string chdmanExe = FindChdmanExePath();

            inputPath = System.IO.Path.GetFullPath(inputPath);
            destinationPath = System.IO.Path.GetFullPath(destinationPath);
            workingDir = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : System.IO.Path.GetFullPath(workingDir);

            string args = BuildChdmanArguments(command, inputPath, destinationPath, destinationFile, rule.ChdCompressionType);

            returnCode = RunChdman(chdmanExe, args, workingDir, out string output);
            if (returnCode != ReturnCode.Good)
            {
                CleanupFailedChd(destinationPath);
                CleanupTempPaths(tempPathsToDelete);
                errorMessage = output;
                return true;
            }

            if (!File.Exists(destinationPath))
            {
                returnCode = ReturnCode.FileSystemError;
                errorMessage = "CHD creation finished but output file was not created.";
                CleanupTempPaths(tempPathsToDelete);
                return true;
            }

            returnCode = VerifyAndMergeCreatedChd(destinationPath, destinationFile, chdmanExe, out errorMessage);
            if (returnCode != ReturnCode.Good)
            {
                CleanupFailedChd(destinationPath);
                CleanupTempPaths(tempPathsToDelete);
                return true;
            }

            bool keepSourceDescriptorInPlace = false;
            if (Settings.rvSettings.ChdKeepCueGdi)
            {
                string ext = (inputExt ?? "").ToLowerInvariant();
                if (ext == ".cue" || ext == ".gdi")
                {
                    string dstDir = System.IO.Path.GetDirectoryName(destinationPath) ?? "";
                    string dstBase = System.IO.Path.GetFileNameWithoutExtension(destinationPath) ?? "";
                    if (!string.IsNullOrWhiteSpace(dstDir) && !string.IsNullOrWhiteSpace(dstBase))
                    {
                        string sidecar = System.IO.Path.Combine(dstDir, dstBase + ext);
                        try
                        {
                            if (!System.IO.File.Exists(sidecar))
                            {
                                string copyFrom = null;
                                if (sourceFile.FileType == FileType.File && !string.IsNullOrWhiteSpace(sourcePath) && System.IO.File.Exists(sourcePath))
                                    copyFrom = sourcePath;
                                else if (!string.IsNullOrWhiteSpace(inputPath) && System.IO.File.Exists(inputPath))
                                    copyFrom = inputPath;

                                if (!string.IsNullOrWhiteSpace(copyFrom))
                                    System.IO.File.Copy(copyFrom, sidecar, overwrite: false);
                            }
                        }
                        catch
                        {
                        }

                        try
                        {
                            if (sourceFile.FileType == FileType.File && !string.IsNullOrWhiteSpace(sourcePath))
                            {
                                string sp = System.IO.Path.GetFullPath(sourcePath);
                                string sc = System.IO.Path.GetFullPath(sidecar);
                                keepSourceDescriptorInPlace = string.Equals(sp, sc, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        catch
                        {
                            keepSourceDescriptorInPlace = false;
                        }
                    }

                    MarkSidecarDescriptorChildrenGot(destinationFile, destinationPath);
                }
            }

            if (!keepSourceDescriptorInPlace)
                usedFiles.Add(sourceFile);
            if (sourceFile.Parent != null && !string.IsNullOrWhiteSpace(inputPath))
            {
                string ext = Path.GetExtension(inputPath).ToLowerInvariant();
                if (ext == ".cue" || ext == ".gdi")
                {
                    IEnumerable<string> refs = ext == ".cue" ? GetReferencedFilesFromCue(inputPath) : GetReferencedFilesFromGdi(inputPath);
                    foreach (string r in refs)
                    {
                        if (string.IsNullOrWhiteSpace(r))
                            continue;
                        string trimmed = r.Trim().Trim('"');
                        string baseName = Path.GetFileName(trimmed);
                        if (!string.IsNullOrWhiteSpace(baseName))
                        {
                            FileType searchType = sourceFile.FileType == FileType.File ? FileType.File : sourceFile.FileType;
                            if (sourceFile.Parent.ChildNameSearch(searchType, baseName, out int idx) == 0)
                            {
                                RvFile rf = sourceFile.Parent.Child(idx);
                                if (rf != null && !usedFiles.Contains(rf))
                                    usedFiles.Add(rf);
                            }
                        }
                    }
                }
            }

            CleanupTempPaths(tempPathsToDelete);
            return true;
        }

        public static bool TryCreateChdFromAudioTracks(List<(int trackNo, RvFile expected, RvFile source)> tracks, RvFile destinationFile, out ReturnCode returnCode, out string errorMessage)
        {
            returnCode = ReturnCode.Good;
            errorMessage = "";

            if (tracks == null || tracks.Count == 0 || destinationFile == null)
                return false;
            if (destinationFile.FileType != FileType.CHD && !destinationFile.IsFile)
                return false;
            if (destinationFile.Name == null || !destinationFile.Name.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                return false;

            RomVaultCore.DatRule rule = DatReader.FindDatRule(destinationFile.Parent?.DatTreeFullName + "\\");
            if (rule == null || !rule.DiscArchiveAsCHD)
                return false;
            if (RequiresGdiSource(rule.ChdCompressionType))
            {
                string stageDestinationPath = ResolveOutputFilePath(destinationFile.FullName);
                string stageDestinationDir = System.IO.Path.GetDirectoryName(stageDestinationPath);
                RvFile destDirNode = destinationFile.Parent;
                if (TryStageTrackSourcesToDestination(tracks, destDirNode, stageDestinationDir, out string stageError))
                {
                    // Raw tracks staged; CHD creation intentionally deferred until .gdi is present.
                    returnCode = ReturnCode.Good;
                    errorMessage = "";
                    return true;
                }

                returnCode = ReturnCode.FileSystemError;
                errorMessage = "__SKIP_PARTIAL_SET__";
                return true;
            }

            string destinationPath = ResolveOutputFilePath(destinationFile.FullName);
            string destinationDir = System.IO.Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !System.IO.Directory.Exists(destinationDir))
                System.IO.Directory.CreateDirectory(destinationDir);

            string baseTempDir = DB.GetToSortCache()?.FullName ?? Environment.CurrentDirectory;
            baseTempDir = ResolveExistingDirectoryPath(baseTempDir);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdtracks." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            List<string> tempPathsToDelete = new List<string> { tempDir };

            List<string> copiedNames = new List<string>();
            try
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    string outName = tracks[i].expected?.NameCase ?? tracks[i].source?.NameCase ?? ("track" + (i + 1).ToString("D2") + ".bin");
                    outName = System.IO.Path.GetFileName(outName.Replace('\\', '/'));
                    string outPath = System.IO.Path.Combine(tempDir, outName);
                    ReturnCode rc = MaterializeSingleFile(tracks[i].source, outPath, out string err);
                    if (rc != ReturnCode.Good)
                    {
                        returnCode = rc;
                        errorMessage = err;
                        CleanupFailedChd(destinationPath);
                        CleanupTempPaths(tempPathsToDelete);
                        return true;
                    }
                    copiedNames.Add(outName);
                }

                string cuePath = System.IO.Path.Combine(tempDir, "disc.cue");
                string cueText = BuildAudioCue(copiedNames);
                System.IO.File.WriteAllText(cuePath, cueText, Encoding.ASCII);

                if (File.Exists(destinationPath))
                {
                    try { File.SetAttributes(destinationPath, RVIO.FileAttributes.Normal); } catch { }
                    try { File.Delete(destinationPath); } catch { }
                }

                string chdmanExe = FindChdmanExePath();
                string args = BuildChdmanArguments("createcd", cuePath, destinationPath, destinationFile, rule.ChdCompressionType);
                returnCode = RunChdman(chdmanExe, args, tempDir, out string output);
                if (returnCode != ReturnCode.Good)
                {
                    CleanupFailedChd(destinationPath);
                    CleanupTempPaths(tempPathsToDelete);
                    errorMessage = output;
                    return true;
                }

                if (!File.Exists(destinationPath))
                {
                    returnCode = ReturnCode.FileSystemError;
                    errorMessage = "CHD creation finished but output file was not created.";
                    CleanupTempPaths(tempPathsToDelete);
                    return true;
                }

                returnCode = VerifyAndMergeCreatedChd(destinationPath, destinationFile, chdmanExe, out errorMessage);
                if (returnCode != ReturnCode.Good)
                {
                    CleanupFailedChd(destinationPath);
                    CleanupTempPaths(tempPathsToDelete);
                    return true;
                }

                if (Settings.rvSettings.ChdKeepCueGdi)
                {
                    try
                    {
                        string dstDir = System.IO.Path.GetDirectoryName(destinationPath) ?? "";
                        string dstBase = System.IO.Path.GetFileNameWithoutExtension(destinationPath) ?? "";
                        if (!string.IsNullOrWhiteSpace(dstDir) && !string.IsNullOrWhiteSpace(dstBase))
                        {
                            string sidecar = System.IO.Path.Combine(dstDir, dstBase + ".cue");
                            if (!System.IO.File.Exists(sidecar) && System.IO.File.Exists(cuePath))
                                System.IO.File.Copy(cuePath, sidecar, overwrite: false);
                        }
                    }
                    catch
                    {
                    }

                    MarkSidecarDescriptorChildrenGot(destinationFile, destinationPath);
                }

                CleanupTempPaths(tempPathsToDelete);
                return true;
            }
            catch (Exception ex)
            {
                CleanupFailedChd(destinationPath);
                CleanupTempPaths(tempPathsToDelete);
                returnCode = ReturnCode.FileSystemError;
                errorMessage = ex.Message;
                return true;
            }
        }

        private static string BuildAudioCue(List<string> trackFileNames)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < trackFileNames.Count; i++)
            {
                string f = trackFileNames[i] ?? "";
                sb.Append("FILE \"").Append(f.Replace("\"", "")).AppendLine("\" BINARY");
                sb.Append("  TRACK ").Append((i + 1).ToString("D2")).AppendLine(" AUDIO");
                sb.AppendLine("    INDEX 01 00:00:00");
            }
            return sb.ToString();
        }

        private static bool TryStageTrackSourcesToDestination(List<(int trackNo, RvFile expected, RvFile source)> tracks, RvFile destDirNode, string destinationDir, out string errorMessage)
        {
            errorMessage = "";
            if (tracks == null || tracks.Count == 0)
                return false;
            if (string.IsNullOrWhiteSpace(destinationDir))
                return false;
            if (destDirNode == null || !destDirNode.IsDirectory)
                return false;

            try
            {
                if (!System.IO.Directory.Exists(destinationDir))
                    System.IO.Directory.CreateDirectory(destinationDir);
            }
            catch
            {
            }

            bool stagedAny = false;
            Dictionary<string, string> stagedBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracks.Count; i++)
            {
                RvFile src = tracks[i].source;
                RvFile exp = tracks[i].expected;
                if (src == null || exp == null || !src.IsFile)
                    continue;

                string outName = exp.NameCase ?? exp.Name ?? src.NameCase ?? src.Name;
                outName = System.IO.Path.GetFileName((outName ?? "").Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(outName))
                    continue;

                string outPath = System.IO.Path.Combine(destinationDir, outName);
                if (System.IO.File.Exists(outPath))
                    continue;

                string sourceKey = BuildStageSourceKey(src);

                if (stagedBySource.TryGetValue(sourceKey, out string stagedPath) &&
                    !string.IsNullOrWhiteSpace(stagedPath) &&
                    System.IO.File.Exists(stagedPath))
                {
                    try
                    {
                        System.IO.File.Copy(stagedPath, outPath, overwrite: false);
                        UpdateOrAddLooseFileToDir(destDirNode, outPath, outName);
                        stagedAny = true;
                    }
                    catch
                    {
                    }
                    continue;
                }

                if (src.FileType == FileType.File)
                {
                    bool inToSort = false;
                    try
                    {
                        inToSort = src.IsInToSort || ((src.TreeFullNameCase ?? "").StartsWith("ToSort", StringComparison.OrdinalIgnoreCase));
                    }
                    catch
                    {
                        inToSort = false;
                    }

                    if (!inToSort)
                    {
                        ReturnCode rc = MaterializeSingleFile(src, outPath, out string err);
                        if (rc != ReturnCode.Good)
                            continue;
                        UpdateOrAddLooseFileToDir(destDirNode, outPath, outName);
                        MarkExpectedStagedGot(exp, outPath);
                        stagedAny = true;
                        stagedBySource[sourceKey] = outPath;
                        continue;
                    }

                    RvFile fileOut = FindExistingDestinationNode(destDirNode, outName) ?? new RvFile(FileType.File) { Name = outName, DatStatus = DatStatus.NotInDat };
                    SyncLooseFileTimestampAndSize(src);
                    ReturnCode moveRc = MoveFile(src, fileOut, outPath, out bool moved, out string moveErr, forceMove: true, skipDatValidation: true);
                    if (moveRc == ReturnCode.RescanNeeded)
                    {
                        SyncLooseFileTimestampAndSize(src);
                        moveRc = MoveFile(src, fileOut, outPath, out moved, out moveErr, forceMove: true, skipDatValidation: true);
                    }

                    if (moveRc != ReturnCode.Good || !moved)
                    {
                        ReturnCode rc = MaterializeSingleFile(src, outPath, out string err);
                        if (rc != ReturnCode.Good)
                            continue;
                        UpdateOrAddLooseFileToDir(destDirNode, outPath, outName);
                        MarkExpectedStagedGot(exp, outPath);
                        stagedAny = true;
                        stagedBySource[sourceKey] = outPath;
                        continue;
                    }

                    EnsureInDestinationDir(destDirNode, fileOut);
                    MarkExpectedStagedGot(exp, outPath);
                    stagedAny = true;
                    stagedBySource[sourceKey] = outPath;
                    continue;
                }

                if ((src.FileType == FileType.FileZip || src.FileType == FileType.FileSevenZip) &&
                    src.Parent != null &&
                    (src.Parent.FileType == FileType.Zip || src.Parent.FileType == FileType.SevenZip))
                {
                    ReturnCode rc = ExtractArchiveEntryToPath(src.Parent, src.ZipFileIndex, outPath, out string err);
                    if (rc != ReturnCode.Good)
                        continue;
                    UpdateOrAddLooseFileToDir(destDirNode, outPath, outName);
                    MarkExpectedStagedGot(exp, outPath);
                    stagedAny = true;
                    stagedBySource[sourceKey] = outPath;
                    continue;
                }

                if (src.FileType == FileType.FileCHD)
                {
                    ReturnCode rc = MaterializeSingleFile(src, outPath, out string err);
                    if (rc != ReturnCode.Good)
                        continue;
                    UpdateOrAddLooseFileToDir(destDirNode, outPath, outName);
                    MarkExpectedStagedGot(exp, outPath);
                    stagedAny = true;
                    stagedBySource[sourceKey] = outPath;
                }
            }

            return stagedAny;
        }

        private static void MarkExpectedStagedGot(RvFile expected, string physicalPath)
        {
            try
            {
                if (expected == null || string.IsNullOrWhiteSpace(physicalPath))
                    return;
                if (!System.IO.File.Exists(physicalPath))
                    return;
                System.IO.FileInfo fi = new System.IO.FileInfo(physicalPath);
                expected.Size = (ulong)fi.Length;
                expected.FileModTimeStamp = fi.LastWriteTime.Ticks;
                expected.GotStatus = GotStatus.Got;
            }
            catch
            {
                try
                {
                    if (expected != null)
                        expected.GotStatus = GotStatus.Got;
                }
                catch
                {
                }
            }
        }

        private static void SyncLooseFileTimestampAndSize(RvFile file)
        {
            try
            {
                if (file == null || file.FileType != FileType.File)
                    return;
                string p = file.FullNameCase ?? file.FullName;
                if (string.IsNullOrWhiteSpace(p))
                    return;
                if (!System.IO.File.Exists(p))
                    return;
                System.IO.FileInfo fi = new System.IO.FileInfo(p);
                file.Size = (ulong)fi.Length;
                file.FileModTimeStamp = fi.LastWriteTime.Ticks;
            }
            catch
            {
            }
        }

        private static bool IsSharedFixSource(RvFile src)
        {
            try
            {
                if (src?.FileGroup?.Files == null)
                    return false;
                int canFixCount = 0;
                for (int i = 0; i < src.FileGroup.Files.Count; i++)
                {
                    RvFile f = src.FileGroup.Files[i];
                    if (f == null)
                        continue;
                    if (f.RepStatus == RepStatus.CanBeFixed || f.RepStatus == RepStatus.CanBeFixedMIA || f.RepStatus == RepStatus.CorruptCanBeFixed)
                    {
                        canFixCount++;
                        if (canFixCount > 1)
                            return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static string BuildStageSourceKey(RvFile src)
        {
            if (src == null)
                return "";
            try
            {
                ulong size = src.Size ?? 0;
                if (src.SHA1 != null && src.SHA1.Length > 0)
                    return "sha1|" + size.ToString() + "|" + src.SHA1.ToHexString();
                if (src.MD5 != null && src.MD5.Length > 0)
                    return "md5|" + size.ToString() + "|" + src.MD5.ToHexString();
                if (src.CRC != null && src.CRC.Length > 0)
                    return "crc|" + size.ToString() + "|" + src.CRC.ToHexString();

                if (src.FileType == FileType.File)
                    return "file|" + (src.FullNameCase ?? src.FullName ?? "");
                if ((src.FileType == FileType.FileZip || src.FileType == FileType.FileSevenZip) && src.Parent != null)
                    return "arc|" + (src.Parent.FullNameCase ?? src.Parent.FullName ?? "") + "|" + src.ZipFileIndex.ToString();
                if (src.FileType == FileType.FileCHD && src.Parent != null)
                    return "chd|" + (src.Parent.FullNameCase ?? src.Parent.FullName ?? "") + "|" + (src.NameCase ?? src.Name ?? "");

                return (src.FileType.ToString() ?? "") + "|" + (src.FullNameCase ?? src.FullName ?? "") + "|" + (src.NameCase ?? src.Name ?? "");
            }
            catch
            {
                return (src.FileType.ToString() ?? "") + "|" + (src.FullNameCase ?? src.FullName ?? "") + "|" + (src.NameCase ?? src.Name ?? "");
            }
        }

        /// <summary>
        /// Ensures a fix routine has a physical file on disk for a given source entry.
        /// </summary>
        /// <remarks>
        /// Fix pipelines frequently operate on virtual members:
        /// - <see cref="FileType.FileCHD"/> members (virtual tracks inside a CHD container)
        /// - archive members (<see cref="FileType.FileZip"/> / <see cref="FileType.FileSevenZip"/>)
        ///
        /// This helper materializes those sources into <paramref name="outputPath"/> so downstream tools
        /// (including <c>chdman</c>) can consume a real file path.
        /// </remarks>
        private static ReturnCode MaterializeSingleFile(RvFile sourceFile, string outputPath, out string errorMessage)
        {
            errorMessage = "";
            if (sourceFile == null || !sourceFile.IsFile)
            {
                errorMessage = "Source track file is not valid.";
                return ReturnCode.LogicError;
            }

            if (sourceFile.FileType == FileType.File)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
                    System.IO.File.Copy(sourceFile.FullNameCase, outputPath, true);
                    return ReturnCode.Good;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return ReturnCode.FileSystemError;
                }
            }

            if (sourceFile.FileType == FileType.FileCHD)
            {
                try
                {
                    RvFile extracted = null;
                    if (sourceFile.FileGroup?.Files != null)
                    {
                        for (int i = 0; i < sourceFile.FileGroup.Files.Count; i++)
                        {
                            RvFile f = sourceFile.FileGroup.Files[i];
                            if (f == null || f.FileType != FileType.File || f.GotStatus != GotStatus.Got)
                                continue;
                            if (!string.Equals(f.Name, sourceFile.Name, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (RVIO.File.Exists(f.FullNameCase))
                            {
                                extracted = f;
                                break;
                            }
                        }
                    }

                    if (extracted == null)
                    {
                        if (sourceFile.Parent == null || sourceFile.Parent.FileType != FileType.CHD)
                        {
                            errorMessage = "CHD track source is missing its parent CHD container.";
                            return ReturnCode.LogicError;
                        }

                        ReturnCode rc = DecompressChdFile.DecompressSourceChdFile(sourceFile.Parent, null, out string err);
                        if (rc != ReturnCode.Good)
                        {
                            errorMessage = err;
                            return rc;
                        }

                        if (sourceFile.FileGroup?.Files != null)
                        {
                            for (int i = 0; i < sourceFile.FileGroup.Files.Count; i++)
                            {
                                RvFile f = sourceFile.FileGroup.Files[i];
                                if (f == null || f.FileType != FileType.File || f.GotStatus != GotStatus.Got)
                                    continue;
                                if (!string.Equals(f.Name, sourceFile.Name, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                if (RVIO.File.Exists(f.FullNameCase))
                                {
                                    extracted = f;
                                    break;
                                }
                            }
                        }
                    }

                    if (extracted == null)
                    {
                        errorMessage = "Unable to materialize CHD track source.";
                        return ReturnCode.FileSystemError;
                    }

                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
                    System.IO.File.Copy(extracted.FullNameCase, outputPath, true);
                    return ReturnCode.Good;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return ReturnCode.FileSystemError;
                }
            }

            if (sourceFile.FileType != FileType.FileZip && sourceFile.FileType != FileType.FileSevenZip)
            {
                errorMessage = "Source track file is not a supported file type.";
                return ReturnCode.LogicError;
            }

            if (sourceFile.Parent == null || (sourceFile.Parent.FileType != FileType.Zip && sourceFile.Parent.FileType != FileType.SevenZip))
            {
                errorMessage = "Archive source is missing its parent archive.";
                return ReturnCode.LogicError;
            }

            return ExtractArchiveEntryToPath(sourceFile.Parent, sourceFile.ZipFileIndex, outputPath, out errorMessage);
        }

        /// <summary>
        /// Checks whether two CHD containers represent the same member set by comparing track hashes.
        /// </summary>
        /// <remarks>
        /// This is used for the "move by track parity" optimization: when the source CHD's member track hashes
        /// match the destination set's expected CHD members, the CHD can be moved/renamed directly instead of
        /// extracting and rebuilding.
        /// </remarks>
        private static bool CheckChdTrackParity(RvFile source, RvFile destination)
        {
            if (source == null || destination == null)
                return false;

            List<RvFile> srcTracks = GetChdTrackChildren(source);
            List<RvFile> dstTracks = GetChdTrackChildren(destination);

            if (srcTracks.Count == 0 || dstTracks.Count == 0)
                return false;
            if (srcTracks.Count != dstTracks.Count)
                return false;

            bool[] used = new bool[dstTracks.Count];
            for (int i = 0; i < srcTracks.Count; i++)
            {
                RvFile s = srcTracks[i];
                bool found = false;
                for (int j = 0; j < dstTracks.Count; j++)
                {
                    if (used[j])
                        continue;
                    RvFile d = dstTracks[j];

                    if (IsHashMatch(s, d))
                    {
                        used[j] = true;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns CHD children that represent track-like members suitable for parity comparison.
        /// </summary>
        private static List<RvFile> GetChdTrackChildren(RvFile chd)
        {
            List<RvFile> tracks = new List<RvFile>();
            if (chd == null)
                return tracks;

            for (int i = 0; i < chd.ChildCount; i++)
            {
                RvFile c = chd.Child(i);
                if (c == null || !c.IsFile)
                    continue;

                string ext = System.IO.Path.GetExtension(c.Name ?? "");
                if (!string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".raw", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".iso", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tracks.Add(c);
            }

            return tracks;
        }

        /// <summary>
        /// Compares two DB entries by size and best-available hash.
        /// </summary>
        /// <remarks>
        /// Used by CHD parity checks and member marking logic. This intentionally refuses to match when
        /// there are no comparable hashes.
        /// </remarks>
        private static bool IsHashMatch(RvFile a, RvFile b)
        {
            if (a == null || b == null)
                return false;

            if (a.Size != b.Size)
                return false;

            // Prefer SHA1 if present, then MD5, then CRC (with size)
            if (a.SHA1 != null && a.SHA1.Length > 0 && b.SHA1 != null && b.SHA1.Length > 0)
                return ArrByte.BCompare(a.SHA1, b.SHA1);

            if (a.MD5 != null && a.MD5.Length > 0 && b.MD5 != null && b.MD5.Length > 0)
                return ArrByte.BCompare(a.MD5, b.MD5);

            if (a.CRC != null && a.CRC.Length > 0 && b.CRC != null && b.CRC.Length > 0)
                return ArrByte.BCompare(a.CRC, b.CRC);

            // If we don't have comparable hashes, do not claim parity.
            return false;
        }

        /// <summary>
        /// Moves sidecar descriptor files (CUE/GDI) alongside a CHD when "Keep cue / gdi" is enabled.
        /// </summary>
        /// <remarks>
        /// This is intentionally best-effort: missing sidecars are silently ignored and existing destination
        /// files are not overwritten.
        /// </remarks>
        private static void MoveChdSidecarDescriptors(string sourceChdPath, string destinationChdPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceChdPath) || string.IsNullOrWhiteSpace(destinationChdPath))
                    return;

                string srcDir = System.IO.Path.GetDirectoryName(sourceChdPath) ?? "";
                string dstDir = System.IO.Path.GetDirectoryName(destinationChdPath) ?? "";
                if (string.IsNullOrWhiteSpace(srcDir) || string.IsNullOrWhiteSpace(dstDir))
                    return;

                string srcBase = System.IO.Path.GetFileNameWithoutExtension(sourceChdPath) ?? "";
                string dstBase = System.IO.Path.GetFileNameWithoutExtension(destinationChdPath) ?? srcBase;
                if (string.IsNullOrWhiteSpace(srcBase) || string.IsNullOrWhiteSpace(dstBase))
                    return;

                string[] exts = new[] { ".cue", ".gdi" };
                for (int i = 0; i < exts.Length; i++)
                {
                    string ext = exts[i];
                    string src = System.IO.Path.Combine(srcDir, srcBase + ext);
                    string dst = System.IO.Path.Combine(dstDir, dstBase + ext);

                    if (!System.IO.File.Exists(src))
                        continue;
                    if (System.IO.File.Exists(dst))
                        continue;

                    System.IO.File.Move(src, dst);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Marks descriptor children (CUE/GDI) as collected when sidecar files exist on disk.
        /// </summary>
        private static void MarkSidecarDescriptorChildrenGot(RvFile destinationChd, string destinationChdPath)
        {
            try
            {
                if (destinationChd == null || destinationChd.FileType != FileType.CHD)
                    return;
                if (string.IsNullOrWhiteSpace(destinationChdPath))
                    return;

                string dstDir = System.IO.Path.GetDirectoryName(destinationChdPath) ?? "";
                if (string.IsNullOrWhiteSpace(dstDir))
                    return;

                string baseName = System.IO.Path.GetFileNameWithoutExtension(destinationChdPath) ?? "";
                if (string.IsNullOrWhiteSpace(baseName))
                    return;

                string cuePhys = System.IO.Path.Combine(dstDir, baseName + ".cue");
                string gdiPhys = System.IO.Path.Combine(dstDir, baseName + ".gdi");
                bool haveCue = System.IO.File.Exists(cuePhys);
                bool haveGdi = System.IO.File.Exists(gdiPhys);
                if (!haveCue && !haveGdi)
                    return;

                for (int i = 0; i < destinationChd.ChildCount; i++)
                {
                    RvFile c = destinationChd.Child(i);
                    if (c == null || !c.IsFile || c.FileType != FileType.FileCHD)
                        continue;
                    string n = c.Name ?? "";
                    bool isCue = n.EndsWith(".cue", StringComparison.OrdinalIgnoreCase);
                    bool isGdi = n.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase);
                    if (!isCue && !isGdi)
                        continue;
                    string phys = isCue ? cuePhys : gdiPhys;
                    if (!System.IO.File.Exists(phys))
                        continue;

                    try
                    {
                        var fi = new System.IO.FileInfo(phys);
                        c.FileModTimeStamp = fi.LastWriteTime.Ticks;
                        c.GotStatus = GotStatus.Got;
                    }
                    catch
                    {
                        c.GotStatus = GotStatus.Got;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Applies an already-proven parity result to the destination CHD's member entries.
        /// </summary>
        /// <remarks>
        /// After a CHD is moved into place by parity, the destination set's member entries would otherwise
        /// remain <c>NotGot</c> until the next scan. This marks matching <see cref="FileType.FileCHD"/> children
        /// as <see cref="GotStatus.Got"/> immediately so tree status updates in the UI without requiring a rescan.
        /// </remarks>
        private static void ApplyChdMemberParity(RvFile sourceChd, RvFile destinationChd)
        {
            try
            {
                if (sourceChd == null || destinationChd == null)
                    return;
                if (sourceChd.FileType != FileType.CHD || destinationChd.FileType != FileType.CHD)
                    return;

                List<RvFile> srcTracks = GetChdTrackChildren(sourceChd);
                if (srcTracks.Count == 0)
                    return;

                bool[] used = new bool[srcTracks.Count];
                long ts = destinationChd.FileModTimeStamp;

                for (int i = 0; i < destinationChd.ChildCount; i++)
                {
                    RvFile d = destinationChd.Child(i);
                    if (d == null || !d.IsFile)
                        continue;
                    if (d.FileType != FileType.FileCHD)
                        continue;

                    for (int j = 0; j < srcTracks.Count; j++)
                    {
                        if (used[j])
                            continue;
                        RvFile s = srcTracks[j];
                        if (!IsHashMatch(d, s) && !IsHashMatch(s, d))
                            continue;

                        used[j] = true;
                        if (d.Size == null || d.Size == 0)
                            d.Size = s.Size;
                        d.FileModTimeStamp = ts;
                        d.GotStatus = GotStatus.Got;
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsDiscSourceExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return false;
            switch (ext.ToLowerInvariant())
            {
                case ".cue":
                case ".gdi":
                case ".iso":
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresGdiSource(RomVaultCore.ChdCompressionType chdCompressionType)
            => chdCompressionType == RomVaultCore.ChdCompressionType.Dreamcast;

        private static bool TryStageDescriptorSetToDestination(RvFile sourceFile, RvFile destinationFile, string sourcePath, string destinationDir, out string errorMessage)
        {
            errorMessage = "";
            if (sourceFile == null || destinationFile == null)
                return false;
            if (string.IsNullOrWhiteSpace(destinationDir))
                return false;

            RvFile destDirNode = destinationFile.Parent;
            if (destDirNode == null || !destDirNode.IsDirectory)
                return false;

            try
            {
                if (!System.IO.Directory.Exists(destinationDir))
                    System.IO.Directory.CreateDirectory(destinationDir);
            }
            catch
            {
            }

            bool stagedAny = false;

            if (sourceFile.FileType == FileType.File)
            {
                string descriptorPath = ResolveExistingFilePath(sourcePath);
                string ext = System.IO.Path.GetExtension(descriptorPath).ToLowerInvariant();
                if (ext != ".cue" && ext != ".gdi")
                    return false;

                List<string> refs = new List<string>(
                    ext == ".cue"
                        ? GetReferencedFilesFromCue(descriptorPath)
                        : GetReferencedFilesFromGdi(descriptorPath));
                refs.Insert(0, System.IO.Path.GetFileName(descriptorPath));

                for (int i = 0; i < refs.Count; i++)
                {
                    string r = refs[i];
                    if (string.IsNullOrWhiteSpace(r))
                        continue;
                    string baseName = System.IO.Path.GetFileName(r.Trim().Trim('"'));
                    if (string.IsNullOrWhiteSpace(baseName))
                        continue;

                    string outPath = System.IO.Path.Combine(destinationDir, baseName);
                    if (System.IO.File.Exists(outPath))
                        continue;

                    RvFile fileIn = null;
                    if (string.Equals(baseName, sourceFile.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        fileIn = sourceFile;
                    }
                    else if (sourceFile.Parent != null)
                    {
                        if (sourceFile.Parent.ChildNameSearch(FileType.File, baseName, out int idx) == 0)
                            fileIn = sourceFile.Parent.Child(idx);
                    }

                    if (fileIn == null)
                        continue;

                    RvFile fileOut = FindExistingDestinationNode(destDirNode, baseName) ?? new RvFile(FileType.File) { Name = baseName, DatStatus = DatStatus.NotInDat };

                    ReturnCode rc = MoveFile(fileIn, fileOut, outPath, out bool moved, out string err, forceMove: true, skipDatValidation: true);
                    if (rc != ReturnCode.Good)
                        continue;
                    if (!moved)
                        continue;

                    EnsureInDestinationDir(destDirNode, fileOut);
                    stagedAny = true;
                }
            }
            else if (sourceFile.FileType == FileType.FileZip || sourceFile.FileType == FileType.FileSevenZip)
            {
                if (sourceFile.Parent == null || (sourceFile.Parent.FileType != FileType.Zip && sourceFile.Parent.FileType != FileType.SevenZip))
                    return false;

                string ext = System.IO.Path.GetExtension(sourceFile.NameCase ?? "").ToLowerInvariant();
                if (ext != ".cue" && ext != ".gdi")
                    return false;

                string descriptorName = System.IO.Path.GetFileName((sourceFile.NameCase ?? "").Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(descriptorName))
                    descriptorName = ext == ".gdi" ? "disc.gdi" : "disc.cue";

                string outDescriptorPath = System.IO.Path.Combine(destinationDir, descriptorName);
                if (!System.IO.File.Exists(outDescriptorPath))
                {
                    ReturnCode rc = ExtractArchiveEntryToPath(sourceFile.Parent, sourceFile.ZipFileIndex, outDescriptorPath, out string err);
                    if (rc != ReturnCode.Good)
                        return false;
                    UpdateOrAddLooseFileToDir(destDirNode, outDescriptorPath, descriptorName);
                    stagedAny = true;
                }

                Dictionary<string, int> index = BuildArchiveEntryIndex(sourceFile.Parent, out string idxErr);
                if (index == null)
                    return stagedAny;

                IEnumerable<string> refs = ext == ".cue"
                    ? GetReferencedFilesFromCue(outDescriptorPath)
                    : GetReferencedFilesFromGdi(outDescriptorPath);
                foreach (string r in refs)
                {
                    if (string.IsNullOrWhiteSpace(r))
                        continue;
                    string trimmed = r.Trim().Trim('"');
                    string baseName = System.IO.Path.GetFileName(trimmed);
                    if (string.IsNullOrWhiteSpace(baseName))
                        continue;

                    string outPath = System.IO.Path.Combine(destinationDir, baseName);
                    if (System.IO.File.Exists(outPath))
                        continue;

                    if (!TryFindArchiveEntryIndex(index, trimmed, out int refIndex))
                    {
                        if (!string.IsNullOrWhiteSpace(baseName) && TryFindArchiveEntryIndex(index, baseName, out refIndex))
                        {
                        }
                        else
                        {
                            continue;
                        }
                    }

                    ReturnCode rc = ExtractArchiveEntryToPath(sourceFile.Parent, refIndex, outPath, out string err);
                    if (rc != ReturnCode.Good)
                        continue;

                    UpdateOrAddLooseFileToDir(destDirNode, outPath, baseName);
                    stagedAny = true;
                }

            }
            else
            {
                return false;
            }

            StageExpectedChdMembersToDestination(destinationFile, destDirNode, destinationDir, ref stagedAny);
            return stagedAny;
        }

        private static void StageExpectedChdMembersToDestination(RvFile destinationFile, RvFile destDirNode, string destinationDir, ref bool stagedAny)
        {
            if (destinationFile == null || destDirNode == null || !destDirNode.IsDirectory)
                return;
            if (string.IsNullOrWhiteSpace(destinationDir))
                return;

            if (destinationFile.ChildCount <= 0)
                return;

            for (int i = 0; i < destinationFile.ChildCount; i++)
            {
                RvFile expected = destinationFile.Child(i);
                if (expected == null || !expected.IsFile)
                    continue;

                string name = expected.Name ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
                if (ext == ".cue" || ext == ".gdi" || ext == ".chd")
                    continue;

                if (ext != ".bin" && ext != ".raw" && ext != ".iso")
                    continue;

                string baseName = System.IO.Path.GetFileName(name.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                string outPath = System.IO.Path.Combine(destinationDir, baseName);
                if (System.IO.File.Exists(outPath))
                    continue;

                List<RvFile> sources;
                try
                {
                    sources = RomVaultCore.FixFile.FixAZipCore.FindSourceFile.GetFixFileList(expected);
                }
                catch
                {
                    sources = null;
                }

                if (sources == null || sources.Count == 0)
                    continue;

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
                    best = s;
                    break;
                }

                if (best == null)
                    continue;

                if (best.FileType == FileType.File)
                {
                    RvFile fileOut = FindExistingDestinationNode(destDirNode, baseName) ?? new RvFile(FileType.File) { Name = baseName, DatStatus = DatStatus.NotInDat };
                    ReturnCode rc = MoveFile(best, fileOut, outPath, out bool moved, out string err, forceMove: true, skipDatValidation: true);
                    if (rc != ReturnCode.Good || !moved)
                        continue;
                    EnsureInDestinationDir(destDirNode, fileOut);
                    stagedAny = true;
                    continue;
                }

                if (best.FileType == FileType.FileCHD)
                {
                    ReturnCode rc = MaterializeSingleFile(best, outPath, out string err);
                    if (rc != ReturnCode.Good)
                        continue;
                    UpdateOrAddLooseFileToDir(destDirNode, outPath, baseName);
                    stagedAny = true;
                    continue;
                }

                if ((best.FileType == FileType.FileZip || best.FileType == FileType.FileSevenZip) &&
                    best.Parent != null &&
                    (best.Parent.FileType == FileType.Zip || best.Parent.FileType == FileType.SevenZip))
                {
                    ReturnCode rc = ExtractArchiveEntryToPath(best.Parent, best.ZipFileIndex, outPath, out string err);
                    if (rc != ReturnCode.Good)
                        continue;
                    UpdateOrAddLooseFileToDir(destDirNode, outPath, baseName);
                    stagedAny = true;
                }
            }
        }

        private static RvFile FindExistingDestinationNode(RvFile destDir, string name)
        {
            if (destDir == null || !destDir.IsDirectory || string.IsNullOrWhiteSpace(name))
                return null;

            if (destDir.ChildNameSearch(FileType.File, name, out int idx) != 0)
                return null;

            RvFile found = destDir.Child(idx);
            if (found == null || !found.IsFile)
                return null;

            return found;
        }

        private static void EnsureInDestinationDir(RvFile destDir, RvFile child)
        {
            if (destDir == null || !destDir.IsDirectory || child == null)
                return;

            if (child.Parent == destDir)
                return;

            try
            {
                if (child.Parent != null && child.Parent.FindChild(child, out int oldIndex))
                    child.Parent.ChildRemove(oldIndex);
            }
            catch
            {
            }

            destDir.ChildAdd(child);
        }

        private static void UpdateOrAddLooseFileToDir(RvFile destDir, string fullPath, string name)
        {
            if (destDir == null || !destDir.IsDirectory)
                return;
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(name))
                return;
            try
            {
                if (!System.IO.File.Exists(fullPath))
                    return;
                System.IO.FileInfo fi = new System.IO.FileInfo(fullPath);
                RvFile existing = FindExistingDestinationNode(destDir, name);
                if (existing != null && existing.IsFile)
                {
                    existing.Size = (ulong)fi.Length;
                    existing.FileModTimeStamp = fi.LastWriteTime.Ticks;
                    existing.GotStatus = GotStatus.Got;
                    return;
                }

                RvFile f = new RvFile(FileType.File)
                {
                    Name = name,
                    DatStatus = DatStatus.NotInDat,
                    Size = (ulong)fi.Length,
                    FileModTimeStamp = fi.LastWriteTime.Ticks,
                    GotStatus = GotStatus.Got
                };
                destDir.ChildAdd(f);
            }
            catch
            {
            }
        }

        private static string ResolveDiscInputPath(string sourcePath, string destinationName, RvFile destinationFile, RomVaultCore.ChdCompressionType chdCompressionType)
        {
            string dir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return sourcePath;

            string baseName = Path.GetFileNameWithoutExtension(destinationName);
            bool requireGdi = RequiresGdiSource(chdCompressionType);
            bool preferGdi = IsGdiPreferredPlatform(destinationFile);

            string gdi = Path.Combine(dir, baseName + ".gdi");
            if (File.Exists(gdi) && (preferGdi || requireGdi))
                return gdi;

            string cue = Path.Combine(dir, baseName + ".cue");
            if (requireGdi)
                return sourcePath;
            if (File.Exists(cue))
                return cue;

            if (File.Exists(gdi))
                return gdi;

            string iso = Path.Combine(dir, baseName + ".iso");
            if (File.Exists(iso))
                return iso;

            if (preferGdi)
            {
                string[] anyGdi = Directory.GetFiles(dir, "*.gdi", SearchOption.TopDirectoryOnly);
                if (anyGdi.Length > 0)
                    return anyGdi[0];
            }

            return sourcePath;
        }

        private static bool IsGdiPreferredPlatform(RvFile destinationFile)
        {
            string hint = GetDatHintText(destinationFile);
            if (string.IsNullOrWhiteSpace(hint))
                return false;

            return hint.IndexOf("Arcade - Namco - Sega - Nintendo - Triforce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("Arcade - Sega - Chihiro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("Arcade - Sega - Naomi 2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("Arcade - Sega - Naomi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("Sega - Dreamcast", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPspPlatform(RvFile destinationFile)
        {
            string hint = GetDatHintText(destinationFile);
            if (string.IsNullOrWhiteSpace(hint))
                return false;

            return hint.IndexOf("Sony - PlayStation Portable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("PlayStation Portable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   hint.IndexOf("PSP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetChdmanCommand(string inputExt)
        {
            if (string.IsNullOrWhiteSpace(inputExt))
                return null;

            switch (inputExt.ToLowerInvariant())
            {
                case ".cue":
                case ".gdi":
                    return "createcd";
                case ".iso":
                    return "createdvd";
                default:
                    return null;
            }
        }

        private static string BuildChdmanArguments(string command, string inputPath, string outputPath, RvFile destinationFile, RomVaultCore.ChdCompressionType chdCompressionType)
        {
            string compression = BuildChdmanCompressionArgument(command, chdCompressionType);
            string npArg = BuildChdmanNumProcessorsArgument();

            if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase) &&
                (chdCompressionType == RomVaultCore.ChdCompressionType.PSP || IsPspPlatform(destinationFile)) &&
                outputPath.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
            {
                return $"{command} -i \"{inputPath}\" -o \"{outputPath}\" {compression} {npArg} -hs 2048 -f";
            }

            if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase))
            {
                int hs = GetDvdHunkSizeBytes();
                if (hs > 0)
                    return $"{command} -i \"{inputPath}\" -o \"{outputPath}\" {compression} {npArg} -hs {hs} -f";
            }

            return $"{command} -i \"{inputPath}\" -o \"{outputPath}\" {compression} {npArg} -f";
        }

        private static string BuildChdmanNumProcessorsArgument()
        {
            int np = 0;
            try { np = Settings.rvSettings?.ChdNumProcessors ?? 0; } catch { np = 0; }
            if (np <= 0)
                return "";
            if (np > 256)
                np = 256;
            return $"-np {np}";
        }

        private static int GetDvdHunkSizeBytes()
        {
            int kib = Settings.rvSettings.ChdDvdHunkSizeKiB;
            if (kib <= 0)
                return 0;
            if (kib < 4)
                kib = 4;
            if (kib > 1024)
                kib = 1024;
            int bytes = kib * 1024;
            int sector = 2048;
            bytes = bytes / sector * sector;
            if (bytes < 4096)
                bytes = 4096;
            return bytes;
        }

        private static string BuildChdmanCompressionArgument(string command, RomVaultCore.ChdCompressionType chdCompressionType)
        {
            if (chdCompressionType == RomVaultCore.ChdCompressionType.Auto)
            {
                if (string.Equals(command, "createcd", StringComparison.OrdinalIgnoreCase))
                    return "-c cdzs,cdzl,cdfl";
                if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase))
                    return "-c zstd,zlib,huff,flac";
                return "-c zstd";
            }
            if (chdCompressionType == RomVaultCore.ChdCompressionType.CD)
            {
                if (string.Equals(command, "createcd", StringComparison.OrdinalIgnoreCase))
                    return "-c cdzs,cdzl,cdfl";
                return "-c zstd";
            }

            if (chdCompressionType == RomVaultCore.ChdCompressionType.DVD)
            {
                if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase))
                    return "-c zstd,zlib,huff,flac";
                return "-c cdzs,cdzl,cdfl";
            }

            if (chdCompressionType == RomVaultCore.ChdCompressionType.PSP)
            {
                if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase))
                    return "-c zstd,zlib,huff,flac";
                return "-c cdzs,cdzl,cdfl";
            }

            if (chdCompressionType == RomVaultCore.ChdCompressionType.Dreamcast)
            {
                if (string.Equals(command, "createcd", StringComparison.OrdinalIgnoreCase))
                    return "-c cdzs,cdzl,cdfl";
                return "-c zstd";
            }

            return "-c zstd";
        }

        private static ReturnCode RunChdman(string chdmanExe, string arguments, string workingDirectory, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (string.IsNullOrWhiteSpace(chdmanExe) || (!System.IO.Path.IsPathRooted(chdmanExe) && !System.IO.File.Exists(chdmanExe)))
                {
                    errorMessage = "chdman.exe not found. Place chdman.exe next to ROMVault, in a 'tools' subfolder, or add it to PATH.";
                    return ReturnCode.FileSystemError;
                }
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = chdmanExe,
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process p = new Process { StartInfo = psi, EnableRaisingEvents = true })
                {
                    System.Text.StringBuilder stdout = new System.Text.StringBuilder();
                    System.Text.StringBuilder stderr = new System.Text.StringBuilder();
                    int lastPercent = -1;

                    p.OutputDataReceived += (_, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(e.Data))
                            return;
                        stdout.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += (_, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(e.Data))
                            return;
                        stderr.AppendLine(e.Data);
                        int pct = TryParsePercent(e.Data);
                        if (pct >= 0 && pct <= 100 && pct != lastPercent)
                        {
                            lastPercent = pct;
                            try { Report.ReportProgress(new bgwText($"CHD {pct}%")); } catch { }
                        }
                    };

                    p.Start();
                    ChdmanProcessTracker.Register(p);
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    while (true)
                    {
                        if (p.WaitForExit(250))
                            break;

                        if (Report.CancellationPending())
                        {
                            ChdmanProcessTracker.Kill(p);
                            errorMessage = "Cancelled.";
                            return ReturnCode.Cancel;
                        }
                    }
                    p.WaitForExit();

                    if (p.ExitCode != 0)
                    {
                        errorMessage = $"{stdout}{Environment.NewLine}{stderr}".Trim();
                        if (string.IsNullOrWhiteSpace(errorMessage))
                            errorMessage = $"chdman exited with code {p.ExitCode}.";
                        return ReturnCode.FileSystemError;
                    }

                    errorMessage = $"{stdout}{Environment.NewLine}{stderr}".Trim();
                    return ReturnCode.Good;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return ReturnCode.FileSystemError;
            }
        }

        private static int TryParsePercent(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '%')
                    continue;
                int j = i - 1;
                while (j >= 0 && line[j] >= '0' && line[j] <= '9')
                    j--;
                int start = j + 1;
                int len = i - start;
                if (len <= 0 || len > 3)
                    continue;
                if (int.TryParse(line.Substring(start, len), out int pct))
                    return pct;
            }
            return -1;
        }

        private static ReturnCode VerifyAndMergeCreatedChd(string destinationPath, RvFile destinationFile, string chdmanExe, out string errorMessage)
        {
            errorMessage = "";

            FileInfo fi = new FileInfo(destinationPath);
            long ts = fi.LastWriteTime;

            if (!string.IsNullOrWhiteSpace(chdmanExe))
            {
                ReturnCode verifyRc = RunChdman(chdmanExe, $"verify -i \"{destinationPath}\"", System.IO.Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory, out string verifyOutput);
                if (verifyRc != ReturnCode.Good)
                {
                    errorMessage = string.IsNullOrWhiteSpace(verifyOutput) ? "chdman verify failed." : ("chdman verify failed: " + verifyOutput);
                    return ReturnCode.DestinationCheckSumMismatch;
                }
            }

            uint? chdVersion;
            byte[] chdSha1;
            byte[] chdMd5;
            ReturnCode rc = ReadChdInternalHashes(destinationPath, true, out chdVersion, out chdSha1, out chdMd5, out errorMessage);
            if (rc != ReturnCode.Good)
                return rc;

            if (chdVersion != 5)
            {
                errorMessage = $"CHD is not V5 (found V{chdVersion ?? 0}).";
                return ReturnCode.DestinationCheckSumMismatch;
            }

            if (destinationFile.SHA1 != null && (chdSha1 == null || !ArrByte.BCompare(destinationFile.SHA1, chdSha1)))
            {
                errorMessage = "CHD internal SHA1 does not match DAT.";
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (destinationFile.MD5 != null && (chdMd5 == null || !ArrByte.BCompare(destinationFile.MD5, chdMd5)))
            {
                errorMessage = "CHD internal MD5 does not match DAT.";
                return ReturnCode.DestinationCheckSumMismatch;
            }

            ScannedFile sf = new ScannedFile(FileType.File)
            {
                Name = destinationPath,
                FileModTimeStamp = ts,
                GotStatus = GotStatus.Got,
                DeepScanned = false,
                Size = (ulong)fi.Length
            };
            sf.FileStatusSet(FileStatus.SizeVerified);
            sf.CHDVersion = chdVersion;
            sf.AltSHA1 = chdSha1;
            sf.AltMD5 = chdMd5;
            if (chdSha1 != null)
                sf.FileStatusSet(FileStatus.AltSHA1FromHeader | FileStatus.AltSHA1Verified);
            if (chdMd5 != null)
                sf.FileStatusSet(FileStatus.AltMD5FromHeader | FileStatus.AltMD5Verified);

            destinationFile.FileMergeIn(sf, false);
            destinationFile.CHDVersion = chdVersion;

            try
            {
                if (Settings.rvSettings.ChdTrustContainerForTracks)
                {
                    ScannedFile trust = new ScannedFile(FileType.CHD)
                    {
                        Name = destinationPath,
                        ZipStruct = ZipStructure.None,
                        Comment = ""
                    };
                    for (int i = 0; i < destinationFile.ChildCount; i++)
                    {
                        RvFile exp = destinationFile.Child(i);
                        if (exp == null || !exp.IsFile)
                            continue;
                        string expExt = System.IO.Path.GetExtension(exp.Name ?? "");
                        if (string.Equals(expExt, ".cue", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(expExt, ".gdi", StringComparison.OrdinalIgnoreCase))
                            continue;
                        trust.Add(new ScannedFile(FileType.FileCHD)
                        {
                            Name = exp.Name,
                            FileModTimeStamp = ts,
                            GotStatus = GotStatus.Got,
                            DeepScanned = true,
                            Size = exp.Size,
                            CRC = exp.CRC,
                            SHA1 = exp.SHA1,
                            MD5 = exp.MD5,
                            AltSize = exp.AltSize,
                            AltCRC = exp.AltCRC,
                            AltSHA1 = exp.AltSHA1,
                            AltMD5 = exp.AltMD5
                        });
                    }
                    trust.Sort();
                    destinationFile.MergeInArchive(trust);
                }
                else
                {
                    ScannedFile chdContents = Populate.FromAZipFileArchive(destinationFile, EScanLevel.Level3, null);
                    if (chdContents != null)
                        destinationFile.MergeInArchive(chdContents);
                }
            }
            catch
            {
            }

            return ReturnCode.Good;
        }

        private static string GetDatHintText(RvFile destinationFile)
        {
            if (destinationFile == null)
                return "";

            string datName = destinationFile.Dat?.GetData(RvDat.DatData.DatName) ?? "";
            string datDescription = destinationFile.Dat?.GetData(RvDat.DatData.Description) ?? "";
            string datCategory = destinationFile.Dat?.GetData(RvDat.DatData.Category) ?? "";
            string datRootDir = destinationFile.Dat?.GetData(RvDat.DatData.RootDir) ?? "";

            string gameCategory = destinationFile.Parent?.Game?.GetData(RvGame.GameData.Category) ?? "";
            string gameSourceFile = destinationFile.Parent?.Game?.GetData(RvGame.GameData.Sourcefile) ?? "";

            return $"{datName} | {datDescription} | {datCategory} | {datRootDir} | {gameCategory} | {gameSourceFile}";
        }

        internal static string FindChdmanExePath()
        {
            string baseDir = "";
            try
            {
                baseDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                string candidate = System.IO.Path.Combine(baseDir, "chdman.exe");
                if (System.IO.File.Exists(candidate))
                    return candidate;
                string tools = System.IO.Path.Combine(baseDir, "tools", "chdman.exe");
                if (System.IO.File.Exists(tools))
                    return tools;
            }

            string cwd = "";
            try
            {
                cwd = Environment.CurrentDirectory;
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(cwd))
            {
                string candidate = System.IO.Path.Combine(cwd, "chdman.exe");
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }

            try
            {
                string? path = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    string[] dirs = path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string d in dirs)
                    {
                        string candidate = System.IO.Path.Combine(d.Trim(), "chdman.exe");
                        if (System.IO.File.Exists(candidate))
                            return candidate;
                    }
                }
            }
            catch
            {
            }

            return "";
        }

        private static ReturnCode MaterializeDiscInput(RvFile sourceFile, RvFile destinationFile, RomVaultCore.DatRule rule, string sourcePath, out string inputPath, out string workingDir, out List<string> tempPathsToDelete, out string errorMessage)
        {
            inputPath = null;
            workingDir = null;
            tempPathsToDelete = new List<string>();
            errorMessage = "";

            if (sourceFile.FileType == FileType.File)
            {
                inputPath = ResolveExistingFilePath(ResolveDiscInputPath(sourcePath, destinationFile.Name, destinationFile, rule?.ChdCompressionType ?? RomVaultCore.ChdCompressionType.Auto));
                workingDir = System.IO.Path.GetDirectoryName(inputPath);
                if (!ValidateDiscInputCompleteness(inputPath, workingDir, rule?.ChdCompressionType ?? RomVaultCore.ChdCompressionType.Auto, out errorMessage))
                    return ReturnCode.FileSystemError;
                return ReturnCode.Good;
            }

            if (sourceFile.FileType != FileType.FileZip && sourceFile.FileType != FileType.FileSevenZip)
            {
                errorMessage = "Disc image source is not a supported archive member.";
                return ReturnCode.LogicError;
            }

            if (sourceFile.Parent == null || (sourceFile.Parent.FileType != FileType.Zip && sourceFile.Parent.FileType != FileType.SevenZip))
            {
                errorMessage = "Archive source is missing its parent archive.";
                return ReturnCode.LogicError;
            }

            string baseTempDir = ResolveExistingDirectoryPath(DB.GetToSortCache()?.FullName);
            if (string.IsNullOrWhiteSpace(baseTempDir))
                baseTempDir = Environment.CurrentDirectory;
            string tempDir = System.IO.Path.Combine(baseTempDir, "__RomVault.chdman." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            tempPathsToDelete.Add(tempDir);

            ReturnCode rc = ExtractDiscSetFromArchive(sourceFile.Parent, destinationFile.Name, destinationFile, rule?.ChdCompressionType ?? RomVaultCore.ChdCompressionType.Auto, tempDir, out inputPath, out errorMessage);
            if (rc != ReturnCode.Good)
                return rc;

            workingDir = tempDir;
            return ReturnCode.Good;
        }

        private static bool ValidateDiscInputCompleteness(string inputPath, string workingDir, RomVaultCore.ChdCompressionType chdCompressionType, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(inputPath))
                return true;

            string ext = System.IO.Path.GetExtension(inputPath).ToLowerInvariant();
            if (RequiresGdiSource(chdCompressionType) && ext != ".gdi")
            {
                errorMessage = "Dreamcast CHD compression requires a .gdi source descriptor.";
                return false;
            }
            if (ext != ".cue" && ext != ".gdi")
                return true;

            if (string.IsNullOrWhiteSpace(workingDir))
                workingDir = System.IO.Path.GetDirectoryName(inputPath);

            if (string.IsNullOrWhiteSpace(workingDir))
                return true;

            IEnumerable<string> refs = ext == ".cue"
                ? GetReferencedFilesFromCue(inputPath)
                : GetReferencedFilesFromGdi(inputPath);

            foreach (string r in refs)
            {
                if (string.IsNullOrWhiteSpace(r))
                    continue;

                string trimmed = r.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                string candidate = null;
                if (System.IO.Path.IsPathRooted(trimmed))
                {
                    candidate = trimmed;
                }
                else
                {
                    candidate = NormalizeChildPath(workingDir, trimmed);
                    if (candidate == null)
                    {
                        string baseName = System.IO.Path.GetFileName(trimmed);
                        if (!string.IsNullOrWhiteSpace(baseName))
                            candidate = System.IO.Path.Combine(workingDir, baseName);
                    }
                }

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    errorMessage = "__SKIP_PARTIAL_SET__";
                    return false;
                }

                try
                {
                    if (!System.IO.File.Exists(candidate))
                    {
                        errorMessage = "__SKIP_PARTIAL_SET__";
                        return false;
                    }
                }
                catch
                {
                    errorMessage = "__SKIP_PARTIAL_SET__";
                    return false;
                }
            }

            return true;
        }

        private static ReturnCode ExtractDiscSetFromArchive(RvFile archiveFile, string destinationName, RvFile destinationFile, RomVaultCore.ChdCompressionType chdCompressionType, string tempDir, out string inputPath, out string errorMessage)
        {
            inputPath = null;
            errorMessage = "";

            Dictionary<string, int> entryIndex = BuildArchiveEntryIndex(archiveFile, out errorMessage);
            if (entryIndex == null)
                return ReturnCode.FileSystemError;

            string baseName = Path.GetFileNameWithoutExtension(destinationName);
            string preferExt = IsGdiPreferredPlatform(destinationFile) ? ".gdi" : ".cue";
            bool requireGdi = RequiresGdiSource(chdCompressionType);

            string[] candidates = requireGdi
                ? new[] { baseName + ".gdi" }
                : new[]
                {
                    baseName + preferExt,
                    baseName + (preferExt == ".gdi" ? ".cue" : ".gdi"),
                    baseName + ".iso"
                };

            string chosenEntry = null;
            int chosenIndex = -1;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryFindArchiveEntryIndex(entryIndex, candidates[i], out int idx))
                {
                    chosenEntry = candidates[i];
                    chosenIndex = idx;
                    break;
                }
            }

            if (chosenEntry == null)
            {
                errorMessage = requireGdi
                    ? "Dreamcast CHD compression requires a .gdi source descriptor inside the archive."
                    : "Could not find cue/gdi/iso inside archive matching the expected CHD base name.";
                return ReturnCode.FileSystemError;
            }

            string extractedMain = System.IO.Path.Combine(tempDir, chosenEntry);
            ReturnCode rc = ExtractArchiveEntryToPath(archiveFile, chosenIndex, extractedMain, out errorMessage);
            if (rc != ReturnCode.Good)
                return rc;

            string ext = System.IO.Path.GetExtension(extractedMain).ToLowerInvariant();
            if (ext == ".iso")
            {
                inputPath = extractedMain;
                return ReturnCode.Good;
            }

            List<string> referenced = new List<string>();
            if (ext == ".cue")
                referenced.AddRange(GetReferencedFilesFromCue(extractedMain));
            else if (ext == ".gdi")
                referenced.AddRange(GetReferencedFilesFromGdi(extractedMain));

            for (int i = 0; i < referenced.Count; i++)
            {
                string refName = referenced[i];
                if (string.IsNullOrWhiteSpace(refName))
                    continue;

                int refIndex;
                if (!TryFindArchiveEntryIndex(entryIndex, refName, out refIndex))
                {
                    errorMessage = $"Referenced file not found in archive: {refName}";
                    return ReturnCode.FileSystemError;
                }

                string outPath = System.IO.Path.Combine(tempDir, refName);
                rc = ExtractArchiveEntryToPath(archiveFile, refIndex, outPath, out errorMessage);
                if (rc != ReturnCode.Good)
                    return rc;
            }

            inputPath = extractedMain;
            return ReturnCode.Good;
        }

        private static Dictionary<string, int> BuildArchiveEntryIndex(RvFile archiveFile, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                ICompress z = archiveFile.FileType == FileType.Zip ? (ICompress)new Compress.StructuredZip.StructuredZip() : new Compress.SevenZip.SevenZ();
                ZipReturn zr = z.ZipFileOpen(archiveFile.FullNameCase, archiveFile.FileModTimeStamp, true);
                if (zr != ZipReturn.ZipGood)
                {
                    errorMessage = $"Error opening archive: {zr}";
                    return null;
                }

                Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < z.LocalFilesCount; i++)
                {
                    FileHeader fh = z.GetFileHeader(i);
                    if (fh == null || fh.IsDirectory)
                        continue;
                    string name = (fh.Filename ?? "").Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    if (!map.ContainsKey(name))
                        map.Add(name, i);
                }
                z.ZipFileClose();
                return map;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        private static bool TryFindArchiveEntryIndex(Dictionary<string, int> entryIndex, string requestedName, out int index)
        {
            index = -1;
            if (entryIndex == null || string.IsNullOrWhiteSpace(requestedName))
                return false;

            string reqNorm = requestedName.Replace('\\', '/').Trim().Trim('"');
            if (entryIndex.TryGetValue(reqNorm, out index))
                return true;

            string reqBase = System.IO.Path.GetFileName(reqNorm);
            if (string.IsNullOrWhiteSpace(reqBase))
                return false;

            int found = -1;
            foreach (KeyValuePair<string, int> kvp in entryIndex)
            {
                string baseName = System.IO.Path.GetFileName(kvp.Key);
                if (!string.Equals(baseName, reqBase, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found != -1)
                    return false;
                found = kvp.Value;
            }

            if (found == -1)
                return false;

            index = found;
            return true;
        }

        private static ReturnCode ExtractArchiveEntryToPath(RvFile archiveFile, int fileIndex, string outputPath, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                ICompress z = archiveFile.FileType == FileType.Zip ? (ICompress)new Compress.StructuredZip.StructuredZip() : new Compress.SevenZip.SevenZ();
                ZipReturn zr = z.ZipFileOpen(archiveFile.FullNameCase, archiveFile.FileModTimeStamp, true);
                if (zr != ZipReturn.ZipGood)
                {
                    errorMessage = $"Error opening archive: {zr}";
                    return ReturnCode.FileSystemError;
                }

                zr = z.ZipFileOpenReadStream(fileIndex, out Stream readStream, out ulong streamSize);
                if (zr != ZipReturn.ZipGood || readStream == null)
                {
                    z.ZipFileClose();
                    errorMessage = $"Error opening archive stream: {zr}";
                    return ReturnCode.FileSystemError;
                }

                string outDir = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outDir))
                    Directory.CreateDirectory(outDir);

                int openRet = RVIO.FileStream.OpenFileWrite(outputPath, RVIO.FileStream.BufSizeMax, out Stream writeStream);
                if (openRet != 0 || writeStream == null)
                {
                    z.ZipFileCloseReadStream();
                    z.ZipFileClose();
                    errorMessage = "Error creating output file for extraction.";
                    return ReturnCode.FileSystemError;
                }

                byte[] buffer = new byte[1024 * 1024];
                ulong remaining = streamSize;
                while (remaining > 0)
                {
                    int toRead = remaining > (ulong)buffer.Length ? buffer.Length : (int)remaining;
                    int read = readStream.Read(buffer, 0, toRead);
                    if (read <= 0)
                        break;
                    writeStream.Write(buffer, 0, read);
                    remaining -= (ulong)read;
                }

                writeStream.Flush();
                writeStream.Close();
                writeStream.Dispose();

                z.ZipFileCloseReadStream();
                z.ZipFileClose();
                return ReturnCode.Good;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return ReturnCode.FileSystemError;
            }
        }

        private static string ResolveExistingFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
                if (System.IO.Path.IsPathRooted(path))
                    return path;
            }
            catch
            {
            }

            try
            {
                if (System.IO.File.Exists(path))
                    return System.IO.Path.GetFullPath(path);
            }
            catch
            {
            }

            try
            {
                string baseDir = "";
                try { baseDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
                DirectoryInfo di = string.IsNullOrWhiteSpace(baseDir) ? null : new DirectoryInfo(baseDir);
                for (int i = 0; i < 8 && di != null; i++)
                {
                    string attempt = System.IO.Path.Combine(di.FullName, path);
                    if (System.IO.File.Exists(attempt))
                        return attempt;
                    di = di.Parent;
                }
            }
            catch
            {
            }

            return path;
        }

        private static string ResolveOutputFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            try
            {
                if (System.IO.Path.IsPathRooted(path))
                    return path;
            }
            catch
            {
            }

            try
            {
                string baseDir = "";
                try { baseDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
                DirectoryInfo di = string.IsNullOrWhiteSpace(baseDir) ? null : new DirectoryInfo(baseDir);
                string firstSegment = "";
                try
                {
                    int sep = path.IndexOfAny(new[] { '\\', '/' });
                    firstSegment = sep >= 0 ? path.Substring(0, sep) : path;
                }
                catch
                {
                }

                for (int i = 0; i < 10 && di != null; i++)
                {
                    if (!string.IsNullOrWhiteSpace(firstSegment))
                    {
                        string candidateRoot = System.IO.Path.Combine(di.FullName, firstSegment);
                        if (System.IO.Directory.Exists(candidateRoot))
                            return System.IO.Path.Combine(di.FullName, path);
                    }

                    string attempt = System.IO.Path.Combine(di.FullName, path);
                    string attemptDir = System.IO.Path.GetDirectoryName(attempt);
                    if (!string.IsNullOrWhiteSpace(attemptDir) && System.IO.Directory.Exists(attemptDir))
                        return attempt;

                    di = di.Parent;
                }
            }
            catch
            {
            }

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static string ResolveExistingDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            try
            {
                if (System.IO.Path.IsPathRooted(path))
                {
                    if (System.IO.Directory.Exists(path))
                        return path;
                }
            }
            catch
            {
            }

            try
            {
                if (System.IO.Directory.Exists(path))
                    return System.IO.Path.GetFullPath(path);
            }
            catch
            {
            }

            try
            {
                string baseDir = "";
                try { baseDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
                System.IO.DirectoryInfo di = string.IsNullOrWhiteSpace(baseDir) ? null : new System.IO.DirectoryInfo(baseDir);
                for (int i = 0; i < 10 && di != null; i++)
                {
                    string attempt = System.IO.Path.Combine(di.FullName, path);
                    if (System.IO.Directory.Exists(attempt))
                        return attempt;
                    di = di.Parent;
                }
            }
            catch
            {
            }

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private static void CleanupTempPaths(List<string> tempPathsToDelete)
        {
            if (tempPathsToDelete == null || tempPathsToDelete.Count == 0)
                return;

            for (int i = tempPathsToDelete.Count - 1; i >= 0; i--)
            {
                string p = tempPathsToDelete[i];
                if (string.IsNullOrWhiteSpace(p))
                    continue;

                try
                {
                    if (Directory.Exists(p))
                    {
                        Directory.Delete(p, true);
                        continue;
                    }
                }
                catch
                {
                }

                TryDeleteFile(p);
            }
        }

        private static void CleanupFailedChd(string destinationPath)
        {
            TryDeleteFile(destinationPath);
        }

        private static IEnumerable<string> GetReferencedFilesFromCue(string cuePath)
        {
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(cuePath);
            }
            catch
            {
                yield break;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();
                if (!trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                    continue;

                int firstQuote = trimmed.IndexOf('"');
                if (firstQuote >= 0)
                {
                    int secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                    if (secondQuote > firstQuote)
                    {
                        string name = trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1).Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                        continue;
                    }
                }

                string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    int startIndex = 1;
                    int endIndex = parts.Length - 1; // last token is the file type (e.g., BINARY/WAVE)
                    if (endIndex > startIndex)
                    {
                        string name = string.Join(" ", parts, startIndex, endIndex - startIndex).Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    }
                    else
                    {
                        string name = parts[1].Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    }
                }
                else if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    yield return parts[1].Trim();
                }
            }
        }

        private static IEnumerable<string> GetReferencedFilesFromGdi(string gdiPath)
        {
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(gdiPath);
            }
            catch
            {
                yield break;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int firstQuote = line.IndexOf('"');
                if (firstQuote >= 0)
                {
                    int secondQuote = line.IndexOf('"', firstQuote + 1);
                    if (secondQuote > firstQuote)
                    {
                        string name = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1).Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                        continue;
                    }
                }

                // Fallback for unquoted filenames that might contain spaces
                string[] parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    int startIndex = 4;
                    int endIndex = parts.Length - 1; // The last token is usually the offset (e.g., '0')
                    if (endIndex > startIndex)
                    {
                        string name = string.Join(" ", parts, startIndex, endIndex - startIndex).Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    }
                    else
                    {
                        string name = parts[4].Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    }
                }
            }
        }

        private static string NormalizeChildPath(string baseDir, string refPath)
        {
            if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(refPath))
                return null;

            string combined;
            try
            {
                combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, refPath));
            }
            catch
            {
                return null;
            }

            string baseFull;
            try
            {
                baseFull = System.IO.Path.GetFullPath(baseDir);
            }
            catch
            {
                return null;
            }

            if (!baseFull.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) &&
                !baseFull.EndsWith(System.IO.Path.AltDirectorySeparatorChar.ToString()))
            {
                baseFull += System.IO.Path.DirectorySeparatorChar;
            }

            if (!combined.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                return null;

            return combined;
        }

        private static void TryDeleteFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return;

            try
            {
                if (!File.Exists(filename))
                    return;
            }
            catch
            {
                return;
            }

            try
            {
                File.SetAttributes(filename, RVIO.FileAttributes.Normal);
            }
            catch
            {
            }

            try
            {
                File.Delete(filename);
            }
            catch
            {
            }
        }

        private static ReturnCode ReadChdInternalHashes(string filename, bool deepCheck, out uint? chdVersion, out byte[] chdSha1, out byte[] chdMd5, out string errorMessage)
        {
            chdVersion = null;
            chdSha1 = null;
            chdMd5 = null;
            errorMessage = "";

            if (!File.Exists(filename))
            {
                errorMessage = "CHD file not found for verification.";
                return ReturnCode.FileSystemError;
            }

            Stream s = null;
            int retval = RVIO.FileStream.OpenFileRead(filename, RVIO.FileStream.BufSizeMax, out s);
            if (retval != 0 || s == null)
            {
                errorMessage = "CHD could not be opened for verification.";
                return ReturnCode.FileSystemError;
            }

            try
            {
                chd_error result = CHD.CheckFile(s, filename, deepCheck, out chdVersion, out chdSha1, out chdMd5);
                if (result != chd_error.CHDERR_NONE && result != chd_error.CHDERR_REQUIRES_PARENT)
                {
                    errorMessage = $"CHD verification error: {result}";
                    return ReturnCode.DestinationCheckSumMismatch;
                }

                return ReturnCode.Good;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return ReturnCode.FileSystemError;
            }
            finally
            {
                try
                {
                    s.Close();
                    s.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
