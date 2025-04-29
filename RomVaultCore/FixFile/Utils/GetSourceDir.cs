using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile.Utils
{
    public static partial class FixFileUtils
    {
        public static void GetSourceDir(RvFile fileIn, out string sourceDir, out string sourceFile)
        {
            string ts = fileIn.Parent.TreeFullName;
            if (fileIn.FileType == FileType.FileZip || fileIn.FileType == FileType.FileSevenZip)
            {
                sourceDir = Path.GetDirectoryName(ts);
                sourceFile = Path.GetFileName(ts);
            }
            else
            {
                sourceDir = ts;
                sourceFile = "";
            }

        }

    }
}
