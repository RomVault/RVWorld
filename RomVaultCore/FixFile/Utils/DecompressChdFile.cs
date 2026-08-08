using System;
using System.IO;
using System.Collections.Generic;
using FileScanner;
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
    public static class DecompressChdFile
    {
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

            string destination = outDir.FullName;
            List<RvFile> expectedMembers = new List<RvFile>();
            bool isDvd = false;
            for (int i = 0; i < dbChdFile.ChildCount; i++)
            {
                RvFile child = dbChdFile.Child(i);
                if (child == null || child.FileType != FileType.FileCHD)
                    continue;
                expectedMembers.Add(child);
                if (child.Name?.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) == true)
                    isDvd = true;
            }

            int exportResult = ChdExport.Export(dbChdFile.FullName, destination, expectedMembers, out error);
            if (exportResult != 0)
            {
                try { System.IO.Directory.Delete(destination, true); } catch { }
                if (cacheDir.FindChild(outDir, out int idx))
                    cacheDir.ChildRemove(idx);
                return ReturnCode.DestinationCheckSumMismatch;
            }

            // Scan the verified exported files and add their actual hashes to the DB and FileGroups.
            FileScan fileScanner = new FileScan();
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

                ScannedFile scanned = new ScannedFile(FileType.File)
                {
                    Name = name,
                    FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                    GotStatus = GotStatus.Got,
                    DeepScanned = true,
                    Size = (ulong)fi.Length
                };
                using (Stream stream = System.IO.File.OpenRead(file))
                    fileScanner.CheckSumRead(stream, scanned, (ulong)fi.Length, true, false, null, 0, 0);

                RvFile outFile = new RvFile(FileType.File)
                {
                    Name = name,
                    Size = (ulong)fi.Length,
                    GotStatus = GotStatus.Got,
                    FileModTimeStamp = fi.LastWriteTime.ToFileTimeUtc(),
                    Parent = outDir,
                    CRC = scanned.CRC,
                    SHA1 = scanned.SHA1,
                    MD5 = scanned.MD5,
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
