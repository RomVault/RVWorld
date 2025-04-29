using System.Xml;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatReader
{
    public static class DatMessXmlReader
    {
        public static bool ReadDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir("", FileType.UnSet) };
            if (!LoadHeaderFromDat(doc, strFilename, datHeader))
            {
                return false;
            }

            XmlNodeList gameNodeList = doc.DocumentElement?.SelectNodes("software");

            if (gameNodeList == null)
            {
                return false;
            }

            for (int i = 0; i < gameNodeList.Count; i++)
                LoadGameFromDat(datHeader.BaseDir, gameNodeList[i]);

            return true;
        }

        private static bool LoadHeaderFromDat(XmlDocument doc, string filename, DatHeader datHeader)
        {
            XmlNodeList head = doc.SelectNodes("softwarelist");
            if (head == null)
            {
                return false;
            }

            if (head.Count == 0)
            {
                return false;
            }

            if (head[0].Attributes == null)
            {
                return false;
            }

            datHeader.Filename = filename;
            datHeader.Name = VarFix.String(head[0].Attributes.GetNamedItem("name"));
            datHeader.Description = VarFix.String(head[0].Attributes.GetNamedItem("description"));

            return true;
        }

        private static void LoadGameFromDat(DatDir parentDir, XmlNode gameNode)
        {
            if (gameNode.Attributes == null)
            {
                return;
            }

            DatDir dDir = new DatDir(VarFix.String(gameNode.Attributes.GetNamedItem("name")), FileType.UnSet)
            {
                DGame = new DatGame()
            };

            DatGame dGame = dDir.DGame;
            dGame.Description = VarFix.String(gameNode.SelectSingleNode("description"));
            dGame.RomOf = VarFix.String(gameNode.Attributes?.GetNamedItem("romof"));
            dGame.CloneOf = VarFix.String(gameNode.Attributes?.GetNamedItem("cloneof"));
            dGame.Year = VarFix.String(gameNode.SelectSingleNode("year"));
            dGame.Manufacturer = VarFix.String(gameNode.SelectSingleNode("publisher"));

            XmlNodeList partNodeList = gameNode.SelectNodes("part");
            if (partNodeList == null)
            {
                return;
            }

            for (int iP = 0; iP < partNodeList.Count; iP++)
            {
                int indexContinue = -1;
                XmlNodeList dataAreaNodeList = partNodeList[iP].SelectNodes("dataarea");
                if (dataAreaNodeList == null)
                {
                    continue;
                }
                for (int iD = 0; iD < dataAreaNodeList.Count; iD++)
                {
                    XmlNodeList romNodeList = dataAreaNodeList[iD].SelectNodes("rom");
                    if (romNodeList == null)
                    {
                        continue;
                    }
                    for (int iR = 0; iR < romNodeList.Count; iR++)
                    {
                        LoadRomFromDat(dDir, romNodeList[iR], ref indexContinue);
                    }
                }
            }

            for (int iP = 0; iP < partNodeList.Count; iP++)
            {
                XmlNodeList diskAreaNodeList = partNodeList[iP].SelectNodes("diskarea");
                if (diskAreaNodeList == null)
                {
                    continue;
                }
                for (int iD = 0; iD < diskAreaNodeList.Count; iD++)
                {
                    XmlNodeList romNodeList = diskAreaNodeList[iD].SelectNodes("disk");
                    if (romNodeList == null)
                    {
                        continue;
                    }
                    for (int iR = 0; iR < romNodeList.Count; iR++)
                    {
                        LoadDiskFromDat(dDir, romNodeList[iR]);
                    }
                }
            }

            if (dDir.Count > 0)
            {
                parentDir.ChildAdd(dDir);
            }
        }

        private static void LoadRomFromDat(DatDir parentDir, XmlNode romNode, ref int indexContinue)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            XmlNode name = romNode.Attributes.GetNamedItem("name");
            string loadflag = VarFix.String(romNode.Attributes.GetNamedItem("loadflag"));
            if (name != null)
            {
                DatFile dRom = new DatFile(VarFix.String(name), FileType.UnSet)
                {
                    Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
                    CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
                    SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                    Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"))
                };

                indexContinue = parentDir.ChildAdd(dRom);
            }
            else if (loadflag.ToLower() == "continue")
            {
                DatFile tRom = (DatFile)parentDir[indexContinue];
                tRom.Size += VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            }
            else if (loadflag.ToLower() == "ignore")
            {
                DatFile tRom = (DatFile)parentDir[indexContinue];
                tRom.Size += VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            }
        }

        private static void LoadDiskFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            DatFile dRom = new DatFile(VarFix.CleanCHD(romNode.Attributes.GetNamedItem("name")), FileType.UnSet)
            {
                SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
                isDisk = true
            };

            parentDir.ChildAdd(dRom);
        }
    }
}