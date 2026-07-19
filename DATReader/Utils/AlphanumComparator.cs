using DATReader.DatStore;
using System;
using System.Collections;
using System.Globalization;

namespace DATReader.Utils
{



    public class IAlphanumComparator : IComparer
    {
        public int Compare(object x, object y)
        {
            DatDir d1 = x as DatDir;
            if (d1 == null)
                return 0;

            DatDir d2 = y as DatDir;
            if (d2 == null)
                return 0;

            return AlphanumComparator.CompareString(d1.Name, d2.Name);
        }
    }

    public static class AlphanumComparator
    {
        public static int CompareWithDirs(string s1, string s2)
        {
            if (s1 == null)
                return 0;

            if (s2 == null)
                return 0;

            bool ns1 = s1.Contains("\\");
            bool ns2 = s2.Contains("\\");

            if (ns1 && !ns2)
                return -1;
            if (ns2 && !ns1)
                return 1;
            if (ns1 && ns2)
            {
                string ts1 = s1.Substring(0, s1.IndexOf("\\"));
                string ts2 = s2.Substring(0, s2.IndexOf("\\"));
                if (ts1 == ts2)
                {
                    ts1 = s1.Substring(s1.IndexOf("\\") + 1);
                    ts2 = s2.Substring(s2.IndexOf("\\") + 1);
                }

                s1 = ts1;
                s2 = ts2;
            }

            return CompareString(s1, s2);
        }


        public static int CompareString(string s1, string s2)
        {
            int len1 = s1.Length;
            int len2 = s2.Length;
            int marker1 = 0;
            int marker2 = 0;

            // Walk through the strings with two markers.
            while (marker1 < len1 && marker2 < len2)
            {
                int start1 = marker1;
                int start2 = marker2;
                bool chunk1IsDigit = AdvanceChunk(ref marker1, s1);
                bool chunk2IsDigit = AdvanceChunk(ref marker2, s2);

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                int result;
                if (chunk1IsDigit && chunk2IsDigit)
                {
                    result = CompareTwoNumericStrings(
                        s1, start1, marker1 - start1,
                        s2, start2, marker2 - start2);
                }
                else
                {
                    result = CultureInfo.CurrentCulture.CompareInfo.Compare(
                        s1, start1, marker1 - start1,
                        s2, start2, marker2 - start2,
                        CompareOptions.None);
                }

                if (result != 0)
                {
                    return result;
                }
            }
            return s1.Length - s2.Length;
        }

        private static int CompareTwoNumericStrings(string str1, int start1, int length1, string str2, int start2, int length2)
        {
            int maxLength = Math.Max(length1, length2);
            for (int i = 0; i < maxLength; i++)
            {
                int index1 = i - (maxLength - length1);
                int index2 = i - (maxLength - length2);
                char c1 = index1 < 0 ? '0' : str1[start1 + index1];
                char c2 = index2 < 0 ? '0' : str2[start2 + index2];

                int result = c1.CompareTo(c2);
                if (result != 0)
                    return result;
            }
            return 0;
        }

        private static bool AdvanceChunk(ref int marker, string s)
        {
            // Walk through all following characters that are digits or
            // characters in a string starting at the appropriate marker.
            bool isDigit = IsDigit(s[marker]);
            do
            {
                marker++;
            } while (marker < s.Length && IsDigit(s[marker]) == isDigit);
            return isDigit;
        }

        private static bool IsDigit(char c)
        {
            return c >= 48 && c <= 57;
        }
    }

}
