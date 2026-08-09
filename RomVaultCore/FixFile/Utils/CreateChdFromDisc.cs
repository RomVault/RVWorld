using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CHDSharpLib;
using Compress;
using FileScanner;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;
using RomVaultCore.Utils;
using RVUtils;
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

                bool shaOk = destinationFile.SHA1 == null || (srcSha1 != null && ByteUtils.ByteArrEquals(destinationFile.SHA1, srcSha1));
                bool md5Ok = destinationFile.MD5 == null || (srcMd5 != null && ByteUtils.ByteArrEquals(destinationFile.MD5, srcMd5));
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

                if (!System.IO.File.Exists(destinationPathChd) && rule.ConvertWhileFixing)
                {
                    ReturnCode recompressRc = RecompressChdIfNeeded(sourcePathChd, destinationPathChd, destinationFile, rule, out bool recompressed, out string recompressError);
                    if (recompressRc != ReturnCode.Good)
                    {
                        CleanupTempPaths(chdTempPathsToDelete);
                        returnCode = recompressRc;
                        errorMessage = recompressError;
                        return true;
                    }

                    if (recompressed)
                    {
                        if (Settings.rvSettings.ChdKeepCueGdi &&
                            (sourceFile.FileType == FileType.File || sourceFile.FileType == FileType.CHD))
                        {
                            MoveChdSidecarDescriptors(sourcePathChd, destinationPathChd);
                            MarkSidecarDescriptorChildrenGot(destinationFile, destinationPathChd);
                        }

                        CleanupTempPaths(chdTempPathsToDelete);
                        usedFiles.Add(sourceFile);
                        return true;
                    }
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
                        CleanupFailedChd(destinationPathChd);
                        returnCode = verifyRc;
                        errorMessage = verifyError;
                        return true;
                    }

                    usedFiles.Add(sourceFile);
                    return true;
                }

                CleanupTempPaths(chdTempPathsToDelete);

                ReturnCode sourceVerifyRc = VerifyAndMergeCreatedChd(sourcePathChd, destinationFile, "", out string sourceVerifyError, mergeResults: false);
                if (sourceVerifyRc != ReturnCode.Good)
                {
                    returnCode = sourceVerifyRc;
                    errorMessage = sourceVerifyError;
                    return true;
                }

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

            string command = GetChdmanCommand(inputExt, rule.ChdCompressionType);
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

            string chdmanExe = ChdmanProcessTracker.FindExecutable();

            inputPath = System.IO.Path.GetFullPath(inputPath);
            destinationPath = System.IO.Path.GetFullPath(destinationPath);
            workingDir = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : System.IO.Path.GetFullPath(workingDir);

            returnCode = CreateStandardizedChd(command, inputPath, destinationPath, destinationFile, rule.ChdCompressionType, rule.ChdStorageProfile, rule.ChdHddGeometry, chdmanExe, workingDir, out string output);
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
                if (ext == ".cue" || ext == ".gdi" || ext == ".toc")
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
                if (ext == ".cue" || ext == ".gdi" || ext == ".toc")
                {
                    IEnumerable<string> refs = GetReferencedFilesFromDescriptor(inputPath);
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

                    // SBI corrections are authenticated auxiliary reconstruction
                    // inputs rather than descriptor references.  Consume the
                    // same-stem source after it has been embedded in RVRM.
                    if (ext == ".cue" || ext == ".toc")
                    {
                        string sbiName = Path.GetFileNameWithoutExtension(sourceFile.Name) + ".sbi";
                        FileType searchType = sourceFile.FileType == FileType.File ? FileType.File : sourceFile.FileType;
                        if (sourceFile.Parent.ChildNameSearch(searchType, sbiName, out int sbiIndex) == 0)
                        {
                            RvFile sbi = sourceFile.Parent.Child(sbiIndex);
                            if (sbi != null && !usedFiles.Contains(sbi))
                                usedFiles.Add(sbi);
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

                string chdmanExe = ChdmanProcessTracker.FindExecutable();
                returnCode = CreateStandardizedChd("createcd", cuePath, destinationPath, destinationFile, rule.ChdCompressionType, rule.ChdStorageProfile, rule.ChdHddGeometry, chdmanExe, tempDir, out string output);
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
                return ByteUtils.ByteArrEquals(a.SHA1, b.SHA1);

            if (a.MD5 != null && a.MD5.Length > 0 && b.MD5 != null && b.MD5.Length > 0)
                return ByteUtils.ByteArrEquals(a.MD5, b.MD5);

            if (a.CRC != null && a.CRC.Length > 0 && b.CRC != null && b.CRC.Length > 0)
                return ByteUtils.ByteArrEquals(a.CRC, b.CRC);

            // If we don't have comparable hashes, do not claim parity.
            return false;
        }

        /// <summary>
        /// Moves sidecar descriptor files (CUE/GDI/TOC) alongside a CHD when descriptor retention is enabled.
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

                string[] exts = new[] { ".cue", ".gdi", ".toc" };
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
        /// Marks descriptor children (CUE/GDI/TOC) as collected when sidecar files exist on disk.
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
                string tocPhys = System.IO.Path.Combine(dstDir, baseName + ".toc");
                bool haveCue = System.IO.File.Exists(cuePhys);
                bool haveGdi = System.IO.File.Exists(gdiPhys);
                bool haveToc = System.IO.File.Exists(tocPhys);
                if (!haveCue && !haveGdi && !haveToc)
                    return;

                for (int i = 0; i < destinationChd.ChildCount; i++)
                {
                    RvFile c = destinationChd.Child(i);
                    if (c == null || !c.IsFile || c.FileType != FileType.FileCHD)
                        continue;
                    string n = c.Name ?? "";
                    bool isCue = n.EndsWith(".cue", StringComparison.OrdinalIgnoreCase);
                    bool isGdi = n.EndsWith(".gdi", StringComparison.OrdinalIgnoreCase);
                    bool isToc = n.EndsWith(".toc", StringComparison.OrdinalIgnoreCase);
                    if (!isCue && !isGdi && !isToc)
                        continue;
                    string phys = isCue ? cuePhys : isGdi ? gdiPhys : tocPhys;
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
                case ".toc":
                case ".iso":
                case ".raw":
                case ".img":
                case ".hdd":
                case ".hd":
                case ".avi":
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresGdiSource(RomVaultCore.ChdCompressionType chdCompressionType)
        {
            // Current chdman supports both Redump CUE and TOSEC GDI GD-ROM
            // layouts.  The descriptor family is preserved by extracting CUE
            // back to CUE and GDI back to GDI, so the Dreamcast compression
            // profile must not force a lossy cross-layout conversion.
            return false;
        }

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
                if (ext != ".cue" && ext != ".gdi" && ext != ".toc")
                    return false;

                List<string> refs = new List<string>(
                    GetReferencedFilesFromDescriptor(descriptorPath));
                if ((ext == ".cue" || ext == ".toc") && File.Exists(System.IO.Path.ChangeExtension(descriptorPath, ".sbi")))
                    refs.Add(System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(descriptorPath, ".sbi")));
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
                if (ext != ".cue" && ext != ".gdi" && ext != ".toc")
                    return false;

                string descriptorName = System.IO.Path.GetFileName((sourceFile.NameCase ?? "").Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(descriptorName))
                    descriptorName = ext == ".gdi" ? "disc.gdi" : ext == ".toc" ? "disc.toc" : "disc.cue";

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

                IEnumerable<string> refs = GetReferencedFilesFromDescriptor(outDescriptorPath);
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
                if (ext == ".cue" || ext == ".gdi" || ext == ".toc" || ext == ".chd")
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

            string encodedRoot = GetEncodedMediaRootName(destinationName);
            if (!string.IsNullOrWhiteSpace(encodedRoot))
            {
                string exact = Path.Combine(dir, encodedRoot);
                return File.Exists(exact) ? exact : sourcePath;
            }

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

            string toc = Path.Combine(dir, baseName + ".toc");
            if (File.Exists(toc))
                return toc;

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

        private static string GetEncodedMediaRootName(string chdName)
        {
            if (string.IsNullOrWhiteSpace(chdName) || !chdName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase))
                return null;
            string rootName = Path.GetFileNameWithoutExtension(Path.GetFileName(chdName));
            string extension = Path.GetExtension(rootName).ToLowerInvariant();
            return IsDiscSourceExtension(extension) ? rootName : null;
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

        private static string GetChdmanCommand(string inputExt, RomVaultCore.ChdCompressionType mediaType)
        {
            if (string.IsNullOrWhiteSpace(inputExt))
                return null;

            switch (inputExt.ToLowerInvariant())
            {
                case ".cue":
                case ".gdi":
                case ".toc":
                    return "createcd";
                case ".iso":
                    return "createdvd";
                case ".raw":
                    return mediaType == RomVaultCore.ChdCompressionType.HardDisk ? "createhd" : "createraw";
                case ".img":
                case ".hdd":
                case ".hd":
                    return "createhd";
                case ".avi":
                    return "createld";
                default:
                    return null;
            }
        }

        private static string BuildUncompressedChdmanArguments(string command, string inputPath, string outputPath, ChdEncodingProfileSpec profile, ChdReconstructionManifest manifest, ChdmanIdentity identity)
        {
            string npArg = BuildChdmanNumProcessorsArgument();
            string mediaArg = "";
            if (string.Equals(command, "createraw", StringComparison.OrdinalIgnoreCase))
                mediaArg = "-us 1";
            else if (string.Equals(command, "createhd", StringComparison.OrdinalIgnoreCase))
            {
                mediaArg = $"-ss {profile.HddSectorSize} -chs {profile.HddCylinders},{profile.HddHeads},{profile.HddSectors}";
            }
            return $"{command} -i \"{inputPath}\" -o \"{outputPath}\" -c none {npArg} {mediaArg} -hs {profile.HunkSize} -f";
        }

        private static string BuildCopyChdmanArguments(string inputPath, string outputPath, string codecs, int hunkSize)
        {
            string npArg = BuildChdmanNumProcessorsArgument();
            return $"copy -i \"{inputPath}\" -o \"{outputPath}\" -c {codecs} {npArg} -hs {hunkSize} -f";
        }

        private static ChdEncodingProfileSpec GetStandardProfileForCreation(string command, string inputPath, RvFile destinationFile, RomVaultCore.ChdCompressionType chdCompressionType, RomVaultCore.ChdStorageProfile storageProfile, RomVaultCore.ChdHddGeometryMode geometryMode)
        {
            if (string.Equals(command, "createraw", StringComparison.OrdinalIgnoreCase))
                return ChdEncodingProfile.ForFamily("raw", storageProfile);
            if (string.Equals(command, "createhd", StringComparison.OrdinalIgnoreCase))
                return ChdEncodingProfile.ApplyHddGeometry(ChdEncodingProfile.ForFamily("hdd", storageProfile), new System.IO.FileInfo(inputPath).Length, storageProfile, geometryMode);
            if (string.Equals(command, "createld", StringComparison.OrdinalIgnoreCase))
                return ChdEncodingProfile.ForFamily("laserdisc", storageProfile);
            if (string.Equals(command, "createcd", StringComparison.OrdinalIgnoreCase))
            {
                string extension = System.IO.Path.GetExtension(inputPath ?? "");
                bool gdRom = string.Equals(extension, ".gdi", StringComparison.OrdinalIgnoreCase) ||
                             chdCompressionType == RomVaultCore.ChdCompressionType.Dreamcast ||
                             IsGdiPreferredPlatform(destinationFile);
                return ChdEncodingProfile.ForFamily(gdRom ? "gdi" : "cd", storageProfile);
            }

            bool psp = chdCompressionType == RomVaultCore.ChdCompressionType.PSP || IsPspPlatform(destinationFile);
            return ChdEncodingProfile.ForFamily(psp ? "psp" : "dvd", storageProfile);
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

        private static ReturnCode CreateStandardizedChd(string command, string inputPath, string outputPath, RvFile destinationFile, RomVaultCore.ChdCompressionType chdCompressionType, RomVaultCore.ChdStorageProfile storageProfile, RomVaultCore.ChdHddGeometryMode geometryMode, string chdmanExe, string workingDirectory, out string errorMessage)
        {
            errorMessage = "";
            ChdEncodingProfileSpec profile = GetStandardProfileForCreation(command, inputPath, destinationFile, chdCompressionType, storageProfile, geometryMode);
            string sourceDialect;
            try { sourceDialect = ChdDialect.DetectSource(inputPath, profile.Family); }
            catch { sourceDialect = ""; }
            ChdEncodingProfile.ApplyDialect(profile, sourceDialect);
            string selectedTool = ChdToolchainRegistry.Select(profile.Family, storageProfile, sourceDialect, out string selectionError);
            if (!string.IsNullOrWhiteSpace(selectedTool))
                chdmanExe = selectedTool;
            else if (!string.IsNullOrWhiteSpace(selectionError))
            {
                errorMessage = selectionError;
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (!ChdmanService.TryGetIdentity(chdmanExe, ChdmanProbeLevel.Full, out ChdmanIdentity toolIdentity, out string versionError))
            {
                errorMessage = "Could not identify the chdman encoder version: " + versionError;
                return ReturnCode.FileSystemError;
            }

            if (toolIdentity.Capabilities == null || !toolIdentity.Capabilities.CanWriteProfile(profile.Family, storageProfile))
            {
                errorMessage = $"This chdman build did not pass RomVault's exact {profile.Family.ToUpperInvariant()} {profile.Storage} round-trip capability probe. " +
                               (toolIdentity.Capabilities?.ProbeError ?? "Capability information is unavailable.");
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (string.Equals(command, "createld", StringComparison.OrdinalIgnoreCase))
                return CreateStandardizedLaserDiscChd(inputPath, outputPath, destinationFile, storageProfile, chdmanExe, workingDirectory, toolIdentity, out errorMessage);
            if (string.Equals(command, "createhd", StringComparison.OrdinalIgnoreCase) && new System.IO.FileInfo(inputPath).Length % 512 != 0)
            {
                errorMessage = "Hard-disk CHD input size must be divisible by the standard 512-byte sector size.";
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (!ChdReconstructionManifest.TryCreateFromSource(inputPath, profile, toolIdentity, out ChdReconstructionManifest manifest, out string manifestError))
            {
                errorMessage = "Could not build the embedded CHD reconstruction manifest: " + manifestError;
                return ReturnCode.DestinationCheckSumMismatch;
            }
            bool multiViewAttempted = false;
            if ((Settings.rvSettings?.ChdMultiView ?? true) &&
                !ChdMultiView.TryAttachEquivalentIsoView(manifest, inputPath, destinationFile, out multiViewAttempted, out string multiViewError))
            {
                errorMessage = "Could not prove the alternate ISO view reversible: " + multiViewError;
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (multiViewAttempted)
            {
                try { Report.ReportProgress(new bgwText("CHD multi-view: same-stem ISO matches the single-track CUE payload exactly.")); } catch { }
            }
            if (toolIdentity.Capabilities == null || !toolIdentity.Capabilities.CanWriteDialect(manifest.Dialect, storageProfile))
            {
                errorMessage = "This source uses the '" + manifest.Dialect + "' disc dialect, but the selected chdman did not pass RomVault's exact " + profile.Storage + " round-trip fixture for that dialect. Use a validated newer chdman or keep the source files unchanged.";
                return ReturnCode.DestinationCheckSumMismatch;
            }
            ulong logicalSize = 0;
            for (int i = 0; i < manifest.Tracks.Count; i++)
            {
                if (manifest.Tracks[i].Size > 0)
                    logicalSize += (ulong)manifest.Tracks[i].Size;
            }
            if (!ChdUpgradeRecovery.HasSufficientSpace(outputPath, logicalSize, 0, out long requiredBytes, out long freeBytes, out string spaceError))
            {
                errorMessage = spaceError;
                return ReturnCode.FileSystemError;
            }
            try { Report.ReportProgress(new bgwText($"CHD preflight: temporary space {requiredBytes:N0} bytes; available {freeBytes:N0} bytes")); } catch { }
            string stagePath = outputPath + ".__rvstage." + Guid.NewGuid().ToString("N") + ".chd";
            string manifestPath = outputPath + ".__rvmanifest." + Guid.NewGuid().ToString("N") + ".bin";
            string encoderInputPath = inputPath;
            string descriptorStageRoot = "";
            if (string.Equals(manifest.Dialect, "cue-unicode", StringComparison.OrdinalIgnoreCase) &&
                !ChdDescriptorStager.TryStageCueWithAsciiNames(inputPath, workingDirectory, out encoderInputPath, out descriptorStageRoot, out string descriptorStageError))
            {
                errorMessage = "Could not stage the Unicode CUE for this chdman build: " + descriptorStageError;
                return ReturnCode.FileSystemError;
            }
            try
            {
                ReturnCode rc = RunChdman(chdmanExe, BuildUncompressedChdmanArguments(command, encoderInputPath, stagePath, profile, manifest, toolIdentity), workingDirectory, out string output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }

                string metadata = profile.ToMetadata(toolIdentity);
                rc = RunChdman(chdmanExe,
                    $"addmeta -i \"{stagePath}\" -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt \"{metadata}\" -nocs",
                    workingDirectory,
                    out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }

                System.IO.File.WriteAllBytes(manifestPath, manifest.Serialize());
                rc = RunChdman(chdmanExe,
                    $"addmeta -i \"{stagePath}\" -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf \"{manifestPath}\" -nocs",
                    workingDirectory,
                    out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }

                rc = RunChdman(chdmanExe, BuildCopyChdmanArguments(stagePath, outputPath, profile.Codecs, profile.HunkSize), workingDirectory, out output);
                if (rc == ReturnCode.Good && !ValidateEmbeddedStandardMetadata(outputPath, profile, toolIdentity, out string metadataError))
                {
                    errorMessage = metadataError;
                    CleanupFailedChd(outputPath);
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                errorMessage = output;
                return rc;
            }
            finally
            {
                CleanupFailedChd(stagePath);
                CleanupFailedChd(manifestPath);
                ChdDescriptorStager.TryDelete(descriptorStageRoot);
            }
        }

        private static ReturnCode CreateStandardizedLaserDiscChd(string inputPath, string outputPath, RvFile destinationFile, RomVaultCore.ChdStorageProfile storageProfile, string chdmanExe, string workingDirectory, ChdmanIdentity toolIdentity, out string errorMessage)
        {
            errorMessage = "";
            string token = Guid.NewGuid().ToString("N");
            string encodedPath = outputPath + ".__rvav." + token + ".chd";
            string stagePath = outputPath + ".__rvstage." + token + ".chd";
            string manifestPath = outputPath + ".__rvmanifest." + token + ".bin";
            try
            {
                ReturnCode rc = RunChdman(chdmanExe,
                    $"createld -i \"{inputPath}\" -o \"{encodedPath}\" -c avhu {BuildChdmanNumProcessorsArgument()} -f",
                    workingDirectory, out string output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }
                if (!ChdMetadata.TryReadContainerInfo(encodedPath, out ChdContainerInfo info, out string infoError))
                {
                    errorMessage = "Could not read the LaserDisc staging CHD: " + infoError;
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                long encodedLength = new System.IO.FileInfo(encodedPath).Length;
                if (!ChdUpgradeRecovery.HasSufficientSpace(outputPath, info.LogicalSize, encodedLength,
                        out long requiredBytes, out long freeBytes, out string spaceError))
                {
                    errorMessage = $"{spaceError} Required={requiredBytes:N0}; available={freeBytes:N0}.";
                    return ReturnCode.FileSystemError;
                }

                ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("laserdisc", storageProfile, (int)info.HunkSize, (int)info.UnitSize);
                if (!ChdReconstructionManifest.TryCreateFromSource(inputPath, profile, toolIdentity, out ChdReconstructionManifest manifest, out string manifestError))
                {
                    errorMessage = "Could not build the embedded LaserDisc reconstruction manifest: " + manifestError;
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                if (toolIdentity.Capabilities == null || !toolIdentity.Capabilities.CanWriteDialect(manifest.Dialect, storageProfile))
                {
                    errorMessage = "The selected chdman did not pass RomVault's exact LaserDisc round-trip fixture.";
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                System.IO.File.WriteAllBytes(manifestPath, manifest.Serialize());

                rc = RunChdman(chdmanExe, BuildCopyChdmanArguments(encodedPath, stagePath, "none", profile.HunkSize), workingDirectory, out output);
                if (rc == ReturnCode.Good)
                    rc = RunChdman(chdmanExe, $"addmeta -i \"{stagePath}\" -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt \"{profile.ToMetadata(toolIdentity)}\" -nocs", workingDirectory, out output);
                if (rc == ReturnCode.Good)
                    rc = RunChdman(chdmanExe, $"addmeta -i \"{stagePath}\" -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf \"{manifestPath}\" -nocs", workingDirectory, out output);
                if (rc == ReturnCode.Good)
                    rc = RunChdman(chdmanExe, BuildCopyChdmanArguments(stagePath, outputPath, "avhu", profile.HunkSize), workingDirectory, out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }
                if (!ValidateEmbeddedStandardMetadata(outputPath, profile, toolIdentity, out errorMessage))
                    return ReturnCode.DestinationCheckSumMismatch;
                return ReturnCode.Good;
            }
            finally
            {
                CleanupFailedChd(encodedPath);
                CleanupFailedChd(stagePath);
                CleanupFailedChd(manifestPath);
            }
        }

        public static ReturnCode RecompressCurrentChdIfNeeded(RvFile destinationFile, out bool changed, out string errorMessage)
        {
            changed = false;
            errorMessage = "";
            if (destinationFile == null || destinationFile.FileType != FileType.CHD)
                return ReturnCode.Good;

            RomVaultCore.DatRule rule = DatReader.FindDatRule(destinationFile.Parent?.DatTreeFullName + "\\");
            if (rule == null || !rule.DiscArchiveAsCHD || !rule.ConvertWhileFixing)
                return ReturnCode.Good;

            string path = ResolveExistingFilePath(destinationFile.FullNameCase);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                errorMessage = "Existing CHD could not be found on disk.";
                return ReturnCode.FileSystemError;
            }

            return RecompressChdIfNeeded(path, path, destinationFile, rule, out changed, out errorMessage);
        }

        public static bool CurrentChdNeedsRecompression(RvFile destinationFile)
        {
            if (destinationFile == null || destinationFile.FileType != FileType.CHD || destinationFile.GotStatus != GotStatus.Got)
                return false;

            RomVaultCore.DatRule rule = DatReader.FindDatRule(destinationFile.Parent?.DatTreeFullName + "\\");
            if (rule == null || !rule.DiscArchiveAsCHD || !rule.ConvertWhileFixing)
                return false;

            string path = ResolveExistingFilePath(destinationFile.FullNameCase);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return false;

            bool psp = rule.ChdCompressionType == RomVaultCore.ChdCompressionType.PSP || IsPspPlatform(destinationFile);
            if (!ChdEncodingProfile.TryDescribeExisting(path, psp, rule.ChdStorageProfile, out ChdEncodingProfileSpec profile, out ChdContainerInfo actual, out _))
            {
                destinationFile.ChdStatus = "Nonstandard or unreadable CHD";
                return false;
            }
            string chdmanExe = ChdToolchainRegistry.Select(profile.Family, rule.ChdStorageProfile, null, out _);
            if (string.IsNullOrWhiteSpace(chdmanExe) || !ChdmanService.TryGetIdentity(chdmanExe, ChdmanProbeLevel.Full, out ChdmanIdentity installed, out _))
            {
                destinationFile.ChdStatus = "Standard profile not evaluated: no validated chdman is available";
                return false;
            }

            bool needs = ChdEncodingProfile.NeedsRecompression(path, profile, actual, installed, out string reason);
            destinationFile.ChdStatus = needs
                ? "CHD upgrade available: " + reason
                : string.IsNullOrWhiteSpace(reason) ? "Standard CHD profile current" : reason;
            return needs;
        }

        private static ReturnCode RecompressChdIfNeeded(string sourcePath, string destinationPath, RvFile destinationFile, RomVaultCore.DatRule rule, out bool changed, out string errorMessage)
        {
            changed = false;
            errorMessage = "";
            bool psp = rule.ChdCompressionType == RomVaultCore.ChdCompressionType.PSP || IsPspPlatform(destinationFile);
            if (!ChdEncodingProfile.TryDescribeExisting(sourcePath, psp, rule.ChdStorageProfile, out ChdEncodingProfileSpec profile, out ChdContainerInfo actual, out string describeError))
            {
                errorMessage = describeError;
                return ReturnCode.DestinationCheckSumMismatch;
            }
            string chdmanExe = ChdToolchainRegistry.Select(profile.Family, rule.ChdStorageProfile, null, out _);
            if (string.IsNullOrWhiteSpace(chdmanExe) || !ChdmanService.TryGetIdentity(chdmanExe, ChdmanProbeLevel.Full, out ChdmanIdentity installed, out _))
            {
                // Existing CHDs remain usable when no discovered encoder has
                // passed this exact family/dialect profile.
                return ReturnCode.Good;
            }

            if (installed.Capabilities == null || !installed.Capabilities.CanWriteProfile(profile.Family, rule.ChdStorageProfile))
            {
                errorMessage = $"Installed chdman {installed.VersionText} did not pass the exact {profile.Family.ToUpperInvariant()} {profile.Storage} round-trip capability probe. " +
                               (installed.Capabilities?.ProbeError ?? "Capability information is unavailable.");
                return ReturnCode.DestinationCheckSumMismatch;
            }

            if (!ChdEncodingProfile.NeedsRecompression(sourcePath, profile, actual, installed, out string reason))
                return ReturnCode.Good;

            try
            {
                Report.ReportProgress(new bgwText("CHD recompress: " + reason));
            }
            catch
            {
            }

            return RecompressChdToPath(sourcePath, destinationPath, destinationFile, profile, actual, installed, chdmanExe, out changed, out errorMessage);
        }

        private static ReturnCode RecompressChdToPath(string sourcePath, string destinationPath, RvFile destinationFile, ChdEncodingProfileSpec profile, ChdContainerInfo actual, ChdmanIdentity installed, string chdmanExe, out bool changed, out string errorMessage)
        {
            changed = false;
            errorMessage = "";
            destinationPath = System.IO.Path.GetFullPath(destinationPath);
            string destinationDirectory = System.IO.Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory;
            string token = Guid.NewGuid().ToString("N");
            string stagePath = destinationPath + ".__rvstage." + token + ".chd";
            string finalPath = destinationPath + ".__rvfinal." + token + ".chd";
            string backupPath = destinationPath + ".__rvbackup." + token + ".chd";
            string manifestPath = destinationPath + ".__rvmanifest." + token + ".bin";
            string hddRawPath = destinationPath + ".__rvhdd." + token + ".img";
            int stageHunk = ChdEncodingProfile.GreatestCommonDivisor((int)actual.HunkSize, profile.HunkSize);
            if (stageHunk < 16 || actual.UnitSize == 0 || stageHunk % actual.UnitSize != 0)
            {
                errorMessage = "Could not choose a safe intermediate CHD hunk size for recompression.";
                return ReturnCode.LogicError;
            }

            long sourceLength = 0;
            try { sourceLength = new System.IO.FileInfo(sourcePath).Length; } catch { }
            if (!ChdUpgradeRecovery.HasSufficientSpace(destinationPath, actual.LogicalSize, sourceLength, out long requiredBytes, out long freeBytes, out string spaceError))
            {
                errorMessage = spaceError;
                return ReturnCode.FileSystemError;
            }
            try { Report.ReportProgress(new bgwText($"CHD upgrade preflight: temporary space {requiredBytes:N0} bytes; available {freeBytes:N0} bytes")); } catch { }

            bool installedDestination = false;
            bool backupCreated = false;
            bool recoveryNeeded = false;
            bool preserveInterruptedArtifacts = false;
            string journalId;
            try
            {
                journalId = ChdUpgradeRecovery.Begin(sourcePath, destinationPath, stagePath, finalPath, backupPath, manifestPath, hddRawPath);
                ChdFaultInjection.Check(ChdFaultPoint.JournalCreated);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not create the CHD upgrade recovery journal: " + ex.Message;
                return ReturnCode.FileSystemError;
            }
            try
            {
                bool rebuildHddGeometry = profile.Family == "hdd" && !ChdHddGeometry.Matches(sourcePath, profile);
                ReturnCode rc;
                string output;
                if (rebuildHddGeometry)
                {
                    rc = RunChdman(chdmanExe, $"extracthd -i \"{sourcePath}\" -o \"{hddRawPath}\" -f", destinationDirectory, out output);
                    if (rc == ReturnCode.Good)
                        rc = RunChdman(chdmanExe,
                            $"createhd -i \"{hddRawPath}\" -o \"{stagePath}\" -c none -hs {stageHunk} -ss {profile.HddSectorSize} -chs {profile.HddCylinders},{profile.HddHeads},{profile.HddSectors} -f",
                            destinationDirectory,
                            out output);
                }
                else
                {
                    rc = RunChdman(chdmanExe, BuildCopyChdmanArguments(sourcePath, stagePath, "none", stageHunk), destinationDirectory, out output);
                }
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }
                ChdFaultInjection.Check(ChdFaultPoint.StageCreated);

                string metadata = profile.ToMetadata(installed);
                rc = RunChdman(chdmanExe,
                    $"addmeta -i \"{stagePath}\" -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt \"{metadata}\" -nocs",
                    destinationDirectory,
                    out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }
                ChdFaultInjection.Check(ChdFaultPoint.ProfileMetadataWritten);

                ChdReconstructionManifest.TryRead(sourcePath, out ChdReconstructionManifest previousManifest, out _);
                ChdReconstructionManifest manifest = ChdReconstructionManifest.CreateFromDat(destinationFile, profile, installed, previousManifest);
                if (!ChdManifestPayloadHasher.EnsureSha256(sourcePath, chdmanExe, destinationDirectory, manifest, out string sha256Error))
                {
                    errorMessage = "Could not upgrade the reconstruction manifest to payload SHA-256: " + sha256Error;
                    return ReturnCode.SourceCheckSumMismatch;
                }
                System.IO.File.WriteAllBytes(manifestPath, manifest.Serialize());
                rc = RunChdman(chdmanExe,
                    $"addmeta -i \"{stagePath}\" -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf \"{manifestPath}\" -nocs",
                    destinationDirectory,
                    out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }
                ChdFaultInjection.Check(ChdFaultPoint.ManifestMetadataWritten);

                rc = RunChdman(chdmanExe, BuildCopyChdmanArguments(stagePath, finalPath, profile.Codecs, profile.HunkSize), destinationDirectory, out output);
                if (rc != ReturnCode.Good)
                {
                    errorMessage = output;
                    return rc;
                }

                if (!ValidateEmbeddedStandardMetadata(finalPath, profile, installed, out string metadataError))
                {
                    errorMessage = metadataError;
                    return ReturnCode.DestinationCheckSumMismatch;
                }
                ChdFaultInjection.Check(ChdFaultPoint.FinalCreated);

                if (System.IO.File.Exists(destinationPath))
                {
                    try { System.IO.File.SetAttributes(destinationPath, System.IO.FileAttributes.Normal); } catch { }
                    try
                    {
                        System.IO.File.Replace(finalPath, destinationPath, backupPath, true);
                        backupCreated = true;
                        ChdFaultInjection.Check(ChdFaultPoint.BackupCreated);
                    }
                    catch
                    {
                        System.IO.File.Move(destinationPath, backupPath);
                        backupCreated = true;
                        ChdFaultInjection.Check(ChdFaultPoint.BackupCreated);
                        try
                        {
                            System.IO.File.Move(finalPath, destinationPath);
                        }
                        catch
                        {
                            System.IO.File.Move(backupPath, destinationPath);
                            backupCreated = false;
                            throw;
                        }
                    }
                }
                else
                {
                    System.IO.File.Move(finalPath, destinationPath);
                }
                installedDestination = true;
                recoveryNeeded = true;
                ChdUpgradeRecovery.MarkInstalled(journalId);
                ChdFaultInjection.Check(ChdFaultPoint.DestinationInstalled);

                rc = VerifyAndMergeCreatedChd(destinationPath, destinationFile, chdmanExe, out errorMessage);
                if (rc != ReturnCode.Good)
                {
                    if (backupCreated && System.IO.File.Exists(backupPath))
                    {
                        CleanupFailedChd(destinationPath);
                        System.IO.File.Move(backupPath, destinationPath);
                        backupCreated = false;
                        recoveryNeeded = false;
                    }
                    else
                    {
                        CleanupFailedChd(destinationPath);
                        recoveryNeeded = false;
                    }
                    installedDestination = false;
                    return rc;
                }
                ChdFaultInjection.Check(ChdFaultPoint.DestinationVerified);

                CleanupFailedChd(backupPath);
                backupCreated = false;
                recoveryNeeded = false;
                changed = true;
                return ReturnCode.Good;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                if (ex is ChdInjectedCrashException)
                {
                    preserveInterruptedArtifacts = true;
                    recoveryNeeded = true;
                    return ReturnCode.FileSystemError;
                }
                if (backupCreated && System.IO.File.Exists(backupPath))
                {
                    try
                    {
                        CleanupFailedChd(destinationPath);
                        System.IO.File.Move(backupPath, destinationPath);
                        backupCreated = false;
                        recoveryNeeded = false;
                    }
                    catch
                    {
                        recoveryNeeded = true;
                    }
                }
                else if (installedDestination && !string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    CleanupFailedChd(destinationPath);
                    recoveryNeeded = System.IO.File.Exists(destinationPath);
                }
                return ReturnCode.FileSystemError;
            }
            finally
            {
                if (!preserveInterruptedArtifacts)
                {
                    CleanupFailedChd(stagePath);
                    CleanupFailedChd(finalPath);
                    CleanupFailedChd(manifestPath);
                    CleanupFailedChd(hddRawPath);
                    if (!backupCreated)
                        CleanupFailedChd(backupPath);
                    if (!recoveryNeeded)
                    {
                        try { ChdUpgradeRecovery.Complete(journalId); } catch { }
                    }
                }
            }
        }

        private static ReturnCode RunChdman(string chdmanExe, string arguments, string workingDirectory, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(chdmanExe) || (!System.IO.Path.IsPathRooted(chdmanExe) && !System.IO.File.Exists(chdmanExe)))
            {
                errorMessage = "chdman.exe not found. Place chdman.exe next to ROMVault, in a 'tools' subfolder, or add it to PATH.";
                return ReturnCode.FileSystemError;
            }

            int lastPercent = -1;
            string progressPhase = DescribeChdmanProgressPhase(arguments);
            ChdmanRunResult result = ChdmanService.Run(
                chdmanExe,
                arguments,
                string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                0,
                () => Report.CancellationPending(),
                line =>
                {
                    int pct = TryParsePercent(line);
                    if (pct >= 0 && pct <= 100 && pct != lastPercent)
                    {
                        lastPercent = pct;
                        try { Report.ReportProgress(new bgwText($"CHD {progressPhase}: {pct}%")); } catch { }
                    }
                });

            errorMessage = result.Output;
            if (result.Cancelled)
            {
                errorMessage = "Cancelled.";
                return ReturnCode.Cancel;
            }
            if (!result.Success)
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = result.TimedOut ? "chdman timed out." : $"chdman exited with code {result.ExitCode}.";
                return ReturnCode.FileSystemError;
            }

            return ReturnCode.Good;
        }

        private static string DescribeChdmanProgressPhase(string arguments)
        {
            string command = (arguments ?? "").TrimStart();
            int separator = command.IndexOfAny(new[] { ' ', '\t' });
            if (separator >= 0)
                command = command.Substring(0, separator);

            switch (command.ToLowerInvariant())
            {
                case "createcd":
                case "createdvd":
                case "createraw":
                case "createhd":
                case "createld":
                    return "encoding";
                case "copy":
                    return Regex.IsMatch(arguments ?? "", @"(?:^|\s)-c\s+none(?:\s|$)", RegexOptions.IgnoreCase)
                        ? "staging"
                        : "compressing";
                case "verify":
                    return "verifying";
                case "extractcd":
                case "extractdvd":
                case "extractraw":
                case "extracthd":
                case "extractld":
                    return "extracting";
                case "addmeta":
                    return "writing metadata";
                default:
                    return "processing";
            }
        }

        private static bool ValidateEmbeddedStandardMetadata(string chdPath, ChdEncodingProfileSpec expected, ChdmanIdentity identity, out string error)
        {
            error = "";
            int expectedWriterRevision = identity?.Capabilities?.WriterRevision(expected.Family) ?? 0;
            if (!ChdEncodingProfile.TryRead(chdPath, out ChdEncodingProfile profile))
            {
                error = "Created CHD is missing its standard encoder profile metadata.";
                return false;
            }
            if (!string.Equals(profile.Profile, ChdEncodingProfile.ProfileId, StringComparison.Ordinal) ||
                profile.Schema != ChdEncodingProfile.CurrentProfileSchema ||
                profile.ProfileRevision != expected.ProfileRevision ||
                profile.WriterRevision != expectedWriterRevision ||
                !string.Equals(profile.Family, expected.Family, StringComparison.Ordinal) ||
                !string.Equals(profile.Storage, expected.Storage, StringComparison.Ordinal) ||
                !string.Equals((profile.Codecs ?? "").Replace(" ", ""), (expected.Codecs ?? "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                profile.HunkSize != expected.HunkSize ||
                profile.UnitSize != expected.UnitSize ||
                !string.Equals(profile.ToolSha256 ?? "", identity?.BinarySha256 ?? "", StringComparison.OrdinalIgnoreCase))
            {
                error = "Created CHD encoder profile metadata is incomplete or inconsistent.";
                return false;
            }
            if (expected.Family == "hdd" &&
                (profile.HddSectorSize != expected.HddSectorSize || profile.HddCylinders != expected.HddCylinders ||
                 profile.HddHeads != expected.HddHeads || profile.HddSectors != expected.HddSectors ||
                 !ChdHddGeometry.Matches(chdPath, expected)))
            {
                error = "Created hard-disk CHD geometry does not match the selected exact profile.";
                return false;
            }
            if (!ChdMetadata.TryReadContainerInfo(chdPath, out ChdContainerInfo container, out string containerError) ||
                container.HunkSize != expected.HunkSize ||
                (expected.UnitSize > 0 && container.UnitSize != expected.UnitSize))
            {
                error = "Created CHD container geometry does not match the standard profile: " + containerError;
                return false;
            }
            if (!ChdReconstructionManifest.TryRead(chdPath, out ChdReconstructionManifest manifest, out string manifestError))
            {
                error = "Created CHD reconstruction metadata could not be read: " + manifestError;
                return false;
            }
            if (manifest.Schema != ChdReconstructionManifest.CurrentSchema ||
                manifest.ProfileRevision != expected.ProfileRevision ||
                manifest.WriterRevision != expectedWriterRevision ||
                !string.Equals(manifest.ProfileId, ChdEncodingProfile.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(manifest.Family, expected.Family, StringComparison.Ordinal) ||
                !string.Equals(manifest.Storage, expected.Storage, StringComparison.Ordinal) ||
                !string.Equals(manifest.ChdmanSha256 ?? "", identity?.BinarySha256 ?? "", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.CapabilityFingerprint ?? "", identity?.Capabilities?.Fingerprint ?? "", StringComparison.Ordinal) ||
                manifest.Tracks == null || manifest.Tracks.Count == 0)
            {
                error = "Created CHD reconstruction metadata is incomplete or inconsistent.";
                return false;
            }
            return true;
        }

        private static int TryParsePercent(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return -1;

            Match match = Regex.Match(line, @"(?<![\d.])(?<percent>\d{1,3}(?:\.\d+)?)\s*%");
            if (match.Success &&
                double.TryParse(match.Groups["percent"].Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out double percent) &&
                percent >= 0 && percent <= 100)
                return (int)Math.Floor(percent);

            return -1;
        }

        private static ReturnCode VerifyAndMergeCreatedChd(string destinationPath, RvFile destinationFile, string chdmanExe, out string errorMessage, bool mergeResults = true)
        {
            errorMessage = "";

            FileInfo fi = new FileInfo(destinationPath);
            long ts = fi.LastWriteTime;

            if (string.IsNullOrWhiteSpace(chdmanExe))
                chdmanExe = ChdmanProcessTracker.FindExecutable();

            if (string.IsNullOrWhiteSpace(chdmanExe) ||
                (!System.IO.Path.IsPathRooted(chdmanExe) && !System.IO.File.Exists(chdmanExe)))
            {
                errorMessage = "chdman.exe is required to verify the CHD round trip.";
                return ReturnCode.FileSystemError;
            }

            ReturnCode verifyRc = RunChdman(chdmanExe, $"verify -i \"{destinationPath}\"", System.IO.Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory, out string verifyOutput);
            if (verifyRc != ReturnCode.Good)
            {
                errorMessage = string.IsNullOrWhiteSpace(verifyOutput) ? "chdman verify failed." : ("chdman verify failed: " + verifyOutput);
                return ReturnCode.DestinationCheckSumMismatch;
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

            if (destinationFile.SHA1 != null && (chdSha1 == null || !ByteUtils.ByteArrEquals(destinationFile.SHA1, chdSha1)))
            {
                errorMessage = "CHD internal SHA1 does not match DAT.";
                return ReturnCode.DestinationCheckSumMismatch;
            }
            if (destinationFile.MD5 != null && (chdMd5 == null || !ByteUtils.ByteArrEquals(destinationFile.MD5, chdMd5)))
            {
                errorMessage = "CHD internal MD5 does not match DAT.";
                return ReturnCode.DestinationCheckSumMismatch;
            }

            ScannedFile chdContents;
            try
            {
                chdContents = Populate.ScanChdForRoundTrip(destinationFile, destinationPath);
            }
            catch (Exception ex)
            {
                errorMessage = "CHD round-trip extraction failed: " + ex.Message;
                return ReturnCode.DestinationCheckSumMismatch;
            }

            if (!ValidateRoundTripPayload(destinationFile, chdContents, out errorMessage))
                return ReturnCode.DestinationCheckSumMismatch;

            if (!mergeResults)
                return ReturnCode.Good;

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
            destinationFile.MergeInArchive(chdContents);

            return ReturnCode.Good;
        }

        private static bool ValidateRoundTripPayload(RvFile destinationFile, ScannedFile extracted, out string errorMessage)
        {
            errorMessage = "";
            if (destinationFile == null || extracted == null)
            {
                errorMessage = "CHD round-trip extraction produced no contents.";
                return false;
            }

            Dictionary<string, ScannedFile> extractedByName = new Dictionary<string, ScannedFile>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < extracted.Count; i++)
            {
                ScannedFile file = extracted[i];
                if (file?.Name != null && !extractedByName.ContainsKey(file.Name))
                    extractedByName.Add(file.Name, file);
            }

            int payloadCount = 0;
            for (int i = 0; i < destinationFile.ChildCount; i++)
            {
                RvFile expected = destinationFile.Child(i);
                if (expected == null || !expected.IsFile || string.IsNullOrWhiteSpace(expected.Name))
                    continue;

                string extension = System.IO.Path.GetExtension(expected.Name);
                if (string.Equals(extension, ".cue", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".gdi", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".toc", StringComparison.OrdinalIgnoreCase))
                    continue;

                payloadCount++;
                bool hasHash = (expected.SHA1 != null && expected.SHA1.Length > 0) ||
                               (expected.MD5 != null && expected.MD5.Length > 0) ||
                               (expected.CRC != null && expected.CRC.Length > 0);
                if (!hasHash)
                {
                    errorMessage = $"Cannot guarantee CHD round trip for '{expected.Name}' because the DAT has no payload hash.";
                    return false;
                }

                if (!extractedByName.TryGetValue(expected.Name, out ScannedFile actual))
                {
                    errorMessage = $"CHD round trip did not reproduce DAT payload '{expected.Name}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(actual.ChdHashMatchMode) &&
                    !string.Equals(actual.ChdHashMatchMode, "Exact", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"CHD round trip for '{expected.Name}' required '{actual.ChdHashMatchMode}' instead of exact extraction.";
                    return false;
                }

                if (expected.Size.HasValue && expected.Size.Value != 0 && actual.Size != expected.Size.Value)
                {
                    errorMessage = $"CHD round-trip size mismatch for '{expected.Name}'.";
                    return false;
                }
                if (expected.SHA1 != null && (actual.SHA1 == null || !ByteUtils.ByteArrEquals(expected.SHA1, actual.SHA1)))
                {
                    errorMessage = $"CHD round-trip SHA1 mismatch for '{expected.Name}'.";
                    return false;
                }
                if (expected.MD5 != null && (actual.MD5 == null || !ByteUtils.ByteArrEquals(expected.MD5, actual.MD5)))
                {
                    errorMessage = $"CHD round-trip MD5 mismatch for '{expected.Name}'.";
                    return false;
                }
                if (expected.CRC != null && (actual.CRC == null || !ByteUtils.ByteArrEquals(expected.CRC, actual.CRC)))
                {
                    errorMessage = $"CHD round-trip CRC mismatch for '{expected.Name}'.";
                    return false;
                }
            }

            if (payloadCount == 0)
            {
                errorMessage = "Cannot guarantee CHD round trip because the DAT contains no payload files.";
                return false;
            }

            return true;
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
            if (ext != ".cue" && ext != ".gdi" && ext != ".toc")
                return true;

            if (string.IsNullOrWhiteSpace(workingDir))
                workingDir = System.IO.Path.GetDirectoryName(inputPath);

            if (string.IsNullOrWhiteSpace(workingDir))
                return true;

            IEnumerable<string> refs = GetReferencedFilesFromDescriptor(inputPath);

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

            string encodedRoot = GetEncodedMediaRootName(destinationName);
            string baseName = Path.GetFileNameWithoutExtension(destinationName);
            string preferExt = IsGdiPreferredPlatform(destinationFile) ? ".gdi" : ".cue";
            bool requireGdi = RequiresGdiSource(chdCompressionType);

            string[] candidates = !string.IsNullOrWhiteSpace(encodedRoot)
                ? new[] { encodedRoot }
                : requireGdi
                ? new[] { baseName + ".gdi" }
                : new[]
                {
                    baseName + preferExt,
                    baseName + (preferExt == ".gdi" ? ".cue" : ".gdi"),
                    baseName + ".toc",
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
                    : "Could not find cue/gdi/toc/iso inside archive matching the expected CHD base name.";
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
            if (ext == ".cue" || ext == ".gdi" || ext == ".toc")
                referenced.AddRange(GetReferencedFilesFromDescriptor(extractedMain));
            if ((ext == ".cue" || ext == ".toc") && TryFindArchiveEntryIndex(entryIndex, System.IO.Path.ChangeExtension(chosenEntry, ".sbi"), out int sbiIndex))
            {
                string sbiEntry = System.IO.Path.ChangeExtension(chosenEntry, ".sbi");
                string sbiPath = System.IO.Path.Combine(tempDir, sbiEntry);
                rc = ExtractArchiveEntryToPath(archiveFile, sbiIndex, sbiPath, out errorMessage);
                if (rc != ReturnCode.Good)
                    return rc;
            }

            for (int i = 0; i < referenced.Count; i++)
            {
                string refName = referenced[i];
                if (string.IsNullOrWhiteSpace(refName))
                    continue;

                int refIndex;
                if (!TryFindArchiveEntryIndex(entryIndex, refName, out refIndex))
                {
                    // An archive containing a descriptor without every file it
                    // references is an incomplete source set.  It must remain
                    // untouched instead of aborting the entire fix run.
                    errorMessage = "__SKIP_PARTIAL_SET__";
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

        private static IEnumerable<string> GetReferencedFilesFromDescriptor(string descriptorPath)
        {
            string extension = Path.GetExtension(descriptorPath ?? "").ToLowerInvariant();
            if (extension == ".gdi")
                return GetReferencedFilesFromGdi(descriptorPath);
            if (extension == ".toc")
                return GetReferencedFilesFromToc(descriptorPath);
            return GetReferencedFilesFromCue(descriptorPath);
        }

        private static IEnumerable<string> GetReferencedFilesFromToc(string tocPath)
        {
            string[] lines;
            try { lines = System.IO.File.ReadAllLines(tocPath); }
            catch { yield break; }
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Regex.Match(lines[i] ?? "", @"^\s*(?:FILE|DATAFILE|AUDIOFILE)\s+(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;
                string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
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
