using System.Collections.Generic;
using System.Xml;
using Compress;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatReader
{
    public static class DatXmlReader
    {
        // new version of this file also reads rvdats

        public static bool ReadDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir("", FileType.UnSet) };

            if (!LoadHeaderFromDat(doc, strFilename, datHeader))
            {
                return false;
            }

            if (doc.DocumentElement == null)
            {
                return false;
            }

            XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(datHeader.BaseDir, dirNodeList[i]);
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(datHeader.BaseDir, gameNodeList[i]);
                }
            }

            XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
            if (machineNodeList != null)
            {
                for (int i = 0; i < machineNodeList.Count; i++)
                {
                    LoadGameFromDat(datHeader.BaseDir, machineNodeList[i]);
                }
            }

            XmlNodeList romNodeList = doc.DocumentElement.SelectNodes("rom");
            if (romNodeList != null)
            {
                for (int i = 0; i < romNodeList.Count; i++)
                {
                    LoadRomFromDat(datHeader.BaseDir, romNodeList[i]);
                }
            }

            XmlNodeList diskNodeList = doc.DocumentElement.SelectNodes("disk");
            if (diskNodeList != null)
            {
                for (int i = 0; i < diskNodeList.Count; i++)
                {
                    LoadDiskFromDat(datHeader.BaseDir, diskNodeList[i]);
                }
            }

            return true;
        }

        public static bool ReadMameDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir("", FileType.UnSet) };
            datHeader.MameXML = true;

            if (!LoadMameHeaderFromDat(doc, strFilename, datHeader))
            {
                return false;
            }

            if (doc.DocumentElement == null)
            {
                return false;
            }

            XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(datHeader.BaseDir, dirNodeList[i]);
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(datHeader.BaseDir, gameNodeList[i]);
                }
            }


            XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
            if (machineNodeList != null)
            {
                for (int i = 0; i < machineNodeList.Count; i++)
                {
                    LoadGameFromDat(datHeader.BaseDir, machineNodeList[i]);
                }
            }



            return true;
        }


        private static bool LoadHeaderFromDat(XmlDocument doc, string filename, DatHeader datHeader)
        {
            if (doc.DocumentElement == null)
            {
                return false;
            }
            XmlNode head = doc.DocumentElement.SelectSingleNode("header");

            datHeader.Filename = filename;

            if (head == null)
            {
                return false;
            }
            datHeader.Id = VarFix.String(head.SelectSingleNode("id"));
            datHeader.Name = VarFix.String(head.SelectSingleNode("name"));
            datHeader.Type = VarFix.String(head.SelectSingleNode("type"));
            datHeader.RootDir = VarFix.String(head.SelectSingleNode("rootdir"));
            datHeader.Description = VarFix.String(head.SelectSingleNode("description"));
            datHeader.Subset = VarFix.String(head.SelectSingleNode("subset"));
            datHeader.Category = VarFix.String(head.SelectSingleNode("category"));
            datHeader.Version = VarFix.String(head.SelectSingleNode("version"));
            datHeader.Date = VarFix.String(head.SelectSingleNode("date"));
            datHeader.Author = VarFix.String(head.SelectSingleNode("author"));
            datHeader.Email = VarFix.String(head.SelectSingleNode("email"));
            datHeader.Homepage = VarFix.String(head.SelectSingleNode("homepage"));
            datHeader.URL = VarFix.String(head.SelectSingleNode("url"));
            datHeader.Comment = VarFix.String(head.SelectSingleNode("comment"));

            XmlNode packingNode = head.SelectSingleNode("romvault") ?? head.SelectSingleNode("clrmamepro");
            if (packingNode?.Attributes != null)
            {
                // dat Header XML filename
                datHeader.Header = VarFix.String(packingNode.Attributes.GetNamedItem("header"));

                // zip, unzip, file
                datHeader.Compression = VarFix.String(packingNode.Attributes.GetNamedItem("forcepacking")).ToLower();

                // split, full
                datHeader.MergeType = VarFix.String(packingNode.Attributes.GetNamedItem("forcemerging")).ToLower();

                // noautodir, nogame
                datHeader.Dir = VarFix.String(packingNode.Attributes.GetNamedItem("dir")).ToLower();

            }

            // Look for: <notzipped>true</notzipped>
            string notzipped = VarFix.String(head.SelectSingleNode("notzipped"));
            datHeader.NotZipped = ((notzipped.ToLower() == "true") || (notzipped.ToLower() == "yes"));

            return true;
        }

        private static bool LoadMameHeaderFromDat(XmlDocument doc, string filename, DatHeader datHeader)
        {
            if (doc.DocumentElement == null)
            {
                return false;
            }
            XmlNode head = doc.SelectSingleNode("mame");

            if (head?.Attributes == null)
            {
                return false;
            }

            datHeader.Filename = filename;
            datHeader.Name = VarFix.String(head.Attributes.GetNamedItem("build"));
            datHeader.Description = VarFix.String(head.Attributes.GetNamedItem("build"));

            return true;
        }


        private static void LoadDirFromDat(DatDir parentDir, XmlNode dirNode)
        {
            if (dirNode.Attributes == null)
            {
                return;
            }

            string name = VarFix.String(dirNode.Attributes.GetNamedItem("name"));

            DatDir dir = new DatDir(name, FileType.UnSet);

            parentDir.ChildAdd(dir);

            XmlNodeList dirNodeList = dirNode.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(dir, dirNodeList[i]);
                }
            }

            XmlNodeList gameNodeList = dirNode.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(dir, gameNodeList[i]);
                }
            }
            XmlNodeList machineNodeList = dirNode.SelectNodes("machine");
            if (machineNodeList != null)
            {
                for (int i = 0; i < machineNodeList.Count; i++)
                {
                    LoadGameFromDat(dir, machineNodeList[i]);
                }
            }
        }

        private static void LoadGameFromDat(DatDir parentDir, XmlNode gameNode)
        {
            if (gameNode.Attributes == null)
            {
                return;
            }

            DatGame dGame = new DatGame();

            FileType fileType = FileType.UnSet;
            string strFileType = VarFix.String(gameNode.Attributes.GetNamedItem("type")).ToLower();
            if (strFileType == "dir")
                fileType = FileType.Dir;
            else if (strFileType == "zip")
                fileType = FileType.Zip;
            else if (strFileType == "7z")
                fileType = FileType.SevenZip;

            DatDir dDir = new DatDir(VarFix.String(gameNode.Attributes.GetNamedItem("name")), fileType)
            {
                DGame = dGame
            };


            dGame.Id = VarFix.String(gameNode.Attributes.GetNamedItem("id"));
            dGame.RomOf = VarFix.String(gameNode.Attributes.GetNamedItem("romof"));
            dGame.CloneOf = VarFix.String(gameNode.Attributes.GetNamedItem("cloneof"));
            dGame.CloneOfId = VarFix.String(gameNode.Attributes.GetNamedItem("cloneofid"));
            dGame.SampleOf = VarFix.String(gameNode.Attributes.GetNamedItem("sampleof"));
            dGame.Description = VarFix.String(gameNode.SelectSingleNode("description"));
            dGame.SourceFile = VarFix.String(gameNode.Attributes?.GetNamedItem("sourcefile"));
            dGame.IsBios = VarFix.String(gameNode.Attributes?.GetNamedItem("isbios"));
            dGame.IsDevice = VarFix.String(gameNode.Attributes?.GetNamedItem("isdevice"));
            dGame.Board = VarFix.String(gameNode.Attributes?.GetNamedItem("board"));
            dGame.Year = VarFix.String(gameNode.SelectSingleNode("year"));
            dGame.Manufacturer = VarFix.String(gameNode.SelectSingleNode("manufacturer"));
            dGame.Runnable = VarFix.String(gameNode.Attributes?.GetNamedItem("runnable"));

            XmlNode emuArc = gameNode.SelectSingleNode("tea") ?? gameNode.SelectSingleNode("trurip") ?? gameNode.SelectSingleNode("EmuArc");
            if (emuArc != null)
            {
                dGame.IsEmuArc = true;
                dGame.Id = VarFix.String(emuArc.SelectSingleNode("titleid"));
                dGame.Publisher = VarFix.String(emuArc.SelectSingleNode("publisher"));
                dGame.Developer = VarFix.String(emuArc.SelectSingleNode("developer"));
                dGame.Year = VarFix.String(emuArc.SelectSingleNode("year"));
                dGame.Genre = VarFix.String(emuArc.SelectSingleNode("genre"));
                dGame.SubGenre = VarFix.String(emuArc.SelectSingleNode("subgenre"));
                dGame.Ratings = VarFix.String(emuArc.SelectSingleNode("ratings"));
                dGame.Score = VarFix.String(emuArc.SelectSingleNode("score"));
                dGame.Players = VarFix.String(emuArc.SelectSingleNode("players"));
                dGame.Enabled = VarFix.String(emuArc.SelectSingleNode("enabled"));
                dGame.CRC = VarFix.String(emuArc.SelectSingleNode("crc"));
                dGame.CloneOf = VarFix.String(emuArc.SelectSingleNode("cloneof"));
                dGame.RelatedTo = VarFix.String(emuArc.SelectSingleNode("relatedto"));
                dGame.Source = VarFix.String(emuArc.SelectSingleNode("source"));
            }


            XmlNodeList romNodeList = gameNode.SelectNodes("rom");
            if (romNodeList != null)
            {
                for (int i = 0; i < romNodeList.Count; i++)
                {
                    LoadRomFromDat(dDir, romNodeList[i]);
                }
            }

            XmlNodeList diskNodeList = gameNode.SelectNodes("disk");
            if (diskNodeList != null)
            {
                for (int i = 0; i < diskNodeList.Count; i++)
                {
                    LoadDiskFromDat(dDir, diskNodeList[i]);
                }
            }

            XmlNodeList categoryList = gameNode.SelectNodes("category");
            if (categoryList != null)
            {
                for (int i = 0; i < categoryList.Count; i++)
                {
                    LoadCategory(dGame, categoryList[i]);
                }
            }

            XmlNodeList deviceRef = gameNode.SelectNodes("device_ref");
            if (deviceRef != null)
            {
                for (int i = 0; i < deviceRef.Count; i++)
                {
                    LoadDeviceRef(dGame, deviceRef[i]);
                }
            }

            parentDir.ChildAdd(dDir);
        }


        private static void LoadRomFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            DatFile rvRom = new DatFile(VarFix.String(romNode.Attributes.GetNamedItem("name")), FileType.UnSet)
            {
                Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
                CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                SHA256 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha256"), 64),
                MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
                Merge = VarFix.String(romNode.Attributes.GetNamedItem("merge")),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
                Region = VarFix.ToLower(romNode.Attributes.GetNamedItem("region")),
                //DateModified =CompressUtils.StringDateTimeToTicks( VarFix.String(romNode.Attributes.GetNamedItem("date"))),
                MIA = VarFix.String(romNode.Attributes.GetNamedItem("mia"))
            };

            parentDir.ChildAdd(rvRom);
        }

        private static void LoadDiskFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            DatFile rvRom = new DatFile(VarFix.CleanCHD(romNode.Attributes.GetNamedItem("name")), FileType.UnSet)
            {
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                SHA256 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha256"), 64),
                MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
                Merge = VarFix.CleanCHD(VarFix.String(romNode.Attributes.GetNamedItem("merge"))),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
                MIA = VarFix.String(romNode.Attributes.GetNamedItem("mia")),
                isDisk = true
            };

            parentDir.ChildAdd(rvRom);
        }

        private static void LoadCategory(DatGame dGame, XmlNode categoryNode)
        {
            string category = VarFix.String(categoryNode);
            if (string.IsNullOrEmpty(category))
                return;

            if (dGame.Category == null)
                dGame.Category = new List<string>();
            dGame.Category.Add(category);
        }


        private static void LoadDeviceRef(DatGame dGame, XmlNode deviceNode)
        {
            if (deviceNode.Attributes == null)
                return;

            string name = VarFix.String(deviceNode.Attributes.GetNamedItem("name"));
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (dGame.device_ref == null)
                dGame.device_ref = new List<string>();
            dGame.device_ref.Add(name);
        }
    }
}