using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Compress;
using Compress.gZip;
using Compress.SevenZip;
using Compress.ZipFile;
using FileHeaderReader;
using RVXCore.DB;
using RVXCore.Util;
using DirectoryInfo = RVIO.DirectoryInfo;
using FileInfo = RVIO.FileInfo;
using FileStream = RVIO.FileStream;

namespace RVXCore
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
            string sInMemorySize = Settings.ScanInMemorySize;

            if (!ulong.TryParse(sInMemorySize, out _inMemorySize))
            {
                _inMemorySize = 1000000;
            }

            _tmpDir = Settings.ScanInDir ?? "tmp";
            if (!Directory.Exists(_tmpDir))
            {
                Directory.CreateDirectory(_tmpDir);
            }

            _bgw = sender as BackgroundWorker;

            ScanADirNew(RootDir);

            DatUpdate.UpdateGotTotal();
            _bgw?.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
        }

        private static bool ScanAFile(string realFilename, Stream memzip, string displayFilename)
        {
            Compress.File.File fStream = new Compress.File.File();
            if (string.IsNullOrEmpty(realFilename) && memzip != null)
            {
                fStream.ZipFileOpen(memzip);
            }
            else
            {
                ZipReturn zRet = fStream.ZipFileOpen(realFilename, -1, true);
                if (zRet != ZipReturn.ZipGood)
                {
                    return false;
                }
            }

            bool ret = false;
            FileScan fScan = new FileScan();
            List<FileScan.FileResults> resScan = fScan.Scan(fStream, true, true);

            HeaderFileType foundFileType = resScan[0].HeaderFileType;
            if (foundFileType == HeaderFileType.CHD)
            {
                // read altheader values from CHD file.
            }

            RvFile tFile = new RvFile
            {
                Size = resScan[0].Size,
                CRC = resScan[0].CRC,
                MD5 = resScan[0].MD5,
                SHA1 = resScan[0].SHA1,
                AltType = resScan[0].HeaderFileType,
                AltSize = resScan[0].AltSize,
                AltCRC = resScan[0].AltCRC,
                AltMD5 = resScan[0].AltMD5,
                AltSHA1 = resScan[0].AltSHA1
            };


            // test if needed.
            FindStatus res = RvRomFileMatchup.FileneededTest(tFile);

            if (res == FindStatus.FileNeededInArchive)
            {
                _bgw?.ReportProgress(0, new bgwShowError(displayFilename, "found"));
                Debug.WriteLine("Reading file as " + tFile.SHA1);
                string outfile = RomRootDir.Getfilename(tFile.SHA1);

                gZip gz1 = new gZip();
                gz1.ZipFileCreate(outfile);
                gz1.ExtraData = gZipExtraData.SetExtraData(tFile);
                gz1.ZipFileOpenWriteStream(false, true, "", tFile.Size, 8, out Stream write, null);

                fStream.ZipFileOpenReadStream(0, out Stream s, out ulong _);
                // do copy
                StreamCopier.StreamCopy(s, write, tFile.Size);

                fStream.ZipFileCloseReadStream();
                fStream.ZipFileClose();

                gz1.ZipFileCloseWriteStream(tFile.CRC);
                tFile.CompressedSize = gz1.CompressedSize;
                gz1.ZipFileClose();


                tFile.DBWrite();
                ret = true;
            }
            else if (res == FindStatus.FoundFileInArchive)
            {
                ret = true;
            }
            fStream.ZipFileClose();

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
                        fz = new Zip();
                        break;
                }

                ZipReturn zp;

                if (string.IsNullOrEmpty(realFilename) && memzip != null)
                {
                    memzip.Position = 0;
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
                        LocalFile lf = fz.GetLocalFile(i);
                        ZipReturn openFile = fz.ZipFileOpenReadStream(i, out Stream stream, out ulong streamSize);

                        if (streamSize <= _inMemorySize)
                        {
                            if (openFile == ZipReturn.ZipTryingToAccessADirectory)
                                continue;
                            byte[] tmpFile = new byte[streamSize];
                            stream.Read(tmpFile, 0, (int)streamSize);
                            using (Stream memStream = new MemoryStream(tmpFile, false))
                            {
                                allZipFound &= ScanAFile(null, memStream, lf.Filename);
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

                            allZipFound &= ScanAFile(file, null, lf.Filename);

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
                _bgw.ReportProgress(0, new bgwValue2(j + 1));
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