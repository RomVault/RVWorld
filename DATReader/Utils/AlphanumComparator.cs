using DATReader.DatStore;
using System;
using System.Collections;

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
                // Collect char arrays.
                char[] chunk1 = GetChunk(ref marker1, s1);
                char[] chunk2 = GetChunk(ref marker2, s2);

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                string str1 = new string(chunk1);
                string str2 = new string(chunk2);

                int result;
                if (IsDigit(chunk1[0]) && IsDigit(chunk2[0]))
                {
                    result = compareTwoNumericString(str1, str2);
                }
                else
                {
                    result = str1.CompareTo(str2);
                }

                if (result != 0)
                {
                    return result;
                }
            }
            return s1.Length - s2.Length;
        }

        private static int compareTwoNumericString(string str1, string str2)
        {
            int pos1 = 0;
            while (pos1 + 1 < str1.Length && str1[pos1 + 1] != 0) { pos1++; }
            int pos2 = 0;
            while (pos2 + 1 < str2.Length && str2[pos2 + 1] != 0) { pos2++; }


            int maxlen = Math.Max(pos1, pos2);

            int tpos1 = pos1 - maxlen;
            int tpos2 = pos2 - maxlen;


            while (true)
            {
                char c1 = tpos1 < 0 ? '0' : str1[tpos1];
                char c2 = tpos2 < 0 ? '0' : str2[tpos2];

                int result = c1.CompareTo(c2);
                if (result != 0)
                    return result;
                tpos1++;
                tpos2++;
                if (tpos1 > pos1)
                    return 0;
            }
        }

        private static char[] GetChunk(ref int marker, string s)
        {
            // Walk through all following characters that are digits or
            // characters in a string starting at the appropriate marker.
            char[] space = new char[s.Length];
            int loc = 0;
            char c = s[marker];
            do
            {
                space[loc++] = c;
                marker++;

                if (marker < s.Length)
                {
                    c = s[marker];
                }
                else
                {
                    break;
                }
            } while (IsDigit(c) == IsDigit(space[0]));
            return space;
        }

        private static bool IsDigit(char c)
        {
            return c >= 48 && c <= 57;
        }
    }

}
