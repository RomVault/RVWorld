using DATReader.DatStore;
using FileScanner;
using RomVaultCore.RvDB;
using RomVaultCore.Storage.Dat;
using SortMethods;
using System;

namespace RomVaultCore
{
    internal static class RVSorters
    {

        /// <summary>
        /// Used in DatImportDir
        /// --------------------
        /// Used in BinarySearch, adding DatRoot dirs into the DatImportDir structure
        /// this will later be merged into the main DB structure
        /// </summary>
        /// <param name="var1"></param>
        /// <param name="var2"></param>
        /// <returns></returns>

        internal static int CompareDirName(DatImportDir var1, DatImportDir var2)
        {
            // this is used while building the DatImportDir list and so
            // believe this should be replaced with a dir case sensitive compare
            return Sorters.DirectoryNameCompareCase(var1.Name, var2.Name);
        }

        /// <summary>
        /// Used in:
        /// DatUpdate/RemovedOldDats
        /// DatUpdate/UpdateDirs
        /// --------------------
        /// Used to compare the dir in DatImportDir and the dir in the main DB structure
        /// </summary>
        /// <param name="var1"></param>
        /// <param name="var2"></param>
        /// <returns></returns>

        internal static int CompareDirName(RvFile var1, DatImportDir var2)
        {
            // this is used while merging the DatImportDir list into the main DB structure and so
            // believe this should be replaced with a dir case insensitive compare
            return Sorters.DirectoryNameCompare(var1.Name, var2.Name);
        }


        /// <summary>
        /// Used in DatImportDir
        /// --------------------
        /// Used in BinarySearch, adding DATs into the importDatFiles struncture
        /// this will later be merged into the DAT lists in the main DB struncture
        /// </summary>
        /// <param name="var1"></param>
        /// <param name="var2"></param>
        /// <returns></returns>

        internal static int CompareDatName(DatImportDat var1, DatImportDat var2)
        {
            return CompareDatName(var1.DatFullName, var2.DatFullName);
        }
        internal static int CompareDatName(RvDat var1, DatImportDat var2)
        {
            return CompareDatName(var1.GetData(RvDat.DatData.DatRootFullName), var2.DatFullName);
        }
        internal static int CompareDatName(RvDat var1, RvDat var2)
        {
            return CompareDatName(var1.GetData(RvDat.DatData.DatRootFullName), var2.GetData(RvDat.DatData.DatRootFullName));
        }

        private static int CompareDatName(string var1, string var2)
        {
            return Math.Sign(string.Compare(var1, var2, StringComparison.CurrentCultureIgnoreCase));
        }



        public static int CompareName(ScannedFile var1,ScannedFile var2)
        {
            return CompareName(var1.FileType, var1.Name, var2.FileType, var2.Name);
        }

        public static int CompareName(RvFile var1, DatBase var2)
        {
            return CompareName(var1.FileType, var1.Name, var2.FileType, var2.Name);
        }

        public static int CompareName(RvFile var1, ScannedFile var2)
        {
            return CompareName(var1.FileType, var1.Name, var2.FileType, var2.Name);
        }

        public static int CompareName(RvFile var1, RvFile var2)
        {
            return CompareName(var1.FileType, var1.Name, var2.FileType, var2.Name);
        }

        private static int CompareName(FileType f1, string name1, FileType f2, string name2)
        {
            int res;
            if (f1 == FileType.FileZip || f2 == FileType.FileZip)
            {
                if (f1 != f2)
                {
                    ReportError.SendAndShow("Incompatible Compare type");
                }
                return Sorters.TrrntZipStringCompareCase(name1, name2);
            }
            if (f1 == FileType.FileSevenZip || f2 == FileType.FileSevenZip)
            {
                if (f1 != f2)
                {
                    ReportError.SendAndShow("Incompatible Compare type");
                }
                return Sorters.Trrnt7ZipStringCompare(name1, name2);
            }

            res = Sorters.DirectoryNameCompare(name1, name2);
            if (res != 0)
                return res;

#if ZipFile
            FileType f2Test = f2;
            if (f1 == FileType.File && f2 == FileType.Zip)
                f2Test= FileType.File;
#endif
            return f1.CompareTo(f2);
        }

    }
}
