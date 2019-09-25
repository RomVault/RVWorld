using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Compress;
using Compress.gZip;
using FileHeaderReader;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;
using RVIO;

namespace RomVaultX
{
    public static class romRootScanner
    {
        private static bool deep;
        private static BackgroundWorker _bgw;

        public static void ScanFiles(object sender, DoWorkEventArgs e)
        {
            deep = false;
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            for (int i = 0; i < 256; i++)
                ScanRomRoot(RomRootDir.GetRootDir((byte)i));

            DatUpdate.UpdateGotTotal();
            _bgw.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }


        public static void ScanFilesDeep(object sender, DoWorkEventArgs e)
        {
            deep = true;
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            for (int i = 0; i < 256; i++)
            {
                ScanRomRoot(RomRootDir.GetRootDir((byte) i));
                if (_bgw.CancellationPending)
                    break;
            }

            DatUpdate.UpdateGotTotal();
            _bgw.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
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
                    GZip gZipTest = new GZip();
                    ZipReturn errorcode = gZipTest.ReadGZip(f.FullName, false);
                    gZipTest.sha1Hash = VarFix.CleanMD5SHA1(Path.GetFileNameWithoutExtension(f.Name), 40);
                    gZipTest.Close();

                    if (errorcode != ZipReturn.ZipGood)
                    {
                        _bgw.ReportProgress(0, new bgwShowError(f.FullName, "gz File corrupt"));
                        if (!Directory.Exists("corrupt"))
                            Directory.CreateDirectory("corrupt");
                        File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                        continue;
                    }
                    RvFile tFile = new RvFile();
                    tFile.CRC = gZipTest.crc;
                    tFile.MD5 = gZipTest.md5Hash;
                    tFile.SHA1 = gZipTest.sha1Hash;
                    tFile.Size = gZipTest.uncompressedSize;
                    tFile.AltType =(HeaderFileType) gZipTest.altType;
                    tFile.AltCRC = gZipTest.altcrc;
                    tFile.AltMD5 = gZipTest.altmd5Hash;
                    tFile.AltSHA1 = gZipTest.altsha1Hash;
                    tFile.AltSize = gZipTest.uncompressedAltSize;
                    tFile.CompressedSize = gZipTest.compressedSize;

                    FindStatus res = fileneededTest(tFile);

                    if (res != FindStatus.FoundFileInArchive)
                    {
                        if (deep)
                        {

                            gZipTest = new GZip();

                            try
                            {
                                errorcode = gZipTest.ReadGZip(f.FullName, true);
                                gZipTest.sha1Hash = VarFix.CleanMD5SHA1(Path.GetFileNameWithoutExtension(f.Name), 40);
                                gZipTest.Close();
                            }
                            catch (Exception e)
                            {
                                gZipTest.Close();
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
                                File.Move(f.FullName,Path.Combine("corrupt",f.Name));
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