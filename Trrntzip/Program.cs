using Compress;

namespace TrrntZip
{
    public enum zipType
    {
        zip,
        sevenzip,
        archive,
        file,
        dir,
        all
    }

    public static class Program
    {
        public static bool VerboseLogging = true;
        public static bool ForceReZip = false;
        public static bool CheckOnly = false;
        public static zipType InZip = zipType.zip;
        public static ZipStructure OutZip = ZipStructure.ZipTrrnt;
        public static object lockObj = new object();
    }
}