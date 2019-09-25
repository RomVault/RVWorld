using System;
using System.Collections.Generic;
using Compress;
using Compress.SevenZip;
using Compress.ZipFile;
using DATReader.DatClean;
using DATReader.DatStore;
using DATReader.DatWriter;
using FileHeaderReader;
using RVIO;

namespace Dir2Dat
{
    class Program
    {
        static void Main(string[] args)
        {
            string dirSource = @"\\10.0.4.11\t$\Downloads\eXoDOS V4";
            DatHeader ThisDat = new DatHeader()
            {
                BaseDir = new DatDir(DatFileType.Dir)
            };
            DirectoryInfo di = new DirectoryInfo(dirSource);
            ProcessDir(di, ThisDat.BaseDir, false);

            DatXMLWriter dWriter = new DatXMLWriter();
            dWriter.WriteDat(@"D:\out.dat", ThisDat, false);
        }


        private static void ProcessDir(DirectoryInfo di, DatDir thisDir, bool newStyle)
        {
            DirectoryInfo[] dia = di.GetDirectories();
            foreach (DirectoryInfo d in dia)
            {
                DatDir nextDir = new DatDir(DatFileType.Dir) { Name = d.Name };
                thisDir.ChildAdd(nextDir);
                ProcessDir(d, nextDir, newStyle);

            }
            FileInfo[] fia = di.GetFiles();

            int fCount = 0;
            foreach (FileInfo f in fia)
            {
                Console.WriteLine(f.FullName);
                string ext = Path.GetExtension(f.Name).ToLower();

                switch (ext)
                {
                    case ".zip":
                        AddZip(f, thisDir);
                        break;
                    case ".7z":
                        Add7Zip(f, thisDir);
                        break;
                    default:
                        if (newStyle)
                            AddFile(f, thisDir);
                        break;
                }

                //fCount++;
                if (fCount > 10)
                    break;
            }
        }

        private static void AddZip(FileInfo f, DatDir thisDir)
        {

            ZipFile zf1 = new ZipFile();
            zf1.ZipFileOpen(f.FullName, -1, true);
            zf1.ZipStatus = ZipStatus.TrrntZip;

            DatDir ZipDir = new DatDir(zf1.ZipStatus == ZipStatus.TrrntZip ? DatFileType.DirTorrentZip : DatFileType.DirRVZip)
            {
                Name = Path.GetFileNameWithoutExtension(f.Name),
                DGame = new DatGame()
            };
            ZipDir.DGame.Description = ZipDir.Name;
            thisDir.ChildAdd(ZipDir);



            FileScan fs = new FileScan();
            List<FileScan.FileResults> fr = fs.Scan(zf1, true, true);
            bool isTorrentZipDate = true;
            for (int i = 0; i < fr.Count; i++)
            {
                if (fr[i].FileStatus != Compress.ZipReturn.ZipGood)
                {
                    Console.WriteLine("File Error :" + zf1.Filename(i) + " : " + fr[i].FileStatus);
                    continue;
                }

                DatFile df = new DatFile(DatFileType.FileTorrentZip)
                {
                    Name = zf1.Filename(i),
                    Size = fr[i].Size,
                    CRC = fr[i].CRC,
                    SHA1 = fr[i].SHA1,
                    Date = zf1.LastModified(i).ToString("yyyy/MM/dd HH:mm:ss")
                    //df.MD5 = zf.MD5(i)
                };
                if (zf1.LastModified(i).Ticks != 629870671200000000)
                    isTorrentZipDate = false;

                ZipDir.ChildAdd(df);
            }
            zf1.ZipFileClose();
            if (isTorrentZipDate && ZipDir.DatFileType == DatFileType.DirRVZip)
                ZipDir.DatFileType = DatFileType.DirTorrentZip;

            if (ZipDir.DatFileType == DatFileType.DirTorrentZip)
            {
                DatSetCompressionType.SetZip(ZipDir);
                DatClean.RemoveUnNeededDirectoriesFromZip(ZipDir);
            }
        }

        private static void Add7Zip(FileInfo f, DatDir thisDir)
        {
            DatDir ZipDir = new DatDir(DatFileType.Dir7Zip)
            {
                Name = Path.GetFileNameWithoutExtension(f.Name),
                DGame = new DatGame()
            };
            ZipDir.DGame.Description = ZipDir.Name;
            thisDir.ChildAdd(ZipDir);

            SevenZ zf1 = new SevenZ();
            zf1.ZipFileOpen(f.FullName, -1, true);
            FileScan fs = new FileScan();
            List<FileScan.FileResults> fr = fs.Scan(zf1, true, true);
            for (int i = 0; i < fr.Count; i++)
            {
                if (zf1.IsDirectory(i))
                    continue;
                DatFile df = new DatFile(DatFileType.File7Zip)
                {
                    Name = zf1.Filename(i),
                    Size = fr[i].Size,
                    CRC = fr[i].CRC,
                    SHA1 = fr[i].SHA1
                    //df.MD5 = zf.MD5(i)
                };
                ZipDir.ChildAdd(df);
            }
            zf1.ZipFileClose();
        }

        private static void AddFile(FileInfo F, DatDir thisDir)
        {

            DatFile df = new DatFile(DatFileType.FileTorrentZip)
            {
                Name = F.Name,
                Size = (ulong)F.Length
            };

            thisDir.ChildAdd(df);
        }
    }
}
