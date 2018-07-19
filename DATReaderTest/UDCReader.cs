using System;
using System.Diagnostics;
using DATReader;
using DATReader.DatClean;
using DATReader.DatStore;
using RVIO;

namespace Tester
{
    class UDCReader
    {
        public static void Go()
        {
            readerDir(@"D:\UDC\Dat1 (UDC)");
            readerDir(@"D:\UDC\Dat2 (OBS)");
        }

        private static void readerDir(string dirName)
        {
            DirectoryInfo di = new DirectoryInfo(dirName);

            FileInfo[] aFI = di.GetFiles();

            foreach (FileInfo f in aFI)
            {
                string ext = Path.GetExtension(f.Name).ToLower();

                if (ext == ".ini" || ext == ".txt")
                    continue;

                if (ext != ".dat" && ext != ".xml")
                    continue;

                DatRead dr = new DatRead
                {
                    ErrorReport = ReadError
                };
                DatHeader dh;
                dr.ReadDat(f.FullName, out dh);
                if (dh != null)
                {
                    DatTypeTester r = new DatTypeTester();
                    r.ProcessDat(dh);

                    string header = dh.Header ?? "";
                    string compression = dh.Compression ?? "";
                    string mergeType = dh.MergeType ?? "";
                    string noDump = dh.NoDump ?? "";
                    string dir = dh.Dir ?? "";

                    string outtxt = header + "," + compression + "," + mergeType + "," + noDump + "," + dir + "," + dh.IsSuperDat + "," + dh.NotZipped;
                    if (outtxt != ",,,,,False,False" || r.Found())
                    {
                        Console.WriteLine(outtxt + "," + r.toString() + "," + f.FullName);

                        Debug.WriteLine(outtxt + "," + r.toString() + "," + f.FullName);
                    }
                    /*
                    if (!string.IsNullOrWhiteSpace(dh.Header)) Console.WriteLine("Header : " + dh.Header);
                    if (!string.IsNullOrWhiteSpace(dh.Compression)) Console.WriteLine("Compression : " + dh.Compression);
                    if (!string.IsNullOrWhiteSpace(dh.MergeType)) Console.WriteLine("MergeType : " + dh.MergeType);
                    if (!string.IsNullOrWhiteSpace(dh.NoDump)) Console.WriteLine("NoDump : " + dh.NoDump);
                    if (!string.IsNullOrWhiteSpace(dh.Dir)) Console.WriteLine("Dir : " + dh.Dir);
                    if (dh.IsSuperDat) Console.WriteLine("IsSuperDat = true");
                    if (dh.NotZipped) Console.WriteLine("NotZipped = true");
                    */

                    /*
                    string outpath = @"D:\out\" + f.FullName.Substring(3);

                    if (outpath.Length > 260)
                    {
                    //    Console.WriteLine("Long filename " + outpath.Length);
                    //    Console.WriteLine(outpath);
                    }

                    string outDir = Path.GetDirectoryName(outpath);
                    DatXMLWriter dxw = new DatXMLWriter();

                    if (!Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);
                    dxw.WriteDat(outpath, dh);
                    */
                }
            }

            DirectoryInfo[] adi = di.GetDirectories();

            foreach (DirectoryInfo d in adi)
            {
                readerDir(d.FullName);
            }
        }


        private static void ReadError(string filename, string error)
        {
            //Console.WriteLine(filename + ": " + error);
            System.Diagnostics.Debug.WriteLine(error);
            //string strCmdText = filename;
            //System.Diagnostics.Process.Start(@"C:\Program Files\Notepad++\Notepad++.exe", strCmdText);
        }

    }
}
