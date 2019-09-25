using System;
using System.Diagnostics;
using System.IO;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.DatWriter;

namespace Tester
{
    static class MameDatTester
    {

        private static StreamWriter _file;
        private static void WriteLine()
        {
            Console.WriteLine();
            _file.WriteLine();
        }

        private static void WriteLine(string message)
        {
            Console.WriteLine(message);
            _file.WriteLine(message);
        }

        private static long? _lastTime;
        private static void WriteLine(string ver, string message)
        {
            long swr = Sw.ElapsedMilliseconds;
            string txt = ver + " , " + message + " , " + (double) swr / 1000 + " , " + (double) (swr - (_lastTime ?? 0)) / 1000;
            Console.WriteLine(txt);
            _file.WriteLine(txt);
            _lastTime = swr;
        }

        static readonly Stopwatch Sw = new Stopwatch();
        public static void Go()
        {
            _file = new StreamWriter(@"D:\timeout.txt");
            //ProcVer("0.194");
            //ProcVer("0.195");
            //ProcVer("0.196");
            ProcVer("0.201");
            _file.Close();
        }


        private static void ProcVer(string ver)
        {
            WriteLine();
            WriteLine();
            _lastTime = null;
            Sw.Reset();
            Sw.Start();
            DatRead dr = new DatRead
            {
                ErrorReport = ReadError
            };
            DatXMLWriter dxw = new DatXMLWriter();


            WriteLine(ver, "Reading BINDat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMS (from bin).xml", out DatHeader dh);
            dh.Name += " (merged)";
            dh.Description += " (merged)";

            WriteLine(ver, "Dat read");
            DatClean.RemoveNonCHD(dh.BaseDir);
            WriteLine(ver, "CHD removed");
            DatClean.RemoveNoDumps(dh.BaseDir);
            WriteLine(ver, "Removed No Dumps");

            DatClean.DatSetMakeMergeSet(dh.BaseDir,false);
            WriteLine(ver, "Made Merge Set");

            DatClean.RemoveDupes(dh.BaseDir);
            WriteLine(ver, "Removed Dupes");

            DatClean.RemoveEmptySets(dh.BaseDir);
            WriteLine(ver, "Removed Empty Sets");

            DatSetCompressionType.SetZip(dh.BaseDir);
            WriteLine(ver, "Set TorrentZip");
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " CHDs (merged-fromBin).xml", dh);

            WriteLine(ver, "Reading Dat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " CHDs (merged).xml", out dh);
            DatSetCompressionType.SetZip(dh.BaseDir);
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " CHDs (merged-sorted).xml", dh);

            WriteLine(ver, "Done Set 2");





            WriteLine(ver, "Reading BINDat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMS (from bin).xml", out dh);
            dh.Name += " (split)";
            dh.Description += " (split)";
            
            WriteLine(ver, "Dat read");
            DatClean.RemoveCHD(dh.BaseDir);
            WriteLine(ver, "CHD removed");
            DatClean.RemoveNoDumps(dh.BaseDir);
            WriteLine(ver, "Removed No Dumps");

            DatClean.DatSetMakeSplitSet(dh.BaseDir);
            WriteLine(ver, "Made Split Set");
            DatClean.RemoveNotCollected(dh.BaseDir);
            WriteLine(ver, "Removed Not Collected");


            DatClean.RemoveDupes(dh.BaseDir);
            WriteLine(ver, "Removed Dupes");

            DatClean.RemoveEmptySets(dh.BaseDir);
            WriteLine(ver, "Removed Empty Sets");

            DatSetCompressionType.SetZip(dh.BaseDir);
            WriteLine(ver, "Set TorrentZip");
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMS (split-fromBin).xml", dh);
            

            WriteLine(ver, "Reading Dat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMs (split).xml", out dh);
            DatSetCompressionType.SetZip(dh.BaseDir);
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMs (split-sorted).xml", dh);

            WriteLine(ver, "Done Set 1");


            WriteLine(ver, "Reading BINDat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMS (from bin).xml", out dh);
            dh.Name += " (merged)";
            dh.Description += " (merged)";

            WriteLine(ver, "Dat read");
            DatClean.RemoveCHD(dh.BaseDir);
            WriteLine(ver, "CHD removed");
            DatClean.RemoveNoDumps(dh.BaseDir);
            WriteLine(ver, "Removed No Dumps");

            DatClean.DatSetMakeMergeSet(dh.BaseDir);
            WriteLine(ver, "Made Merge Set");

            DatClean.RemoveDupes(dh.BaseDir);
            WriteLine(ver, "Removed Dupes");

            DatClean.RemoveEmptySets(dh.BaseDir);
            WriteLine(ver, "Removed Empty Sets");

            DatSetCompressionType.SetZip(dh.BaseDir);
            WriteLine(ver, "Set TorrentZip");
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMS (merged-fromBin).xml", dh);

            WriteLine(ver, "Reading Dat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMs (merged).xml", out dh);
            DatSetCompressionType.SetZip(dh.BaseDir);
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMs (merged-sorted).xml", dh);

            WriteLine(ver, "Done Set 2");
            



            WriteLine(ver, "Reading BINDat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMS (from bin).xml", out dh);
            dh.Name += " (non-merged)";
            dh.Description+= " (non-merged)";


            WriteLine(ver, "Dat read");
            DatClean.RemoveCHD(dh.BaseDir);
            WriteLine(ver, "CHD removed");
            DatClean.RemoveNoDumps(dh.BaseDir);
            WriteLine(ver, "Removed No Dumps");

            DatClean.DatSetMakeNonMergeSet(dh.BaseDir);
            WriteLine(ver, "Made Merge Set");
            DatClean.RemoveDupes(dh.BaseDir);
            WriteLine(ver, "Removed Dupes");

            DatClean.RemoveEmptySets(dh.BaseDir);
            WriteLine(ver, "Removed Empty Sets");

            DatSetCompressionType.SetZip(dh.BaseDir);
            WriteLine(ver, "Set TorrentZip");
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMS (non-merged-fromBin).xml", dh);

            WriteLine(ver, "Reading Dat Set");
            dr.ReadDat(@"TestDATs\MAME " + ver + " ROMs (non-merged).xml", out dh);
            DatSetCompressionType.SetZip(dh.BaseDir);
            dxw.WriteDat(@"TestDATs\out\MAME " + ver + " ROMs (non-merged-sorted).xml", dh);

            WriteLine(ver, "Done Set 3");



        }


        private static void ReadError(string filename, string error)
        {
            WriteLine(filename + ": " + error);
        }

    }
}
