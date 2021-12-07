/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2020                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;

namespace RomVaultCore.ReadDat
{
    public static class DatMaker
    {
        private static StreamWriter _sw;
        private static string _datName;
        private static string _datDir;

        public static void MakeDatFromDir(RvFile startingDir,string filename, bool CHDsAreDisk = true)
        {
            _datName = startingDir.Name;
            _datDir = startingDir.Name;
            Console.WriteLine("Creating Dat: " + filename);
            _sw = new StreamWriter(filename);

            WriteDatFile(startingDir, CHDsAreDisk);

            _sw.Close();

            Console.WriteLine("Dat creation complete");
        }

        private static void WriteDatFile(RvFile dir, bool CHDsAreDisk)
        {
            WriteLine("<?xml version=\"1.0\"?>");
            WriteLine("");
            WriteLine("<datafile>");
            WriteHeader(CHDsAreDisk ? "CHDs as disk - if you see lots of status=nodump, try the other way" :
                "CHD as rom - if you see lots of empty double-quotes, try the other way");

            /* write Games/Dirs */
            if (CHDsAreDisk)
            {
                ProcessDir(dir);
            }
            else
            {
                PlainProcessDir(dir);
            }

            WriteLine("</datafile>");
        }

        private static void WriteHeader(string comment)
        {
            WriteLine("\t<header>");
            WriteLine("\t\t<name>" + clean(_datName) + "</name>");
            WriteLine("\t\t<rootdir>" + clean(_datDir) + "</rootdir>");
            WriteLine("\t\t<comment>" + clean(comment) + "</comment>");
            WriteLine("\t</header>");
        }

        private static void WriteLine(string s)
        {
            _sw.WriteLine(s);
        }

        private static string clean(string s)
        {
            s = s.Replace("&", "&amp;");
            s = s.Replace("\"", "&quot;");
            s = s.Replace("'", "&apos;");
            s = s.Replace("<", "&lt;");
            s = s.Replace(">", "&gt;");
            return s;
        }

