using System;
using DATReader.DatStore;
using RVIO;

namespace DATReader.DatWriter
{
    public static class DatXMLWriter
    {
        public static void WriteDat(string strFilename, DatHeader datHeader, bool newStyle = false)
        {
            string dir = Path.GetDirectoryName(strFilename);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            using (DatStreamWriter sw = new DatStreamWriter(strFilename))
            {
                WriteToStream(sw, datHeader, newStyle);
            }
        }
        public static void WriteDat(System.IO.Stream strOut, DatHeader datHeader, bool newStyle = false)
        {
            using (DatStreamWriter sw = new DatStreamWriter(strOut))
            {
                WriteToStream(sw, datHeader, newStyle);
            }
        }

        private static void WriteToStream(DatStreamWriter sw, DatHeader datHeader, bool newStyle)
        {
            sw.WriteLine("<?xml version=\"1.0\"?>");
            if (newStyle)
                sw.WriteLine("<RVDatFile>", 1);
            else
                sw.WriteLine("<DatFile>", 1);

            WriteHeader(sw, datHeader);

            writeBase(sw, datHeader.BaseDir, newStyle);

            if (newStyle)
                sw.WriteLine("</RVDatFile>", -1);
            else
                sw.WriteLine("</DatFile>", -1);
        }

        private static void WriteHeader(DatStreamWriter sw, DatHeader datHeader)
        {
            sw.WriteLine("<header>", 1);
            sw.WriteNode("name", datHeader.Name);
            sw.WriteNode("rootdir", datHeader.RootDir);
            sw.WriteNode("header", datHeader.Header);
            sw.WriteNode("type", datHeader.Type);
            sw.WriteNode("description", datHeader.Description);
            sw.WriteNode("category", datHeader.Category);
            sw.WriteNode("version", datHeader.Version);
            sw.WriteNode("date", datHeader.Date);
            sw.WriteNode("author", datHeader.Author);
            sw.WriteNode("email", datHeader.Email);
            sw.WriteNode("homepage", datHeader.Homepage);
            sw.WriteNode("url", datHeader.URL);
            sw.WriteNode("comment", datHeader.Comment);
            sw.Write(@"<romvault");
            sw.WriteItem("forcepacking", datHeader.Compression);
            sw.WriteEnd(@"/>");
            sw.WriteLine("</header>", -1);
        }

