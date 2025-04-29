using System;
using Compress;
using DATReader.DatStore;
using RVIO;

namespace DATReader.DatWriter
{
    public static class DatMAMEXMLWriter
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
            sw.WriteLine("<?xml version=\"1.0\"?>");
            sw.WriteLine(
@"<!DOCTYPE mame [
<!ELEMENT mame (machine+)>
	<!ATTLIST mame build CDATA #IMPLIED>
	<!ATTLIST mame debug (yes|no) ""no"">
	<!ELEMENT machine (description, year?, manufacturer?, biosset*, rom*, disk*, device_ref*, sample*, chip*, display*, sound?, input?, dipswitch*, configuration*, port*, adjuster*, driver?, feature*, device*, slot*, softwarelist*, ramoption*)>
		<!ATTLIST machine name CDATA #REQUIRED>
		<!ATTLIST machine isbios (yes|no) ""no"">
		<!ATTLIST machine isdevice (yes|no) ""no"">
		<!ATTLIST machine runnable (yes|no) ""yes"">
		<!ATTLIST machine cloneof CDATA #IMPLIED>
		<!ATTLIST machine romof CDATA #IMPLIED>
		<!ELEMENT description (#PCDATA)>
		<!ELEMENT year (#PCDATA)>
		<!ELEMENT manufacturer (#PCDATA)>
		<!ELEMENT rom EMPTY>
			<!ATTLIST rom name CDATA #REQUIRED>
			<!ATTLIST rom bios CDATA #IMPLIED>
			<!ATTLIST rom size CDATA #REQUIRED>
			<!ATTLIST rom crc CDATA #IMPLIED>
			<!ATTLIST rom sha1 CDATA #IMPLIED>
			<!ATTLIST rom merge CDATA #IMPLIED>
			<!ATTLIST rom region CDATA #IMPLIED>
			<!ATTLIST rom offset CDATA #IMPLIED>
			<!ATTLIST rom status (baddump|nodump|good) ""good"">
			<!ATTLIST rom optional (yes|no) ""no"">
		<!ELEMENT disk EMPTY>
			<!ATTLIST disk name CDATA #REQUIRED>
			<!ATTLIST disk sha1 CDATA #IMPLIED>
			<!ATTLIST disk merge CDATA #IMPLIED>
			<!ATTLIST disk region CDATA #IMPLIED>
			<!ATTLIST disk index CDATA #IMPLIED>
			<!ATTLIST disk writable (yes|no) ""no"">
			<!ATTLIST disk status (baddump|nodump|good) ""good"">
			<!ATTLIST disk optional (yes|no) ""no"">
		<!ELEMENT device_ref EMPTY>
			<!ATTLIST device_ref name CDATA #REQUIRED>
]>

");

            sw.WriteLine($@"<mame build=""{datHeader.Name}"">");

            writeBase(sw, datHeader.BaseDir);

            sw.WriteLine("</mame>", -1);
        }

        private static void writeBase(DatStreamWriter sw, DatDir baseDirIn)
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
                        sw.Write(@"<machine");

                        sw.WriteItem("name", baseDir.Name);

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
                        sw.WriteNode("year", g.Year);
                        sw.WriteNode("manufacturer", g.Manufacturer);

                        writeBase(sw, baseDir);

                        if (g.device_ref != null)
                        {
                            foreach (string d in g.device_ref)
                            {
                                sw.WriteLine($@"<device_ref name=""{d}""/>");
                            }
                        }

                        sw.WriteLine(@"</machine>", -1);
                    }
                    else
                    {
                        sw.WriteLine(@"<dir name=""" + Etxt(baseDir.Name) + @""">", 1);
                        writeBase(sw, baseDir);
                        sw.WriteLine(@"</dir>", -1);
                    }
                    continue;
                }

                if (baseObj is DatFile baseRom)
                {
                    if (baseRom.isDisk)
                        sw.Write(@"<disk");
                    else
                        sw.Write(@"<rom");

                    sw.WriteItem("name", baseRom.Name);
                    sw.WriteItem("merge", baseRom.Merge);
                    sw.WriteItem("size", baseRom.Size);
                    sw.WriteItem("crc", baseRom.CRC);
                    sw.WriteItem("sha1", baseRom.SHA1);
                    sw.WriteItem("md5", baseRom.MD5);

                    if (baseObj.DateModified != null && baseObj.DateModified != Compress.StructuredZip.StructuredZip.TrrntzipDateTime)
                        sw.WriteItem("date", CompressUtils.zipDateTimeToString(baseObj.DateModified));

                    if (baseRom.Status != null && baseRom.Status.ToLower() != "good")
                        sw.WriteItem("status", baseRom.Status);
                    sw.WriteEnd("/>");
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
                if (c == 127) { ret += "&#7f;"; continue; }
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
