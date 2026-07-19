using System;
using System.Text;
using RVIO;

namespace DATReader.Utils
{
    internal class DatFileLoader : IDisposable
    {
        private System.IO.StreamReader _streamReader;
        private string _line = "";
        public string Next;
        public int LineNumber = 0;

        public string Filename { get; private set; }

        public int LoadDat(System.IO.Stream fStream, string strFilename)
        {
            Filename = strFilename;
            _streamReader = new System.IO.StreamReader(fStream, Encoding.UTF8, true, 4096, true);
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
        public string GnRestQ()
        {
            string strret = _line;
            _line = "";
            Next = strret;
            return strret;
        }

        public string GnNameToSize()
        {
            int sizePos = _line.LastIndexOf(" size ", StringComparison.OrdinalIgnoreCase);
            string strret = (sizePos == 0) ? "" : _line.Substring(0, sizePos);
            _line = _line.Substring(sizePos + 1);
            Next = strret;
            return strret;
        }

        public string Gn()
        {
            string ret;
            while (string.IsNullOrWhiteSpace(_line) && !_streamReader.EndOfStream)
            {
                _line = _streamReader.ReadLine();
                LineNumber++;

                _line = (_line ?? "").Replace('\t', ' ');

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

                string lineTrimmedStart = _line.TrimStart();
                if (lineTrimmedStart.Length > 1 && lineTrimmedStart[0] == '#')
                {
                    _line = "";
                }
                else if (lineTrimmedStart.Length > 1 && lineTrimmedStart[0] == ';')
                {
                    _line = "";
                }
                _line = _line.Trim() + " ";
            }

            if (!string.IsNullOrWhiteSpace(_line))
            {
                int intS;
                if (_line[0] == '"')
                {
                    intS = _line.IndexOf('"', 1);
                    if (intS < 0)
                        intS = _line.Length;
                    ret = _line.Substring(1, intS - 1);
                    _line = intS < _line.Length
                        ? _line.Substring(intS + 1).Trim(' ')
                        : "";
                }
                else
                {
                    intS = _line.IndexOf(' ');
                    if (intS < 0)
                        intS = _line.Length;
                    ret = _line.Substring(0, intS);
                    _line = intS < _line.Length
                        ? _line.Substring(intS).Trim(' ')
                        : "";
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
