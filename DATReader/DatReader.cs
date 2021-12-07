using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Xml;
using DATReader.DatReader;
using DATReader.DatStore;
using File = RVIO.File;
using FileStream = RVIO.FileStream;

namespace DATReader
{
    public delegate void ReportError(string filename, string error);

    public static class DatRead
    {
        public static readonly Encoding Enc = Encoding.GetEncoding(28591);
        public static bool ReadDat(string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;

            System.Diagnostics.Debug.WriteLine("Reading : " + fullname);

            string strLine = null;
            using (StreamReader myfile = File.OpenText(fullname, Enc))
            {
                if (myfile == null)
                    return false;

                while (string.IsNullOrWhiteSpace(strLine) && !myfile.EndOfStream)
                {
                    strLine = myfile.ReadLine();
                }
                myfile.Close();
            }


            if (strLine == null)
            {
                return false;
            }


            if (strLine.ToLower().IndexOf("xml", StringComparison.Ordinal) >= 0)
            {
                if (!ReadXMLDat(fullname, ErrorReport, out rvDat))
                {
                    return false;
                }
            }
            else if ((strLine.ToLower().IndexOf("clrmamepro", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("clrmame", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("romvault", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("game", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("machine", StringComparison.Ordinal) >= 0))
            {
                if (!DatCmpReader.ReadDat(fullname, ErrorReport, out rvDat))
                {
                    return false;
                }
            }
            else if (strLine.ToLower().IndexOf("doscenter", StringComparison.Ordinal) >= 0)
            {
                if (!DatDOSReader.ReadDat(fullname, ErrorReport, out rvDat))
                    return false;
            }
            else if (strLine.ToLower().IndexOf("[credits]", StringComparison.Ordinal) >= 0)
            {
                if (!DatROMCenterReader.ReadDat(fullname, ErrorReport, out rvDat))
                    return false;
            }
            else if (strLine.ToLower().IndexOf("raine (680x0 arcade emulation)",StringComparison.Ordinal)>=0)
            {
                if (!DatCmpReader.ReadDat(fullname, ErrorReport, out rvDat))
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

        public static bool ReadXMLDatFromStream(Stream fs, string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;
            XmlDocument doc = new XmlDocument { XmlResolver = null };
            try
            {
                doc.Load(fs);
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

        private static bool ReadXMLDat(string fullname, ReportError ErrorReport, out DatHeader rvDat)
        {
            rvDat = null;
            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                ErrorReport?.Invoke(fullname, errorCode + ": " + new Win32Exception(errorCode).Message);
                return false;
            }

            bool retVal = false;
            try
            {
                retVal = ReadXMLDatFromStream(fs, fullname, ErrorReport, out rvDat);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                ErrorReport?.Invoke(fullname, $"Error Occured Reading Dat:\r\n{e.Message}\r\n");
                return false;
            }
            fs.Close();
            fs.Dispose();

            return retVal;
        }
    }
}