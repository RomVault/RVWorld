using System;
using System.IO;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.Utils;

namespace Tester
{
    public static class TestDatRV
    {
        public enum MergeType
        {
            None,
            Split,
            Merge,
            NonMerged,
            CHDsMerge
        }


        public enum FileType
        {
            Unknown,
            Dir,
            Zip,
            SevenZip,
            File,
            ZipFile,
            SevenZipFile
        }




        private static StreamWriter _file;

        private static void WriteLine(string message)
        {
            Console.WriteLine(message);
            _file.WriteLine(message);
        }

        private static void ReadError(string filename, string error)
        {
            WriteLine(filename + ": " + error);
        }



        public static void test()
        {

            string datFullName = @"D:\baddat.xml";
            _file = new StreamWriter(@"D:\timeout.txt");


            DatRead dr = new DatRead
            {
                ErrorReport = ReadError
            };
            dr.ReadDat(datFullName, out DatHeader dh);

            DatClean.CleanFilenames(dh.BaseDir);

            //DatClean.DirectoryExpand(dh.BaseDir);

            //DatClean.RemoveNonCHD(dh.BaseDir);
            DatClean.RemoveCHD(dh.BaseDir);

            DatClean.RemoveNoDumps(dh.BaseDir);

            SetMergeType(MergeType.Split, dh);

            //if (datRule.SingleArchive)
            //    DatClean.MakeDatSingleLevel(dh);

            SetCompressionMethod(FileType.Zip, dh);

            DatClean.DirectoryExpand(dh.BaseDir);




            _file.Close();
        }



        private static void SetMergeType(MergeType mt, DatHeader dh)
        {
            bool hasRom = DatHasRomOf.HasRomOf(dh.BaseDir);
            if (hasRom)
            {
                switch (mt)
                {
                    case MergeType.Merge:
                        DatClean.DatSetMakeMergeSet(dh.BaseDir);
                        break;
                    case MergeType.NonMerged:
                        DatClean.DatSetMakeNonMergeSet(dh.BaseDir);
                        break;
                    case MergeType.Split:
                        DatClean.DatSetMakeSplitSet(dh.BaseDir);
                        //DatClean.RemoveNotCollected(dh.BaseDir);
                        break;
                    case MergeType.CHDsMerge:
                        DatClean.DatSetMakeMergeSet(dh.BaseDir, false);
                        break;
                }



                DatClean.RemoveDupes(dh.BaseDir);
                DatClean.RemoveEmptySets(dh.BaseDir);
            }

        }



        private static void SetCompressionMethod(FileType ft, DatHeader dh)
        {
            switch (ft)
            {
                case FileType.Dir:
                    DatSetCompressionType.SetFile(dh.BaseDir);
                    return;
                case FileType.SevenZip:
                    DatSetCompressionType.SetZip(dh.BaseDir,true);
                    return;
                default:
                    DatSetCompressionType.SetZip(dh.BaseDir);
                    return;
            }
        }


    }
}
