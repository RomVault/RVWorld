using System;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace SortMethods
{
    /*
     * (trrnt)Zips are case sensitive, but have a strange sort order.
     * They are sorted first case insensitive (for A to Z only), 
     * then if that matches they are sorted case sensitive.
     * 
     * SevenZip files are sorted case sensitive by extension, then name, then path.
     * 
     * 
     * When sorting FileStore objects they should be stored as follows:
     * 
     * Dirs:
     * Sorted by DirectoryNameCompareCase (case sensitive)  (For merging case insensitive should be used.)
     * 
     * Zip:
     * Sorted by TrrntZipStringCompareCase (case insensitive first, then case sensitive)
     * 
     * SevenZip:
     * Sorted by Trrnt7ZipStringCompare  (which is case sensistive)
     * 
     */


    public static class Sorters
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StringCompare(string string1, string string2)
        {
            return Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }


        public static int TrrntZipStringCompare(string string1, string string2)
        {
            int pos1 = 0;
            int pos2 = 0;

            for (; ; )
            {
                if (pos1 == string1.Length)
                    return pos2 == string2.Length ? 0 : -1;
                if (pos2 == string2.Length)
                    return 1;

                char byte1 = string1[pos1++];
                char byte2 = string2[pos2++];

                if (byte1 >= 65 && byte1 <= 90)
                    byte1 += (char)0x20;
                if (byte2 >= 65 && byte2 <= 90)
                    byte2 += (char)0x20;

                if (byte1 < byte2)
                    return -1;
                if (byte1 > byte2)
                    return 1;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrrntZipStringCompareCase(string string1, string string2)
        {
            int res = TrrntZipStringCompare(string1, string2);
            return res != 0 ? res : Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }




        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DirectoryNameCompare(string string1, string string2)
        {
            int compareLength = Math.Min(string1.Length, string2.Length);
            TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
            for (int i = 0; i < compareLength; i++)
            {
                char char1 = textInfo.ToLower(string1[i]);
                char char2 = textInfo.ToLower(string2[i]);
                if (char1 < char2)
                    return -1;
                if (char1 > char2)
                    return 1;
            }
            return Math.Sign(string1.Length.CompareTo(string2.Length));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DirectoryNameCompareCase(string string1, string string2)
        {
            int res = DirectoryNameCompare(string1, string2);
            return res != 0 ? res : Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }


        public static int Trrnt7ZipStringCompare(string string1, string string2)
        {
            SplitFilename(string1, out int pathLength1, out int nameStart1, out int nameLength1, out int extStart1, out int extLength1);
            SplitFilename(string2, out int pathLength2, out int nameStart2, out int nameLength2, out int extStart2, out int extLength2);

            int res = CompareSegment(string1, extStart1, extLength1, string2, extStart2, extLength2);
            if (res != 0)
                return res;

            res = CompareSegment(string1, nameStart1, nameLength1, string2, nameStart2, nameLength2);
            if (res != 0)
                return res;

            return CompareSegment(string1, 0, pathLength1, string2, 0, pathLength2);
        }


        private static void SplitFilename(string filename, out int pathLength, out int nameStart, out int nameLength, out int extStart, out int extLength)
        {
            int dirIndex = filename.LastIndexOf('/');
            if (dirIndex >= 0)
            {
                pathLength = dirIndex;
                nameStart = dirIndex + 1;
            }
            else
            {
                pathLength = 0;
                nameStart = 0;
            }

            int extIndex = -1;
            for (int i = filename.Length - 1; i >= nameStart; i--)
            {
                if (filename[i] != '.')
                    continue;
                extIndex = i;
                break;
            }

            if (extIndex >= nameStart)
            {
                nameLength = extIndex - nameStart;
                extStart = extIndex + 1;
                extLength = filename.Length - extStart;
            }
            else
            {
                nameLength = filename.Length - nameStart;
                extStart = filename.Length;
                extLength = 0;
            }
        }

        private static int CompareSegment(string string1, int start1, int length1, string string2, int start2, int length2)
        {
            int compareLength = Math.Min(length1, length2);
            int result = string.Compare(string1, start1, string2, start2, compareLength, StringComparison.Ordinal);
            if (result != 0)
                return Math.Sign(result);
            return Math.Sign(length1.CompareTo(length2));
        }


        public delegate void ErrorOut(string message);

    }
}
