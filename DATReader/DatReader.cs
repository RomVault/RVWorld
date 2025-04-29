using System;
using System.IO;
using System.Text;
using System.Xml;
using DATReader.DatReader;
using DATReader.DatStore;
using File = RVIO.File;

namespace DATReader
{
    public delegate void ReportError(string filename, string error);

    public static class DatRead
    {
        public static bool ReadDat(string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;

            System.Diagnostics.Debug.WriteLine("Reading : " + fullname);

            byte[] buffer = null;
            try
            {
                if (Path.GetExtension(fullname.ToLower()) == ".datz")
                {
                    Compress.gZip.gZip gz = new Compress.gZip.gZip();
                    gz.ZipFileOpen(fullname);
                    buffer = new byte[gz.GetFileHeader(0).UncompressedSize];
                    gz.ZipFileOpenReadStream(0, out Stream stream, out ulong streamSize);
                    stream.Read(buffer, 0, (int)streamSize);
                    gz.ZipFileCloseReadStream();
                    gz.ZipFileClose();
                }
                else
                    buffer = File.ReadAllBytes(fullname);

                return ReadDat(buffer, fullname, ErrorReport, out rvDat);
            }
            catch (Exception e)
            {
                ErrorReport?.Invoke(fullname, $"Error Occured Opening Dat:\r\n{e.Message}\r\n");
                return false;
            }
        }

        public static bool ReadDat(byte[] buffer, string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;

            using (MemoryStream mStream = new MemoryStream(buffer, false))
            {
                string strLine = null;
                using (StreamReader myfile = new StreamReader(mStream, Encoding.UTF8, true, 1024, true))
                {
                    if (myfile == null)
                        return false;

                    while (string.IsNullOrWhiteSpace(strLine) && !myfile.EndOfStream)
                    {
                        strLine = myfile.ReadLine();
                    }
                }
                mStream.Position = 0;

                if (strLine == null)
                {
                    return false;
                }


                if (strLine.ToLower().IndexOf("xml", StringComparison.Ordinal) >= 0)
                {
                    if (!ReadXMLDatFromStream(mStream, fullname, ErrorReport, out rvDat))
                    {
                        return false;
                    }
                }
                else if ((strLine.ToLower().IndexOf("clrmamepro", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("clrmame", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("romvault", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("game", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("machine", StringComparison.Ordinal) >= 0))
                {
                    if (!DatCmpReader.ReadDat(mStream, fullname, ErrorReport, out rvDat))
                    {
                        return false;
                    }
                }
                else if (strLine.ToLower().IndexOf("doscenter", StringComparison.Ordinal) >= 0)
                {
                    if (!DatDOSReader.ReadDat(mStream, fullname, ErrorReport, out rvDat))
                        return false;
                }
                else if (strLine.ToLower().IndexOf("[credits]", StringComparison.Ordinal) >= 0)
                {
                    if (!DatROMCenterReader.ReadDat(mStream, fullname, ErrorReport, out rvDat))
                        return false;
                }
                else if (strLine.ToLower().IndexOf("raine (680x0 arcade emulation)", StringComparison.Ordinal) >= 0)
                {
                    if (!DatCmpReader.ReadDat(mStream, fullname, ErrorReport, out rvDat))
                    {
                        return false;
                    }
                }
                else
                {
                    ErrorReport?.Invoke(fullname, "Invalid DAT File");
                    return false;
                }

                return true;
            }
        }

        public static bool ReadXMLDatFromStream(Stream fs, string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
            try
            {
                XmlReaderSettings xmlReaderSettings = new XmlReaderSettings() { CheckCharacters = false, DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader reader = XmlReader.Create(fs, xmlReaderSettings))
                {
                    doc.Load(reader);
                }
            }
            catch (Exception e)
            {
                ErrorReport?.Invoke(fullname, $"Error Occured Reading Dat:\r\n{e.Message}\r\n");
                return false;
            }

            if (doc.DocumentElement == null)
            {
                return false;
            }

            XmlNode mame = doc.SelectSingleNode("mame");
            if (mame != null)
            {
                return DatXmlReader.ReadMameDat(doc, fullname, out rvDat);
            }

            XmlNode head = doc.DocumentElement?.SelectSingleNode("header");
            if (head != null)
            {
                return DatXmlReader.ReadDat(doc, fullname, out rvDat);
            }

            XmlNodeList headList = doc.SelectNodes("softwarelist");
            if (headList != null)
            {
                return DatMessXmlReader.ReadDat(doc, fullname, out rvDat);
            }

            return false;
        }

    }
}