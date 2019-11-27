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
            go(@"Y:\MAME 0.184 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.184");
            go(@"Y:\MAME 0.185 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.185");
            go(@"Y:\MAME 0.186 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.186");
            go(@"Y:\MAME 0.187 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.187");
            go(@"Y:\MAME 0.194 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.194");
            go(@"Y:\MAME 0.199 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.199");
            go(@"Y:\MAME 0.200 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.200");
            go(@"Y:\MAME 0.201 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.201");
            go(@"Y:\MAME 0.202 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.202");
            go(@"Y:\MAME 0.205 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.205");
            go(@"Y:\MAME 0.206 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.206");
            go(@"Y:\MAME 0.207 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.207");
            go(@"Y:\MAME 0.208 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.208");
            go(@"Y:\MAME 0.209 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.209");
            go(@"Y:\MAME 0.212 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.212");
            go(@"Y:\MAME 0.213 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.213");
            go(@"Y:\MAME 0.214 EXTRAs", @"D:\ExtraDatOut\Mame_Extra_0.214");

        }

        private static void go(string dirSource,string outfile)
        {
            DatHeader ThisDat = new DatHeader()
            {
                BaseDir = new DatDir(DatFileType.Dir)
            };
            DirectoryInfo di = new DirectoryInfo(dirSource);
            ProcessDir(di, ThisDat.BaseDir, false);

            DatXMLWriter dWriter = new DatXMLWriter();
            dWriter.WriteDat(outfile+"_old.dat", ThisDat, false);
            dWriter.WriteDat(outfile+"_new.dat", ThisDat, true);

        }


        private static void ProcessDir(DirectoryInfo di, DatDir thisDir, bool newStyle)
        {
            DirectoryInfo[] dia = di.GetDirectories();
            foreach (DirectoryInfo d in dia)
            {
                bool procAsGame = CheckAddDir(d);
                if (procAsGame)
                {
                    Console.WriteLine(d.FullName + "\\ need to add as game");
                    AddDirAsGame(d, thisDir);
                }
                else
                {
                    DatDir nextDir = new DatDir(DatFileType.Dir) { Name = d.Name };
                    thisDir.ChildAdd(nextDir);
                    ProcessDir(d, nextDir, newStyle);
                }
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

        private static bool CheckAddDir(DirectoryInfo di)
        {
            DirectoryInfo[] dia = di.GetDirectories();
            if (dia.Length > 0)
                return false;
            FileInfo[] fia = di.GetFiles();

            foreach (FileInfo f in fia)
            {
                string ext = Path.GetExtension(f.Name).ToLower();

                switch (ext)
                {
                    case ".zip":
                    case ".7z":
                        return false;
                }
            }
            return true;
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

        private static void AddDirAsGame(DirectoryInfo di, DatDir thisDir)
        {
            DatDir fDir = new DatDir(DatFileType.Dir)
            {
                Name = Path.GetFileNameWithoutExtension(di.Name),
                DGame = new DatGame()
            };
            fDir.DGame.Description = fDir.Name;
            thisDir.ChildAdd(fDir);

            FileInfo[] fia = di.GetFiles();

            int fCount = 0;
            foreach (FileInfo f in fia)
            {
                Console.WriteLine(f.FullName);
                AddFile(f, fDir);

                //fCount++;
                if (fCount > 10)
                    break;
            }

        }

        private static void AddFile(FileInfo f, DatDir thisDir)
        {
            Compress.File.File zf1=new Compress.File.File();
            zf1.ZipFileOpen(f.FullName, -1, true);
            FileScan fs = new FileScan();
            List<FileScan.FileResults> fr = fs.Scan(zf1, true, true);
            
            DatFile df = new DatFile(DatFileType.File)
            {
                Name = f.Name,
                Size = fr[0].Size,
                CRC = fr[0].CRC,
                SHA1 = fr[0].SHA1
            };

            thisDir.ChildAdd(df);
            zf1.ZipFileClose();
        }
    }
}