        private static bool hasChdGrandChildren(RvFile aDir)
        {
            if (aDir == null)
            {
                return false;
            }
            for (int i = 0; i < aDir.ChildCount; i++)
            {
                RvFile item = aDir.Child(i);
                if (!item.IsDir)
                {
                    continue;
                }
                for (int j = 0; j < item.ChildCount; j++)
                {
                    if (item.Child(j).Name.ToLower().EndsWith(".chd"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // CHDs as rom
        private static void PlainProcessDir(RvFile dir, int depth = 1)
        {
            string indent = new string('\t', depth);
            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvFile item = dir.Child(i);
                if (item.IsDir && (item.FileType == FileType.Zip || item.FileType== FileType.SevenZip || item.FileType == FileType.Dir))
                {
                    string gamename = clean(item.Name);
                    if (item.FileType==FileType.Zip || item.FileType==FileType.SevenZip)
                    {
                        gamename = Path.GetFileNameWithoutExtension(gamename);
                    }

                    WriteLine(indent + "<game name=\"" + gamename + "\">");
                    WriteLine(indent + "\t<description>" + clean(item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description)) + "</description>");

                    for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile file = item.Child(j);
                        if (file.IsFile)
                        {
                            WriteLine(indent + "\t<rom name=\"" + clean(file.Name) + "\" size=\"" + file.Size + "\" crc=\"" + file.CRC.ToHexString() + "\" md5=\"" + file.MD5.ToHexString() + "\" sha1=\"" + file.SHA1.ToHexString() + "\"/>");
                        }
                        RvFile aDir = item.Child(j);
                        if (aDir.IsDir)
                        {
                            string dName = aDir.Name;
                            for (int k = 0; k < aDir.ChildCount; k++)
                            {
                                RvFile subFile = aDir.Child(k);
                                WriteLine(indent + "\t<rom name=\"" + dName + "\\" + clean(subFile.Name) + "\" size=\"" + subFile.Size + "\" crc=\"" + subFile.CRC.ToHexString() + "\" md5=\"" + subFile.MD5.ToHexString() + "\" sha1=\"" + subFile.SHA1.ToHexString() + "\"/>");
                            }
                        }
                    }
                    WriteLine(indent + "</game>");
                }
                // only recurse when grandchildren are not CHDs
                if (item.FileType == FileType.Dir && !hasChdGrandChildren(item))
                {
                    WriteLine(indent + "<dir name=\"" + clean(item.Name) + "\">");
                    PlainProcessDir(item, depth + 1);
                    WriteLine(indent + "</dir>");
                }
            }
        }

        // returns number of CHD files in a RvDir
        // will be confused if there are any RvDirs as well as CHD files in 'dir'
        // does not check sub-dirs of 'dir'
        private static int numDisks(RvFile dir)
        {
            int retVal = 0;
            if (dir != null)
            {
                for (int i = 0; i < dir.ChildCount; i++)
                {
                    RvFile chd = dir.Child(i);
                    if (chd.IsFile && chd.FileType == FileType.File && chd.Name.EndsWith(".chd"))
                    {
                        retVal++;
                    }
                }
            }
            return retVal;
        }

        // writes a list of CHDs as a game when there are no ROMs in the game
        private static void justCHDs(string indent, List<string> lst)
        {
            WriteLine(indent + "<game name=\"" + clean(lst[0]) + "\">");
            WriteLine(indent + "\t<description>" + clean(lst[1]) + "</description>");
            for (int j = 2; j < lst.Count; j++)
            {
                WriteLine(lst[j]);
            }
            WriteLine(indent + "</game>");
        }

        // CHDs as disk
        private static void ProcessDir(RvFile dir, int depth = 1)
        {
            string indent = new string('\t', depth); // recursive indent
            List<string> disks = new List<string> {string.Empty};

            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvFile item = dir.Child(i);
                if (item.IsDir && item.FileType == FileType.Dir)
                {
                    if (disks.Count > 2 && item.Name != disks[0]) // flush the last one if there were only CHDs in it
                    {
                        justCHDs(indent, disks);
                        disks.Clear();
                    }
                    // tabulate next disk list, if any
                    disks = new List<string> {item.Name, item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description)};
                    for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile chd = item.Child(j);
                        if (chd.IsFile && chd.FileType == FileType.File && chd.Name.EndsWith(".chd"))
                        {
                            if (!string.IsNullOrEmpty(chd.AltSHA1.ToHexString()))
                            {
                                disks.Add(indent + "\t<disk name=\"" + clean(chd.Name).Replace(".chd", "") + "\" sha1=\"" + chd.AltSHA1.ToHexString() + "\"/>");
                            }
                            else
                            {
                                disks.Add(indent + "\t<disk name=\"" + clean(chd.Name).Replace(".chd", "") + "\" status=\"nodump\"/>");
                            }
                        }
                    }
                }
                if (item.FileType == FileType.Zip || item.FileType==FileType.SevenZip)
                {
                    WriteLine(indent + "<game name=\"" +Path.GetFileNameWithoutExtension( clean(item.Name)) + "\">");
                    string desc = item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description);
                    WriteLine(indent + "\t<description>" + clean(desc) + "</description>");

                    for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile file = item.Child(j);
                        if (file.IsFile)
                        {
                            WriteLine(indent + "\t<rom name=\"" + clean(file.Name) + "\" size=\"" + file.Size + "\" crc=\"" + file.CRC.ToHexString() + "\" md5=\"" + file.MD5.ToHexString() + "\" sha1=\"" + file.SHA1.ToHexString() + "\"/>");
                        }
                    }

                    if (disks.Count > 2) // take care of previous list of CHDs now
                    {
                        for (int j = 2; j < disks.Count; j++)
                        {
                            WriteLine(disks[j]);
                        }
                        disks.Clear();
                    }

                    WriteLine(indent + "</game>");
                }

                if (item.FileType == FileType.Dir)
                {
                    if (numDisks(item) == 0) // only recurse when children are not CHDs
                    {
                        WriteLine(indent + "<dir name=\"" + clean(item.Name) + "\">");
                        ProcessDir(item, depth + 1);
                        WriteLine(indent + "</dir>");
                    }
                }
            }
            // check for one last CHDs-only game
            if (disks.Count > 2)
            {
                justCHDs(indent, disks);
            }
        }
    }
}