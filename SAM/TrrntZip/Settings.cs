using Compress.StructuredZip;
using System.ComponentModel;

namespace TrrntZip
{
    public enum InputZipType
    {
        [Description("ZIP")]
        Zip,
        [Description("7Z")]
        SevenZip,
        [Description("ZIP & 7Z")]
        Archive,
        [Description("Files")]
        File,
        [Description("Directories")]
        Directory, 
        [Description("All")]
        All
    }

    public class Settings
    {
        public bool VerboseLogging = true;
        public bool Repair = false;
        public bool DryRun = false;
        public InputZipType InZip = InputZipType.Zip;
        public ZipStructure OutZip = ZipStructure.ZipTrrnt;
        public object lockObj = new object();
    }
}