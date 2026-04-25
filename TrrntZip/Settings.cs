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

    public class Settings
    {
        public bool VerboseLogging = true;
        public bool ForceReZip = false;
        public bool CheckOnly = false;
        public zipType InZip = zipType.zip;
        public ZipStructure OutZip = ZipStructure.ZipTrrnt;
        public object lockObj = new object();
    }
}