using RomVaultCore.RvDB;
using Path = RVIO.Path;

namespace RomVaultCore.Scanner
{
    public static class Utils
    {

        public static bool IsDeepScanned(RvFile tBase)
        {
            RvFile tFile = tBase;
            if (tFile.IsFile)
            {
                return tFile.FileStatusIs(FileStatus.SizeVerified) &&
                       tFile.FileStatusIs(FileStatus.CRCVerified) &&
                       tFile.FileStatusIs(FileStatus.SHA1Verified) &&
                       tFile.FileStatusIs(FileStatus.MD5Verified);
            }

            // is a dir
            RvFile tZip = tBase;
            for (int i = 0; i < tZip.ChildCount; i++)
            {
                RvFile zFile = tZip.Child(i);
                if (zFile.IsFile && zFile.GotStatus == GotStatus.Got &&
                    (!zFile.FileStatusIs(FileStatus.SizeVerified) || !zFile.FileStatusIs(FileStatus.CRCVerified) || !zFile.FileStatusIs(FileStatus.SHA1Verified) || !zFile.FileStatusIs(FileStatus.MD5Verified)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
