using System;

namespace DATReader.Utils
{


    public static class AlphanumComparatorFast
    {
        public static int Compare(string s1, string s2)
        {
            if (s1 == null)
            {
                return 0;
            }

            if (s2 == null)
            {
                return 0;
            }

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

            int len1 = s1.Length;
            int len2 = s2.Length;
            int marker1 = 0;
            int marker2 = 0;

            // Walk through two the strings with two markers.
            while (marker1 < len1 && marker2 < len2)
            {
                char ch1 = s1[marker1];
                char ch2 = s2[marker2];

                // Some buffers we can build up characters in for each chunk.
                char[] space1 = new char[len1];
                int loc1 = 0;
                char[] space2 = new char[len2];
                int loc2 = 0;

                // Walk through all following characters that are digits or
                // characters in BOTH strings starting at the appropriate marker.
                // Collect char arrays.
                do
                {
                    space1[loc1++] = ch1;
                    marker1++;

                    if (marker1 < len1)
                    {
                        ch1 = s1[marker1];
                    }
                    else
                    {
                        break;
                    }
                } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

                do
                {
                    space2[loc2++] = ch2;
                    marker2++;

                    if (marker2 < len2)
                    {
                        ch2 = s2[marker2];
                    }
                    else
                    {
                        break;
                    }
                } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                string str1 = new string(space1, 0, loc1);
                string str2 = new string(space2, 0, loc2);

                int result;

                if (char.IsDigit(space1[0]) && char.IsDigit(space2[0]))
                {
                    long thisNumericChunk = long.Parse(str1);
                    long thatNumericChunk = long.Parse(str2);
                    result = thisNumericChunk.CompareTo(thatNumericChunk);
                    if (result == 0)
                    {
                        if (loc1 != loc2)
                            return loc1 - loc2;

                    }
                }
                else
                {
                    result = string.Compare(str1.ToLower(), str2.ToLower(), StringComparison.Ordinal);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return len2 - len1;
        }
    }
}