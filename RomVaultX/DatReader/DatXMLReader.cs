using System.Xml;
using FileHeaderReader;
using RomVaultX.DB;
using RomVaultX.Util;
using RVIO;

namespace RomVaultX.DatReader
{
    public static class DatXmlReader
    {
        public static bool ReadDat(XmlDocument doc, string strFilename, out RvDat rvDat)
        {
            rvDat = new RvDat();
            string filename = Path.GetFileName(strFilename);

            if (!LoadHeaderFromDat(doc, rvDat, filename, out HeaderFileType datFileType))
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
                    LoadDirFromDat(rvDat, dirNodeList[i], "", datFileType);
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(rvDat, gameNodeList[i], "", datFileType);
                }
            }

            XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
            if (machineNodeList != null)
            {
                for (int i = 0; i < machineNodeList.Count; i++)
                {
                    LoadGameFromDat(rvDat, machineNodeList[i], "", datFileType);
                }
            }

            return true;
        }

        public static bool ReadMameDat(XmlDocument doc, string strFilename, out RvDat rvDat)
        {
            rvDat = new RvDat();
            string filename = Path.GetFileName(strFilename);

            if (!LoadMameHeaderFromDat(doc, rvDat, filename))
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
                    LoadDirFromDat(rvDat, dirNodeList[i], "", HeaderFileType.Nothing);
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(rvDat, gameNodeList[i], "", HeaderFileType.Nothing);
                }
            }


            XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
            if (machineNodeList != null)
            {
                for (int i = 0; i < machineNodeList.Count; i++)
                {
                    LoadGameFromDat(rvDat, machineNodeList[i], "", HeaderFileType.Nothing);
                }
            }

            return true;
        }


        private static bool LoadHeaderFromDat(XmlDocument doc, RvDat rvDat, string filename, out HeaderFileType datFileType)
        {
            datFileType = HeaderFileType.Nothing;

            if (doc.DocumentElement == null)
            {
                return false;
            }
            XmlNode head = doc.DocumentElement.SelectSingleNode("header");

            rvDat.Filename = filename;

            if (head == null)
            {
                return false;
            }
            rvDat.Name = VarFix.CleanFileName(head.SelectSingleNode("name"));
            rvDat.RootDir = VarFix.CleanFileName(head.SelectSingleNode("rootdir"));
            rvDat.Description = VarFix.String(head.SelectSingleNode("description"));
            rvDat.Category = VarFix.String(head.SelectSingleNode("category"));
            rvDat.Version = VarFix.String(head.SelectSingleNode("version"));
            rvDat.Date = VarFix.String(head.SelectSingleNode("date"));
            rvDat.Author = VarFix.String(head.SelectSingleNode("author"));
            rvDat.Email = VarFix.String(head.SelectSingleNode("email"));
            rvDat.Homepage = VarFix.String(head.SelectSingleNode("homepage"));
            rvDat.URL = VarFix.String(head.SelectSingleNode("url"));
            rvDat.Comment = VarFix.String(head.SelectSingleNode("comment"));


            XmlNode packingNode = head.SelectSingleNode("romvault") ?? head.SelectSingleNode("clrmamepro");

            if (packingNode?.Attributes != null)
            {
                // dat Header XML filename
                datFileType = FileHeaderReader.FileHeaderReader.GetFileTypeFromHeader(VarFix.String(packingNode.Attributes.GetNamedItem("header")));

                rvDat.MergeType = VarFix.String(packingNode.Attributes.GetNamedItem("forcemerging")).ToLower();
            }

            return true;
        }

        private static bool LoadMameHeaderFromDat(XmlDocument doc, RvDat rvDat, string filename)
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

            rvDat.Filename = filename;
            rvDat.Name = VarFix.CleanFileName(head.Attributes.GetNamedItem("build")); // ?? is this correct should it be Name & Descripition??
            rvDat.Description = VarFix.String(head.Attributes.GetNamedItem("build"));

            return true;
        }


        private static void LoadDirFromDat(RvDat rvDat, XmlNode dirNode, string rootDir, HeaderFileType datFileType)
        {
            if (dirNode.Attributes == null)
            {
                return;
            }

            string fullname = VarFix.CleanFullFileName(dirNode.Attributes.GetNamedItem("name"));

            XmlNodeList dirNodeList = dirNode.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(rvDat, dirNodeList[i], Path.Combine(rootDir, fullname), datFileType);
                }
            }

            XmlNodeList gameNodeList = dirNode.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(rvDat, gameNodeList[i], Path.Combine(rootDir, fullname), datFileType);
                }
            }
        }

        private static void LoadGameFromDat(RvDat rvDat, XmlNode gameNode, string rootDir, HeaderFileType datFileType)
        {
            if (gameNode.Attributes == null)
            {
                return;
            }

            RvGame rvGame = new RvGame
            {
                Name = VarFix.CleanFullFileName(gameNode.Attributes.GetNamedItem("name")),
                RomOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("romof")),
                CloneOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof")),
                SampleOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("sampleof")),
                Description = VarFix.String(gameNode.SelectSingleNode("description")),
                SourceFile = VarFix.String(gameNode.Attributes.GetNamedItem("sourcefile")),
                IsBios = VarFix.String(gameNode.Attributes.GetNamedItem("isbios")),
                Board = VarFix.String(gameNode.Attributes.GetNamedItem("board")),
                Year = VarFix.String(gameNode.SelectSingleNode("year")),
                Manufacturer = VarFix.String(gameNode.SelectSingleNode("manufacturer"))
            };

            XmlNode trurip = gameNode.SelectSingleNode("trurip");
            if (trurip != null)
            {
                rvGame.IsTrurip = true;
                rvGame.Publisher = VarFix.String(trurip.SelectSingleNode("publisher"));
                rvGame.Developer = VarFix.String(trurip.SelectSingleNode("developer"));
                rvGame.Edition = VarFix.String(trurip.SelectSingleNode("edition"));
                rvGame.Version = VarFix.String(trurip.SelectSingleNode("version"));
                rvGame.Type = VarFix.String(trurip.SelectSingleNode("type"));
                rvGame.Media = VarFix.String(trurip.SelectSingleNode("media"));
                rvGame.Language = VarFix.String(trurip.SelectSingleNode("language"));
                rvGame.Players = VarFix.String(trurip.SelectSingleNode("players"));
                rvGame.Ratings = VarFix.String(trurip.SelectSingleNode("ratings"));
                rvGame.Peripheral = VarFix.String(trurip.SelectSingleNode("peripheral"));
                rvGame.Genre = VarFix.String(trurip.SelectSingleNode("genre"));
                rvGame.MediaCatalogNumber = VarFix.String(trurip.SelectSingleNode("mediacatalognumber"));
                rvGame.BarCode = VarFix.String(trurip.SelectSingleNode("barcode"));
            }

            rvGame.Name = Path.Combine(rootDir, rvGame.Name);

            rvDat.AddGame(rvGame);

            XmlNodeList romNodeList = gameNode.SelectNodes("rom");
            if (romNodeList != null)
            {
                for (int i = 0; i < romNodeList.Count; i++)
                {
                    LoadRomFromDat(rvGame, romNodeList[i], datFileType);
                }
            }

            XmlNodeList diskNodeList = gameNode.SelectNodes("disk");
            if (diskNodeList != null)
            {
                for (int i = 0; i < diskNodeList.Count; i++)
                {
                    LoadDiskFromDat(rvGame, diskNodeList[i]);
                }
            }
        }

        private static void LoadRomFromDat(RvGame rvGame, XmlNode romNode, HeaderFileType datFileType)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            RvRom rvRom = new RvRom
            {
                AltType=datFileType,
                Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")),
                Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
                CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
                Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge")),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"))
            };

            rvGame.AddRom(rvRom);
        }

        private static void LoadDiskFromDat(RvGame rvGame, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            string Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")) + ".chd";
            byte[] SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
            byte[] MD5CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32);
            string Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge"));
            string Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));
        }
    }
}