        private static void writeBase(DatStreamWriter sw, DatDir baseDirIn, bool newStyle)
        {
            DatBase[] dirChildren = baseDirIn.ToArray();

            if (dirChildren == null)
                return;
            foreach (DatBase baseObj in dirChildren)
            {
                if (baseObj is DatDir baseDir)
                {
                    if (baseDir.DGame != null)
                    {
                        DatGame g = baseDir.DGame;
                        sw.Write(newStyle ? @"<set" : @"<game");

                        sw.WriteItem("name", baseDir.Name);

                        if (newStyle)
                        {
                            if (baseDir.DatFileType == DatFileType.DirTorrentZip)
                            {
                                sw.WriteItem("type", "trrntzip");
                            }
                            else if (baseDir.DatFileType == DatFileType.DirRVZip)
                            {
                                sw.WriteItem("type", "rvzip");
                            }
                            else if (baseDir.DatFileType == DatFileType.Dir7Zip)
                            {
                                sw.WriteItem("type", "7zip");
                            }
                            else if (baseDir.DatFileType == DatFileType.Dir)
                            {
                                sw.WriteItem("type", "dir");
                            }
                        }

                        if (!g.IsEmuArc)
                        {
                            sw.WriteItem("cloneof", g.CloneOf);
                            sw.WriteItem("romof", g.RomOf);
                        }
                        if (!string.IsNullOrWhiteSpace(g.IsBios) && g.IsBios != "no") sw.WriteItem("isbios", g.IsBios);
                        if (!string.IsNullOrWhiteSpace(g.IsDevice) && g.IsDevice != "no") sw.WriteItem("isdevice", g.IsDevice);
                        if (!string.IsNullOrWhiteSpace(g.Runnable) && g.Runnable != "yes") sw.WriteItem("runnable", g.Runnable);
                        sw.WriteEnd(@">", 1);

                        sw.WriteNode("description", g.Description);
                        //if (newStyle && !string.IsNullOrWhiteSpace(g.Comments))
                        //    sw.WriteNode("comments", g.Comments);
                        if (g.IsEmuArc)
                        {
                            sw.WriteLine("<tea>", 1);
                            sw.WriteNode("titleid", g.TitleId);
                            sw.WriteNode("source", g.Source);
                            sw.WriteNode("publisher", g.Publisher);
                            sw.WriteNode("developer", g.Developer);
                            sw.WriteNode("year", g.Year);
                            sw.WriteNode("genre", g.Genre);
                            sw.WriteNode("subgenre", g.SubGenre);
                            sw.WriteNode("ratings", g.Ratings);
                            sw.WriteNode("score", g.Score);
                            sw.WriteNode("players", g.Players);
                            sw.WriteNode("enabled", g.Enabled);
                            sw.WriteNode("crc", g.CRC);
                            sw.WriteNode("cloneof", g.CloneOf);
                            sw.WriteNode("relatedto", g.RelatedTo);

                            sw.WriteLine("</trurip>", -1);
                        }
                        else
                        {
                            sw.WriteNode("year", g.Year);
                            sw.WriteNode("manufacturer", g.Manufacturer);
                        }

                        writeBase(sw, baseDir, newStyle);

                        if (g.device_ref != null)
                        {
                            foreach (string d in g.device_ref)
                            {
                                sw.WriteLine($@"<device_ref name=""{d}""/>");
                            }
                        }

                        sw.WriteLine(newStyle ? @"</set>" : @"</game>", -1);
                    }
                    else
                    {
                        sw.WriteLine(@"<dir name=""" + Etxt(baseDir.Name) + @""">", 1);
                        writeBase(sw, baseDir, newStyle);
                        sw.WriteLine(@"</dir>", -1);
                    }
                    continue;
                }

                if (baseObj is DatFile baseRom)
                {
                    if (baseRom.Name.Substring(baseRom.Name.Length - 1) == "/" && newStyle)
                    {
                        sw.Write(@"<dir");
                        sw.WriteItem("name", baseRom.Name.Substring(0, baseRom.Name.Length - 1));
                        //sw.WriteItem("merge", baseRom.Merge);
                        if (baseRom.DateModified != "1996/12/24 23:32:00")
                            sw.WriteItem("date", baseRom.DateModified);
                        sw.WriteEnd("/>");
                    }
                    else
                    {
                        if (baseRom.isDisk)
                            sw.Write(@"<disk");
                        else
                            sw.Write(newStyle ? @"<file" : @"<rom");

                        sw.WriteItem("name", baseRom.Name);
                        //sw.WriteItem("merge", baseRom.Merge);
                        sw.WriteItem("size", baseRom.Size);
                        sw.WriteItem("crc", baseRom.CRC);
                        sw.WriteItem("sha1", baseRom.SHA1);
                        sw.WriteItem("md5", baseRom.MD5);
                        if (baseRom.DateModified != "1996/12/24 23:32:00")
                            sw.WriteItem("date", baseRom.DateModified);
                        if (baseRom.Status != null && baseRom.Status.ToLower() != "good")
                            sw.WriteItem("status", baseRom.Status);
                        sw.WriteEnd("/>");
                    }
                }
            }
        }

        private static string Etxt(string e)
        {
            string ret = "";
            foreach (char c in e)
            {
                if (c == '&') { ret += "&amp;"; continue; }
                if (c == '\"') { ret += "&quot;"; continue; }
                if (c == '\'') { ret += "&apos;"; continue; }
                if (c == '<') { ret += "&lt;"; continue; }
                if (c == '>') { ret += "&gt;"; continue; }
                //if (c == 127) { ret += "&#7f;"; continue; }
                if (c < ' ')
                {
                    ret += $"&#{((int)c).ToString("X2")};";
                    continue;
                }
                ret += c;
            }

            return ret;
        }

        private static string ByteToStr(byte[] b)
        {
            return b == null ? "" : BitConverter.ToString(b).ToLower().Replace("-", "");
        }

        private class DatStreamWriter : IDisposable
        {
            private int _tabDepth;
            private string _tabString = "";
            private readonly System.IO.StreamWriter _sw;
            public DatStreamWriter(string path)
            {
                _sw = File.CreateText(path);
            }
            public DatStreamWriter(System.IO.Stream stOut)
            {
                _sw= new System.IO.StreamWriter(stOut);
            }

            public void Dispose()
            {
                _sw.Close();
                _sw.Dispose();
            }

            public void WriteLine(string value)
            {
                _sw.WriteLine(_tabString + value);
            }
            public void Write(string value)
            {
                _sw.Write(_tabString + value);
            }

            public void WriteLine(string value, int tabDir)
            {
                if (tabDir == -1)
                {
                    if (_tabDepth > 0)
                        _tabDepth -= 1;
                    _tabString = new string('\t', _tabDepth);
                }
                _sw.WriteLine(_tabString + value);
                if (tabDir == 1)
                {
                    _tabDepth += 1;
                    _tabString = new string('\t', _tabDepth);
                }
            }
            public void WriteEnd(string value)
            {
                _sw.WriteLine(value);
            }

            public void WriteEnd(string value, int tabDir)
            {
                if (tabDir == -1)
                {
                    if (_tabDepth > 0)
                        _tabDepth -= 1;
                    _tabString = new string('\t', _tabDepth);
                }
                _sw.WriteLine(value);
                if (tabDir == 1)
                {
                    _tabDepth += 1;
                    _tabString = new string('\t', _tabDepth);
                }
            }



            public void WriteNode(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                WriteLine("<" + name + ">" + Etxt(value) + "</" + name + ">");
            }

            public void WriteItem(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                _sw.Write(@" " + name + @"=""" + Etxt(value) + @"""");
            }
            public void WriteItem(string name, ulong? value)
            {
                if (value == null)
                    return;
                _sw.Write(@" " + name + @"=""" + value + @"""");
            }
            public void WriteItem(string name, byte[] value)
            {
                if (value == null)
                    return;
                _sw.Write(@" " + name + @"=""" + ByteToStr(value) + @"""");
            }


        }

    }

}
