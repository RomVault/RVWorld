using System;
using System.Collections.Generic;

namespace TrrntZipUI
{
    public class TzFile
    {
        public readonly string Filename;

        public TzFile(string filename)
        {
            Filename = filename;
        }
    }

    public class FileList
    {
        private readonly List<TzFile> _tzFiles = new List<TzFile>();

        public void Clear()
        {
            _tzFiles.Clear();
        }

        public int Count()
        {
            return _tzFiles.Count;
        }

        public TzFile Get(int index)
        {
            if ((index < 0) || (index >= _tzFiles.Count))
            {
                return null;
            }
            return _tzFiles[index];
        }

        public void Add(int index, TzFile value)
        {
            _tzFiles.Insert(index, value);
        }

        public int Search(TzFile value, out int index)
        {
            int intBottom = 0;
            int intTop = _tzFiles.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop)/2;

                intRes = Compare(value, _tzFiles[intMid]);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while ((index > 0) && (intRes1 == 0))
                {
                    intRes1 = Compare(value, _tzFiles[index - 1]);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;
        }

        private static int Compare(TzFile var1, TzFile var2)
        {
            return string.Compare(var1.Filename, var2.Filename, StringComparison.Ordinal);
        }
    }
}