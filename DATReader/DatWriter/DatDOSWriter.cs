using System;
using Compress;
using Compress.StructuredZip;
using DATReader.DatStore;
using RVIO;

namespace DATReader.DatWriter
{
    public static class DatDOSWriter
    {
        public static void WriteDat(string strFilename, DatHeader datHeader)
        {
            string dir = Path.GetDirectoryName(strFilename);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            using (DatStreamWriter sw = new DatStreamWriter(strFilename))
            {
                WriteToStream(sw, datHeader);
            }
        }
        public static void WriteDat(System.IO.Stream strOut, DatHeader datHeader)
        {
            using (DatStreamWriter sw = new DatStreamWriter(strOut))
            {
                WriteToStream(sw, datHeader);
            }
        }

        private static void WriteToStream(DatStreamWriter sw, DatHeader datHeader)
        {
            sw.WriteLine("DOSCenter (", 1);
            WriteHeader(sw, datHeader);
            sw.WriteLine(")", -1);

            writeBase(sw, datHeader.BaseDir);

        }

        private static void WriteHeader(DatStreamWriter sw, DatHeader datHeader)
        {
            sw.WriteNode("name", datHeader.Name);
            sw.WriteNode("description", datHeader.Description);
            sw.WriteNode("version", datHeader.Version);
            sw.WriteNode("date", datHeader.Date);
            sw.WriteNode("author", datHeader.Author);
            sw.WriteNode("homepage", datHeader.Homepage);
            sw.WriteNode("comment", datHeader.Comment);
        }

        private static void writeBase(DatStreamWriter sw, DatDir baseDirIn)
        {
            DatBase[] dirChildren = baseDirIn?.ToArray();

            if (dirChildren == null)
                return;
            foreach (DatBase baseObj in dirChildren)
            {
                if (baseObj is DatDir baseDir)
                {
                    if (baseDir.DGame != null)
                    {
                        DatGame g = baseDir.DGame;
                        sw.WriteLine(@"");
                        sw.WriteLine(@"game (", 1);

                        sw.WriteName("name", baseDir.Name);


                        writeBase(sw, baseDir);


                        sw.WriteLine(@")", -1);
                    }

                    continue;
                }

                if (baseObj is DatFile baseRom)
                {
                    // if (baseRom.Name.EndsWith("/"))
                    // {
                    //    // skip all DIRs
                    // }
                    // else
                    // {
                    sw.Write(@"file (");

                    sw.WriteItem("name", baseRom.Name, true);
                    sw.WriteItem("size", baseRom.Size);

                    if (baseObj.DateModified != null && baseObj.DateModified != StructuredZip.TrrntzipDateTime)
                        sw.WriteItem("date", CompressUtils.zipDateTimeToString(baseObj.DateModified));

                    sw.WriteItem("crc", baseRom.CRC);
                    sw.WriteItem("sha1", baseRom.SHA1);
                    sw.WriteItem("sha256", baseRom.SHA256);
                    sw.WriteItem("md5", baseRom.MD5);

                    sw.WriteEnd(" )");
                    // }
                }
            }
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
                _sw = new System.IO.StreamWriter(stOut);
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
                WriteLine(name + ": " + value);
            }
            public void WriteName(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                WriteLine(name + @" """ + value + @".zip""");
            }

            public void WriteItem(string name, string value,bool force=false)
            {
                if (string.IsNullOrWhiteSpace(value) && !force)
                    return;
                _sw.Write(@" " + name + @" " + value);
            }
            public void WriteItem(string name, ulong? value)
            {
                if (value == null)
                    return;
                _sw.Write(@" " + name + @" " + value);
            }
            public void WriteItem(string name, byte[] value)
            {
                if (value == null)
                    return;
                _sw.Write(@" " + name + @" " + ByteToStr(value));
            }
        }
    }
}
