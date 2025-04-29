using System;

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
        public static int StringCompare(string string1, string string2)
        {
            return Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }


        /// <summary>
        /// I Dont think this should be seen public ever!!
        /// </summary>
        /// <param name="string1">trrntzip filename 1</param>
        /// <param name="string2">trrntzip filename 2</param>
        /// <returns></returns>
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
        public static int TrrntZipStringCompareCase(string string1, string string2)
        {
            int res = TrrntZipStringCompare(string1, string2);
            return res != 0 ? res : Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }




        public static int DirectoryNameCompare(string string1, string string2)
        {
            return Math.Sign(string.Compare(string1.ToLower(), string2.ToLower(), StringComparison.Ordinal));
        }
        public static int DirectoryNameCompareCase(string string1, string string2)
        {
            int res = DirectoryNameCompare(string1, string2);
            return res != 0 ? res : Math.Sign(string.Compare(string1, string2, StringComparison.Ordinal));
        }


        public static int Trrnt7ZipStringCompare(string string1, string string2)
        {

            splitFilename(string1, out string path1, out string name1, out string ext1);
            splitFilename(string2, out string path2, out string name2, out string ext2);

            int res = Math.Sign(string.Compare(ext1, ext2, StringComparison.Ordinal));
            if (res != 0)
                return res;

            res = Math.Sign(string.Compare(name1, name2, StringComparison.Ordinal));
            if (res != 0)
                return res;

            res = Math.Sign(string.Compare(path1, path2, StringComparison.Ordinal));
            if (res != 0)
                return res;


            return 0;
        }


        private static void splitFilename(string filename, out string path, out string name, out string ext)
        {
            int dirIndex = filename.LastIndexOf('/');

            if (dirIndex >= 0)
            {
                path = filename.Substring(0, dirIndex);
                name = filename.Substring(dirIndex + 1);
            }
            else
            {
                path = "";
                name = filename;
            }

            int extIndex = name.LastIndexOf('.');

            if (extIndex >= 0)
            {
                ext = name.Substring(extIndex + 1);
                name = name.Substring(0, extIndex);
            }
            else
            {
                ext = "";
            }
        }


        public delegate void ErrorOut(string message);

    }
}