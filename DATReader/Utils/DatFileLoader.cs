using System;
using System.Text;
using RVIO;

namespace DATReader.DatReader
{
    internal class DatFileLoader : IDisposable
    {
        private System.IO.StreamReader _streamReader;
        private string _line = "";
        public string Next;
        public int LineNumber = 0;

        public string Filename { get; private set; }

        public int LoadDat(string strFilename, Encoding enc)
        {
            Filename = strFilename;
            _streamReader = File.OpenText(strFilename, enc);
            return 0;
        }

        public void Dispose()
        {
            _streamReader.Close();
            _streamReader.Dispose();
        }

        public bool EndOfStream()
        {
            return _streamReader.EndOfStream;
        }

        public string GnRest()
        {
            string strret = _line.Replace("\"", "");
            _line = "";
            Next = strret;
            return strret;
        }

        public string GnNameToSize()
        {
            int sizePos = _line.ToLower().LastIndexOf(" size ");
            string strret = (sizePos == 0) ? "" : _line.Substring(0, sizePos);
            _line = _line.Substring(sizePos+1);
            Next = strret;
            return strret;
        }

        public string Gn()
        {
            string ret;
            while ((_line.Trim().Length == 0) && !_streamReader.EndOfStream)
            {
                _line = _streamReader.ReadLine();
                LineNumber++;

                _line = (_line ?? "").Replace("" + (char)9, " ");
              
                int indexof = _line.IndexOf(@"//");
                if (indexof >= 0)
                {
                    _line = _line.Substring(0, indexof);
                }
                /*
                if ((_line.TrimStart().Length > 2) && (_line.TrimStart().Substring(0, 2) == @"//"))
                {
                    _line = "";
                }
                */

                if ((_line.TrimStart().Length > 1) && (_line.TrimStart().Substring(0, 1) == @"#"))
                {
                    _line = "";
                }
                if ((_line.TrimStart().Length > 1) && (_line.TrimStart().Substring(0, 1) == @";"))
                {
                    _line = "";
                }
                _line = _line.Trim() + " ";
            }

            if (_line.Trim().Length > 0)
            {
                int intS;
                if (_line.Substring(0, 1) == "\"")
                {
                    intS = (_line + "\"").IndexOf("\"", 1, StringComparison.Ordinal);
                    ret = _line.Substring(1, intS - 1);
                    _line = (_line + " ").Substring(intS + 1).Trim(new char[] { ' ' });
                }
                else
                {
                    intS = (_line + " ").IndexOf(" ", StringComparison.Ordinal);
                    ret = _line.Substring(0, intS);
                    _line = (_line + " ").Substring(intS).Trim(new char[] { ' ' });
                }
            }
            else
            {
                ret = "";
            }

            Next = ret;
            return ret;
        }

    }

}
