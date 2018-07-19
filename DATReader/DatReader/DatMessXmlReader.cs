using System.Xml;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatReader
{
    public class DatMessXmlReader
    {
        private int _indexContinue;
        private string _filename;

        public bool ReadDat(XmlDocument doc, string strFilename, out DatHeader datHeader)
        {
            datHeader = new DatHeader { BaseDir = new DatDir(DatFileType.UnSet) };
            _filename = strFilename;
            if (!LoadHeaderFromDat(doc, datHeader))
            {
                return false;
            }

            XmlNodeList gameNodeList = doc.DocumentElement?.SelectNodes("software");

            if (gameNodeList == null)
            {
                return false;
            }
            for (int i = 0; i < gameNodeList.Count; i++)
            {
                LoadGameFromDat(datHeader.BaseDir, gameNodeList[i]);
            }

            return true;
        }

        private bool LoadHeaderFromDat(XmlDocument doc, DatHeader datHeader)
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

            datHeader.Filename = _filename;
            datHeader.Name = VarFix.String(head[0].Attributes.GetNamedItem("name"));
            datHeader.Description = VarFix.String(head[0].Attributes.GetNamedItem("description"));

            return true;
        }

        private void LoadGameFromDat(DatDir parentDir, XmlNode gameNode)
        {
            if (gameNode.Attributes == null)
            {
                return;
            }

            DatDir dDir = new DatDir(DatFileType.UnSet)
            {
                Name = VarFix.String(gameNode.Attributes.GetNamedItem("name")),
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
                _indexContinue = -1;
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
                        LoadRomFromDat(dDir, romNodeList[iR]);
                    }
                }
            }

            /*
            for (int iP = 0; iP < partNodeList.Count; iP++)
            {
                XmlNodeList diskAreaNodeList = partNodeList[iP].SelectNodes("diskarea");
                if (diskAreaNodeList != null)
                    for (int iD = 0; iD < diskAreaNodeList.Count; iD++)
                    {
                        XmlNodeList romNodeList = diskAreaNodeList[iD].SelectNodes("disk");
                        if (romNodeList != null)
                            for (int iR = 0; iR < romNodeList.Count; iR++)
                            {
                                LoadDiskFromDat(dDirCHD, romNodeList[iR]);
                            }
                    }
            }
            */

            if (dDir.ChildCount > 0)
            {
                parentDir.ChildAdd(dDir);
            }

            /*
            if (tDirCHD.ChildCount > 0)
                parentDir.ChildAdd(rvGameCHD, index1);
            */
        }

        private void LoadRomFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
            {
                return;
            }

            XmlNode name = romNode.Attributes.GetNamedItem("name");
            string loadflag = VarFix.String(romNode.Attributes.GetNamedItem("loadflag"));
            if (name != null)
            {
                DatFile dRom = new DatFile(DatFileType.UnSet)
                {
                    Name = VarFix.String(name),
                    Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
                    CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
                    SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                    Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"))
                };

                _indexContinue = parentDir.ChildAdd(dRom);
            }
            else if (loadflag.ToLower() == "continue")
            {
                DatFile tRom = (DatFile)parentDir.Child(_indexContinue);
                tRom.Size += VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            }
        }

        /*
        private void LoadDiskFromDat(DatDir parentDir, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            XmlNode name = romNode.Attributes.GetNamedItem("name");

            DatFile dRom = new DatFile(DatFileType.UnSet)
            {
                Name = VarFix.String(name),
                SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
            };

            parentDir.ChildAdd(dRom);
        }
        */
    }
}