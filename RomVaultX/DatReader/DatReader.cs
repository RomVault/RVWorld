using System;
using System.ComponentModel;
using System.IO;
using System.Xml;
using RomVaultX.DB;
using FileStream = RVIO.FileStream;

namespace RomVaultX.DatReader
{
    internal static class DatReader
    {
        private static BackgroundWorker _bgw;

        public static bool ReadDat(string fullname, BackgroundWorker bgw, out RvDat rvDat)
        {
            _bgw = bgw;

            rvDat = null;

            Console.WriteLine("Reading " + fullname);

            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
                return false;
            }


            StreamReader myfile = new StreamReader(fs, Program.Enc);
            string strLine = myfile.ReadLine();
            myfile.Close();
            fs.Close();
            fs.Dispose();

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

            else if ((strLine.ToLower().IndexOf("clrmamepro", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("romvault", StringComparison.Ordinal) >= 0) || (strLine.ToLower().IndexOf("game", StringComparison.Ordinal) >= 0))
            {
                if (!DatCmpReader.ReadDat(fullname, out rvDat))
                {
                    return false;
                }
            }
            else if (strLine.ToLower().IndexOf("doscenter", StringComparison.Ordinal) >= 0)
            {
                //    if (!DatDOSReader.ReadDat(datFullName))
                //        return;
            }
            else
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, "Invalid DAT File"));
                return false;
            }

            return true;
        }


        private static bool ReadXMLDat(string fullname, out RvDat rvDat)
        {
            rvDat = null;
            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
                return false;
            }

            XmlDocument doc = new XmlDocument {XmlResolver = null};
            try
            {
                doc.Load(fs);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                _bgw.ReportProgress(0, new bgwShowError(fullname, string.Format("Error Occured Reading Dat:\r\n{0}\r\n", e.Message)));
                return false;
            }
            fs.Close();
            fs.Dispose();

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