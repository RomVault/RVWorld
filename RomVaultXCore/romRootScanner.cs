using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Compress;
using Compress.gZip;
using FileHeaderReader;
using RVXCore.DB;
using Directory = RVIO.Directory;
using DirectoryInfo = RVIO.DirectoryInfo;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;
using Path = RVIO.Path;

namespace RVXCore
{
    public static class romRootScanner
    {
        private static bool deep;
        private static BackgroundWorker _bgw;

        public static void ScanFiles(object sender, DoWorkEventArgs e)
        {
            deep = false;
            _bgw = sender as BackgroundWorker;

            for (int i = 0; i < 256; i++)
                ScanRomRoot(RomRootDir.GetRootDir((byte)i));

            DatUpdate.UpdateGotTotal();
            _bgw.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
        }


        public static void ScanFilesDeep(object sender, DoWorkEventArgs e)
        {
            deep = true;
            _bgw = sender as BackgroundWorker;

            for (int i = 0; i < 256; i++)
            {
                ScanRomRoot(RomRootDir.GetRootDir((byte)i));
                if (_bgw.CancellationPending)
                    break;
            }

            DatUpdate.UpdateGotTotal();
            _bgw.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
        }


        private static void ScanRomRoot(string directory)
        {
            _bgw.ReportProgress(0, new bgwText("Scanning Dir : " + directory));
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();

            _bgw.ReportProgress(0, new bgwRange2Visible(true));
            _bgw.ReportProgress(0, new bgwSetRange2(fi.Count()));

            for (int j = 0; j < fi.Count(); j++)
            {
                if (_bgw.CancellationPending)
                {
                    return;
                }

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new bgwValue2(j));
                _bgw.ReportProgress(0, new bgwText2(f.Name));
                string ext = Path.GetExtension(f.Name);

                if (ext.ToLower() == ".gz")
                {
                    gZip gZipTest = new gZip();
                    ZipReturn errorcode = gZipTest.ZipFileOpen(f.FullName);
                    if (errorcode != ZipReturn.ZipGood)
                    {
                        _bgw.ReportProgress(0, new bgwShowError(f.FullName, "gz File corrupt"));
                        if (!Directory.Exists("corrupt"))
                            Directory.CreateDirectory("corrupt");
                        File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                        continue;
                    }

                    RvFile tFile = gZipExtraData.fromGZip(f.FullName, gZipTest.ExtraData, gZipTest.CompressedSize);
                    gZipTest.ZipFileClose();

                    FindStatus res = fileneededTest(tFile);

                    if (res != FindStatus.FoundFileInArchive)
                    {
                        if (deep)
                        {

                            gZipTest = new gZip();

                            try
                            {
                                errorcode = gZipTest.ZipFileOpen(f.FullName);
                                if (errorcode == ZipReturn.ZipGood)
                                {
                                    FileScan fs = new FileScan();
                                    List<FileScan.FileResults> gRes = fs.Scan(gZipTest, true, true);
                                    errorcode = gRes[0].FileStatus;
                                    gZipTest.ZipFileClose();
                                }
                            }
                            catch
                            {
                                gZipTest.ZipFileClose();
                                _bgw.ReportProgress(0, new bgwShowError(f.FullName, "gz Crashed Compression"));
                                if (!Directory.Exists("corrupt"))
                                    Directory.CreateDirectory("corrupt");
                                File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                                continue;
                            }

                            if (errorcode != ZipReturn.ZipGood)
                            {
                                _bgw.ReportProgress(0, new bgwShowError(f.FullName, "gz File corrupt"));
                                if (!Directory.Exists("corrupt"))
                                    Directory.CreateDirectory("corrupt");
                                File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                                continue;
                            }
                        }
                        tFile.DBWrite();
                    }
                }
                if (_bgw.CancellationPending)
                {
                    return;
                }
            }

            DirectoryInfo[] childdi = di.GetDirectories();
            foreach (DirectoryInfo d in childdi)
            {
                if (_bgw.CancellationPending)
                {
                    return;
                }
                ScanRomRoot(d.FullName);
            }
        }

        private static FindStatus fileneededTest(RvFile tFile)
        {
            // first check to see if we already have it in the file table
            bool inFileDB = RvRomFileMatchup.FindInFiles(tFile); // returns true if found in File table
            return inFileDB ? FindStatus.FoundFileInArchive : FindStatus.FileNeededInArchive;
        }

        private enum FindStatus
        {
            FileUnknown,
            FoundFileInArchive,
            FileNeededInArchive
        }
    }
}