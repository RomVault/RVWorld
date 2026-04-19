using System;
using System.IO;
using System.Collections.Generic;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using RomVaultCore.Scanner;
using RomVaultCore.FixFile.Utils;
using FileStream = RVIO.FileStream;
using Path = RVIO.Path;
using FileInfo = RVIO.FileInfo;
using Directory = RVIO.Directory;

namespace RomVaultCore.FixFile.Utils
{
    /// <summary>
    /// Extracts CHD contents into the ToSort cache so individual members can be used as physical sources during fixing.
    /// </summary>
    /// <remarks>
    /// RomVault represents CHD members as <see cref="FileType.FileCHD"/> nodes, which are virtual.
    /// When a fix routine needs real files (e.g., to build a zip/7z, or to copy a track), this helper
    /// materializes them to disk and wires them into the member file groups.
    /// </remarks>
    public static class DecompressChdFile
    {
        /// <summary>
        /// Decompresses a CHD into a uniquely named cache directory and registers extracted files into the DB.
        /// </summary>
        /// <param name="dbChdFile">CHD container node.</param>
        /// <param name="filesUsedForFix">Optional set of files that should be considered "used" for fix cleanup.</param>
        /// <param name="error">Error message on failure.</param>
        /// <returns>A <see cref="ReturnCode"/> indicating success or failure.</returns>
        public static ReturnCode DecompressSourceChdFile(RvFile dbChdFile, Dictionary<string, RvFile> filesUsedForFix, out string error)
        {
            error = "";
            if (dbChdFile == null || dbChdFile.FileType != FileType.CHD)
            {
                error = "Not a CHD file";
                return ReturnCode.LogicError;
            }

            RvFile cacheDir = DB.GetToSortCache();
            if (cacheDir == null)
            {
                error = "ToSort cache not found";
                return ReturnCode.LogicError;
            }

            string cacheName = dbChdFile.Name + ".cache";
            RvFile outDir = new RvFile(FileType.Dir)
            {
                Name = cacheName,
                Parent = cacheDir,
                DatStatus = DatStatus.InToSort,
                GotStatus = GotStatus.Got
            };

            int nameDirIndex = 0;
            while (cacheDir.ChildNameSearch(outDir, out int index) == 0)
            {
                nameDirIndex++;
                outDir.Name = cacheName + " (" + nameDirIndex + ")";
            }
            cacheDir.ChildAdd(outDir);
            Directory.CreateDirectory(outDir.FullName);

            string chdmanExe = FixFileUtils.FindChdmanExePath();
            if (string.IsNullOrWhiteSpace(chdmanExe) || !System.IO.File.Exists(chdmanExe))
            {
                error = "chdman.exe not found at: " + chdmanExe;
                return ReturnCode.FileSystemError;
            }

            ChdmanChdExtractor extractor = new ChdmanChdExtractor(chdmanExe, outDir.FullName);
            bool isDvd = dbChdFile.Name.EndsWith(".iso.chd", StringComparison.OrdinalIgnoreCase) || dbChdFile.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase);
            
            bool ok;
            string destination = outDir.FullName;
            if (isDvd)
            {
                string isoOut = System.IO.Path.Combine(destination, Path.GetFileNameWithoutExtension(dbChdFile.Name));
                if (isoOut.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)) isoOut = isoOut.Substring(0, isoOut.Length - 4);
                if (!isoOut.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) isoOut += ".iso";
                ok = extractor.ExtractDvd(dbChdFile.FullName, isoOut, out error);
            }
            else
            {
                string cueOut = System.IO.Path.Combine(destination, "disc.cue");
                ok = extractor.ExtractCd(dbChdFile.FullName, cueOut, out error);
            }

            if (!ok)
            {
                Directory.Delete(destination);
                if (cacheDir.FindChild(outDir, out int idx))
                    cacheDir.ChildRemove(idx);
                return ReturnCode.FileSystemError;
            }

            // Scan the extracted files and add them to the DB and FileGroups
            string[] files = System.IO.Directory.GetFiles(destination);
            foreach (string file in files)
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(file);
                string name = fi.Name;

                // Find the corresponding FileCHD in dbChdFile's children
                RvFile thisFile = null;
                for (int j = 0; j < dbChdFile.ChildCount; j++)
                {
                    RvFile child = dbChdFile.Child(j);
                    if (child == null || child.FileType != FileType.FileCHD)
                        continue;
                    
                    // Match by name first
                    if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        thisFile = child;
                        break;
                    }
                }

                // If no name match, try to match by size (for DVD ISOs where name might differ)
                if (thisFile == null && isDvd && dbChdFile.ChildCount == 1)
                {
                    thisFile = dbChdFile.Child(0);
                }

                if (thisFile == null)
                    continue;

                RvFile outFile = new RvFile(FileType.File)
                {
                    Name = name,
                    Size = (ulong)fi.Length,
                    GotStatus = GotStatus.Got,
                    FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                    Parent = outDir,
                    CRC = thisFile.CRC,
                    SHA1 = thisFile.SHA1,
                    MD5 = thisFile.MD5,
                    AltSize = thisFile.AltSize,
                    AltCRC = thisFile.AltCRC,
                    AltSHA1 = thisFile.AltSHA1,
                    AltMD5 = thisFile.AltMD5,
                    FileGroup = thisFile.FileGroup
                };
                outFile.SetDatGotStatus(DatStatus.InToSort, GotStatus.Got);
                outFile.FileStatusSet(
                    FileStatus.SizeVerified | FileStatus.CRCVerified | FileStatus.SHA1Verified | FileStatus.MD5Verified
                );
                outFile.RepStatus = RepStatus.NeededForFix;

                outDir.ChildAdd(outFile);
                if (thisFile.FileGroup != null)
                {
                    thisFile.FileGroup.Files.Add(outFile);
                }

                if (filesUsedForFix != null)
                {
                    string fn = outFile.TreeFullName;
                    if (!filesUsedForFix.ContainsKey(fn))
                        filesUsedForFix.Add(fn, outFile);
                }
            }
            
            return ReturnCode.Good;
        }
    }
}
