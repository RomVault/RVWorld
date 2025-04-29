using DATReader.DatStore;
using DATReader.DatWriter;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RomVaultCore
{
    public static class FixDatReport
    {

        public static void RecursiveDatTree(string outDirectory, RvFile tDir, bool redOnly)
        {
            // cannot process from a file.
            if (!tDir.IsDirectory)
                return;

            if (tDir.Dat != null)
            {
                ExtractDat(outDirectory, tDir.Dat, tDir, redOnly);
                return;
            }

            if (tDir.DirDatCount > 0)
            {
                Debug.WriteLine($"Dats found in {tDir.FullName}");
                for (int i = 0; i < tDir.DirDatCount; i++)
                {
                    RvDat rvDat = tDir.DirDat(i);
                    Debug.WriteLine($"  {i} {rvDat.GetData(RvDat.DatData.DatName)}");

                    ExtractDat(outDirectory, rvDat, tDir, redOnly);
                }
            }

            for (int i = 0; i < tDir.ChildCount; i++)
            {
                RvFile child = tDir.Child(i);
                if (child.IsDirectory && child.Dat == null)
                    RecursiveDatTree(outDirectory, child, redOnly);
            }
        }


        public static void ExtractDat(string outDirectory, RvDat rvDat, RvFile tDir, bool redOnly)
        {
            RvFile outDir = new RvFile(FileType.Dir);
            outDir.DirDatAdd(rvDat);

            RecursiveDatTreeFindingDat(rvDat, tDir, outDir, redOnly);
            // added outDir.Child(0).Game ==null to fix a big if there is only one missing game in a fixdat
            if (rvDat.Flag(DatFlags.AutoAddedDirectory) && outDir.ChildCount == 1 && outDir.Child(0).Game == null)
                outDir = outDir.Child(0);

            if (outDir.ChildCount == 0)
                return;

            FixSingleLevelDat(outDir);

            DatHeader dh = (new ExternalDatConverterTo()).ConvertToExternalDat(outDir);
            DATReader.DatClean.DatClean.ArchiveDirectoryFlattern(dh.BaseDir);
            DATReader.DatClean.DatClean.RemoveUnNeededDirectories(dh.BaseDir);

            dh.Name = "FixDat_" + dh.Name;
            dh.Description = "FixDat_" + dh.Description;
            dh.Author = "RomVault";
            dh.Date = DateTime.Now.ToString("yyyy-MM-dd");

            int test = 0;
            string datFullName = rvDat.GetData(RvDat.DatData.DatRootFullName);
            string datDir = Path.GetDirectoryName(datFullName.Substring(8)).Replace("\\", "_");
            string datName = Path.GetFileNameWithoutExtension(datFullName);
            string datFilename = Path.Combine(outDirectory, $"fixDat_{datDir}_{datName}.dat");
            while (File.Exists(datFilename))
            {
                test++;
                datFilename = Path.Combine(outDirectory, $"fixDat_{datDir}_{datName}({test}).dat");
            }


            DatXMLWriter.WriteDat(datFilename, dh);
        }

        private static int RecursiveDatTreeFindingDat(RvDat rvDat, RvFile tDir, RvFile outDir, bool redOnly)
        {
            int found = 0;
            for (int i = 0; i < tDir.ChildCount; i++)
            {
                RvFile child = tDir.Child(i);
                if (child.Dat != rvDat)
                    continue;

                if (child.IsDirectory)
                {
                    RvFile tCopy = new RvFile(child.FileType);
                    child.CopyTo(tCopy);
                    tCopy.Game = child.Game;
                    int ret = RecursiveDatTreeFindingDat(rvDat, child, tCopy, redOnly);
                    found += ret;
                    if (ret > 0)
                        outDir.ChildAdd(tCopy);
                    continue;
                }

                //child.isFile
                if ((child.DatStatus == DatStatus.InDatCollect || child.DatStatus == DatStatus.InDatMIA) &&
                     child.GotStatus != GotStatus.Got && (!redOnly || !(child.RepStatus == RepStatus.CanBeFixed || child.RepStatus == RepStatus.CanBeFixedMIA || child.RepStatus == RepStatus.CorruptCanBeFixed)))
                {
                    RvFile tCopy = new RvFile(child.FileType);
                    child.CopyTo(tCopy);
                    outDir.ChildAdd(tCopy);
                    found++;
                }
            }
            return found;
        }

        private static void FixSingleLevelDat(RvFile tDir)
        {
            List<RvFile> filesToFix = new List<RvFile>();
            for (int i = 0; i < tDir.ChildCount; i++)
            {
                RvFile child = tDir.Child(i);
                if (child.Game != null)
                    continue;
                if (child.IsDirectory)
                {
                    FixSingleLevelDat(child);
                    continue;
                }
                RvFile tCopy = new RvFile(child.FileType);
                child.CopyTo(tCopy);

                filesToFix.Add(tCopy);
                tDir.ChildRemove(i);
                i--;
            }
            if (filesToFix.Count == 0)
                return;
            foreach (RvFile file in filesToFix)
            {
                RvFile newParent = new RvFile(FileType.Dir);
                newParent.Name = Path.GetFileNameWithoutExtension(file.Name);
                int found = tDir.ChildNameSearch(newParent, out int index);
                if (found != 0)
                {
                    RvGame tGame = new RvGame(newParent.Name);
                    newParent.Game = tGame;
                    tDir.ChildAdd(newParent, index);
                }
                else
                    newParent = tDir.Child(index);

                newParent.ChildAdd(file);
            }
        }
    }
}