using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Compress;
using Compress.gZip;
using Compress.SevenZip;
using Compress.ZipFile;
using FileHeaderReader;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.Files;
using RomVaultX.SupportedFiles.GZ;
using DirectoryInfo = RVIO.DirectoryInfo;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace RomVaultX
{
    public static class RomScanner
    {
        private const int Buffersize = 1024 * 1024;
        private static BackgroundWorker _bgw;

        public static string RootDir = @"ToSort";
        private static readonly byte[] Buffer = new byte[Buffersize];
        private static string _tmpDir = @"tmp";

        private static ulong _inMemorySize;
        public static bool DelFiles = true;


        public static void ScanFiles(object sender, DoWorkEventArgs e)
        {
            string sInMemorySize = AppSettings.ReadSetting("ScanInMemorySize");
            if (sInMemorySize == null)
            {
                // I use 1000000
                AppSettings.AddUpdateAppSettings("ScanInMemorySize", "1000000");
                sInMemorySize = AppSettings.ReadSetting("ScanInMemorySize");
            }

            if (!ulong.TryParse(sInMemorySize, out _inMemorySize))
            {
                _inMemorySize = 1000000;
            }

            _tmpDir = AppSettings.ReadSetting("ScanInDir") ?? "tmp";
            if (!Directory.Exists(_tmpDir))
            {
                Directory.CreateDirectory(_tmpDir);
            }

            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            ScanADirNew(RootDir);

            DatUpdate.UpdateGotTotal();
            _bgw?.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        private static bool ScanAFile(string realFilename, Stream memzip, string displayFilename)
        {
            Stream fStream;
            if (string.IsNullOrEmpty(realFilename) && memzip != null)
            {
                fStream = memzip;
            }
            else
            {
                int errorCode = FileStream.OpenFileRead(realFilename, out fStream);
                if (errorCode != 0)
                {
                    return false;
                }
            }

            bool ret = false;
            HeaderFileType foundFileType = FileHeaderReader.FileHeaderReader.GetType(fStream, out int offset);

            fStream.Position = 0;
            RvFile tFile = UnCompFiles.CheckSumRead(fStream, offset);
            tFile.AltType = foundFileType;


            if (foundFileType == HeaderFileType.CHD)
            {
                // read altheader values from CHD file.
            }

            // test if needed.
            FindStatus res = RvRomFileMatchup.FileneededTest(tFile);

            if (res == FindStatus.FileNeededInArchive)
            {
                _bgw?.ReportProgress(0, new bgwShowError(displayFilename, "found"));
                Debug.WriteLine("Reading file as " + tFile.SHA1);
                GZip gz = new GZip(tFile);
                string outfile = RomRootDir.Getfilename(tFile.SHA1);
                fStream.Position = 0;
                gz.WriteGZip(outfile, fStream, false);

                tFile.CompressedSize = gz.compressedSize;
                tFile.DBWrite();
                ret = true;
            }
            else if (res == FindStatus.FoundFileInArchive)
            {
                ret = true;
            }

            if (foundFileType == HeaderFileType.ZIP || foundFileType == HeaderFileType.SevenZip || foundFileType == HeaderFileType.GZ)
            {

                ICompress fz;
                switch (foundFileType)
                {
                    case HeaderFileType.SevenZip:
                        fz = new SevenZ();
                        break;
                    case HeaderFileType.GZ:
                        fz = new gZip();
                        break;

                    //case HeaderFileType.ZIP:
                    default:
                        fz = new ZipFile();
                        break;
                }

                fStream.Position = 0;

                ZipReturn zp;

                if (string.IsNullOrEmpty(realFilename) && memzip != null)
                {
                    zp = fz.ZipFileOpen(memzip);
                }
                else
                {
                    zp = fz.ZipFileOpen(realFilename);
                }

                if (zp == ZipReturn.ZipGood)
                {
                    bool allZipFound = true;
                    for (int i = 0; i < fz.LocalFilesCount(); i++)
                    {
                        ZipReturn openFile = fz.ZipFileOpenReadStream(i, out Stream stream, out ulong streamSize);

                        if (streamSize <= _inMemorySize)
                        {
                            if (openFile == ZipReturn.ZipTryingToAccessADirectory)
                                continue;
                            byte[] tmpFile = new byte[streamSize];
                            stream.Read(tmpFile, 0, (int)streamSize);
                            using (Stream memStream = new MemoryStream(tmpFile, false))
                            {
                                allZipFound &= ScanAFile(null, memStream, fz.Filename(i));
                            }
                        }
                        else
                        {
                            string file = Path.Combine(_tmpDir, Guid.NewGuid().ToString());
                            FileStream.OpenFileWrite(file, out Stream fs);
                            ulong sizetogo = streamSize;
                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                                stream.Read(Buffer, 0, sizenow);
                                fs.Write(Buffer, 0, sizenow);
                                sizetogo -= (ulong)sizenow;
                            }
                            fs.Close();

                            allZipFound &= ScanAFile(file, null, fz.Filename(i));

                            File.Delete(file);
                        }
                        //fz.ZipFileCloseReadStream();
                    }
                    fz.ZipFileClose();
                    ret |= allZipFound;
                }
                else
                {
                    ret = false;
                }
            }

            if (!string.IsNullOrEmpty(realFilename) || memzip == null)
            {
                fStream.Close();
                fStream.Dispose();
            }


            return ret;
        }

        private static void ScanADirNew(string directory)
        {
            _bgw.ReportProgress(0, new bgwText("Scanning Dir : " + directory));
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();

            _bgw.ReportProgress(0, new bgwRange2Visible(true));
            _bgw.ReportProgress(0, new bgwSetRange2(fi.Length));

            for (int j = 0; j < fi.Length; j++)
            {
                if (_bgw.CancellationPending)
                {
                    return;
                }

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new bgwValue2(j));
                _bgw.ReportProgress(0, new bgwText2(f.Name));

                bool fileFound = ScanAFile(f.FullName, null, f.Name);

                if (fileFound && DelFiles)
                {
                    File.SetAttributes(f.FullName, FileAttributes.Normal);
                    File.Delete(f.FullName);
                }
            }

            DirectoryInfo[] childdi = di.GetDirectories();
            foreach (DirectoryInfo d in childdi)
            {
                if (_bgw.CancellationPending)
                {
                    return;
                }
                ScanADirNew(d.FullName);
            }

            if (directory == "ToSort")
            {
                return;
            }
            if (IsDirectoryEmpty(directory))
            {
                System.IO.DirectoryInfo dii = new System.IO.DirectoryInfo(directory);
                dii.Attributes &= ~FileAttributes.ReadOnly;

                Directory.Delete(directory);
            }
        }


        private static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
    }
}