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

    public class DatRead
    {

        public ReportError ErrorReport;

        public static readonly Encoding Enc = Encoding.GetEncoding(28591);
        public bool ReadDat(string fullname, out DatHeader rvDat)
        {
            rvDat = null;

            System.Diagnostics.Debug.WriteLine("Reading : " + fullname);

            StreamReader myfile = File.OpenText(fullname, Enc);
            string strLine = null;
            while (string.IsNullOrWhiteSpace(strLine) && !myfile.EndOfStream)
            {
                strLine = myfile.ReadLine();
            }
            myfile.Close();

            if (strLine == null)
            {
                return false;
            }


            if (strLine.ToLower().IndexOf("xml", StringComparison.Ordinal) >= 0)
            {
                if (!ReadXMLDat(fullname, out rvDat))
                {
                    return false;
                }
            }
            else if ((strLine.ToLower().IndexOf("clrmamepro", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("clrmame", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("romvault", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("game", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("machine", StringComparison.Ordinal) >= 0))
            {
                DatCmpReader dcr = new DatCmpReader(ErrorReport);
                if (!dcr.ReadDat(fullname, out rvDat))
                {
                    return false;
                }
            }
            else if (strLine.ToLower().IndexOf("doscenter", StringComparison.Ordinal) >= 0)
            {
                DatDOSReader ddr = new DatDOSReader(ErrorReport);
                if (!ddr.ReadDat(fullname, out rvDat))
                    return false;
            }
            else if (strLine.ToLower().IndexOf("[credits]", StringComparison.Ordinal) >= 0)
            {
                DatROMCenterReader drcr = new DatROMCenterReader(ErrorReport);
                if (!drcr.ReadDat(fullname, out rvDat))
                    return false;
            }
            else
            {
                ErrorReport?.Invoke(fullname, "Invalid DAT File");
                return false;
            }

            return true;
        }


        private bool ReadXMLDat(string fullname, out DatHeader rvDat)
        {
            rvDat = null;
            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                ErrorReport?.Invoke(fullname, errorCode + ": " + new Win32Exception(errorCode).Message);
                return false;
            }

            XmlDocument doc = new XmlDocument { XmlResolver = null };
            try
            {
                doc.Load(fs);
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

            if (doc.DocumentElement == null)
            {
                return false;
            }


            DatXmlReader dXMLReader = new DatXmlReader();

            XmlNode mame = doc.SelectSingleNode("mame");
            if (mame != null)
            {
                return dXMLReader.ReadMameDat(doc, fullname, out rvDat);
            }

            XmlNode head = doc.DocumentElement?.SelectSingleNode("header");
            if (head != null)
            {
                return dXMLReader.ReadDat(doc, fullname, out rvDat);
            }

            XmlNodeList headList = doc.SelectNodes("softwarelist");
            if (headList != null)
            {
                DatMessXmlReader dmXMLReader = new DatMessXmlReader();
                return dmXMLReader.ReadDat(doc, fullname, out rvDat);
            }

            return false;
        }
    }
}