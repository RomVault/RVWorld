using System.Collections.Generic;
using System.Xml;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatReader
{
    public class DatXmlReader
    {
        private string _filename;
        public bool ReadDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir(DatFileType.UnSet) };
            _filename = strFilename;

            if (!LoadHeaderFromDat(doc, datHeader))
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

        public bool ReadMameDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir(DatFileType.UnSet) };
            _filename = strFilename;

            if (!LoadMameHeaderFromDat(doc, datHeader))
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


        private bool LoadHeaderFromDat(XmlDocument doc, DatHeader datHeader)
        {
            if (doc.DocumentElement == null)
            {
                return false;
            }
            XmlNode head = doc.DocumentElement.SelectSingleNode("header");

            datHeader.Filename = _filename;

            if (head == null)
            {
                return false;
            }
            datHeader.Name = VarFix.String(head.SelectSingleNode("name"));
            datHeader.Type = VarFix.String(head.SelectSingleNode("type"));
            datHeader.RootDir = VarFix.String(head.SelectSingleNode("rootdir"));
            datHeader.Description = VarFix.String(head.SelectSingleNode("description"));
            datHeader.Category = VarFix.String(head.SelectSingleNode("category"));
            datHeader.Version = VarFix.String(head.SelectSingleNode("version"));
            datHeader.Date = VarFix.String(head.SelectSingleNode("date"));
            datHeader.Author = VarFix.String(head.SelectSingleNode("author"));
            datHeader.Email = VarFix.String(head.SelectSingleNode("email"));
            datHeader.Homepage = VarFix.String(head.SelectSingleNode("homepage"));
            datHeader.URL = VarFix.String(head.SelectSingleNode("url"));
            datHeader.Comment = VarFix.String(head.SelectSingleNode("comment"));

            datHeader.IsSuperDat = (datHeader.Type.ToLower() == "superdat") || (datHeader.Type.ToLower() == "gigadat");

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

        private bool LoadMameHeaderFromDat(XmlDocument doc, DatHeader datHeader)
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

            datHeader.Filename = _filename;
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

            DatDir dir = new DatDir(DatFileType.UnSet)
            {
                Name = name
            };

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
        }

        private static void LoadGameFromDat(DatDir parentDir, XmlNode gameNode)
        {
            if (gameNode.Attributes == null)
            {
                return;
            }

            DatGame dGame=new DatGame();
            DatDir dDir = new DatDir(DatFileType.UnSet)
            {
                Name = VarFix.String(gameNode.Attributes.GetNamedItem("name")),
                DGame = dGame
            };

            dGame.RomOf = VarFix.String(gameNode.Attributes.GetNamedItem("romof"));
            dGame.CloneOf = VarFix.String(gameNode.Attributes.GetNamedItem("cloneof"));
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
                dGame.TitleId = VarFix.String(emuArc.SelectSingleNode("titleid"));
                dGame.Publisher = VarFix.String(emuArc.SelectSingleNode("publisher"));
                dGame.Developer = VarFix.String(emuArc.SelectSingleNode("developer"));
                dGame.Year = VarFix.String(gameNode.SelectSingleNode("year"));
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

            parentDir.ChildAdd(dDir);

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

            XmlNodeList deviceRef = gameNode.SelectNodes("device_ref");
            if (deviceRef != null)
            {
                for (int i = 0; i < deviceRef.Count; i++)
                {
                    LoadDeviceRef(dGame, deviceRef[i]);
                }
            }

            XmlNodeList slotList = gameNode.SelectNodes("slot");
            if (slotList != null)
            {
                for (int i = 0; i < slotList.Count; i++)
                {
                    LoadSlot(dGame, slotList[i]);
                }
            }
        }

        private static void LoadRomFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            DatFile rvRom = new DatFile(DatFileType.UnSet)
            {
                Name = VarFix.String(romNode.Attributes.GetNamedItem("name")),
                Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
                CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
                Merge = VarFix.String(romNode.Attributes.GetNamedItem("merge")),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
                Region = VarFix.ToLower(romNode.Attributes.GetNamedItem("region")),
                DateModified =VarFix.String(romNode.Attributes.GetNamedItem("date")),
                //DateCreated = VarFix.String(romNode.Attributes.GetNamedItem("CreationDate")),
                //DateAccessed = VarFix.String(romNode.Attributes.GetNamedItem("LastAccessDate")),
            };

            parentDir.ChildAdd(rvRom);
        }

        private static void LoadDiskFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            DatFile rvRom = new DatFile(DatFileType.UnSet)
            {
                Name = VarFix.String(romNode.Attributes.GetNamedItem("name")) + ".chd",
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
                Merge = VarFix.String(romNode.Attributes.GetNamedItem("merge")),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
                isDisk = true
            };
            if (!string.IsNullOrWhiteSpace(rvRom.Merge))
                rvRom.Merge += ".chd";

            parentDir.ChildAdd(rvRom);
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

        private static void LoadSlot(DatGame dGame, XmlNode slot)
        {
            XmlNodeList slotList = slot.SelectNodes("slotoption");
            if (slotList == null)
                return;

            for (int i = 0; i < slotList.Count; i++)
            {
                if (slotList[i].Attributes == null)
                    continue;

                string name = VarFix.String(slotList[i].Attributes.GetNamedItem("devname"));

                if (dGame.slot == null)
                    dGame.slot = new List<string>();

                dGame.slot.Add(name);
            }
        }
    }
}