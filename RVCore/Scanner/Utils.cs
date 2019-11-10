using RVCore.RvDB;
using Path = RVIO.Path;

namespace RVCore.Scanner
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

        public static bool IschdmanScanned(RvFile tFile)
        {
            if (!tFile.FileStatusIs(FileStatus.AltSHA1FromHeader))
                return true;

            if (tFile.GotStatus == GotStatus.Corrupt)
            {
                return true;
            }

            return tFile.FileStatusIs(FileStatus.AltSHA1Verified);
        }

        public static void ChdManCheck(RvFile tFile, string directory,  ThreadWorker thWrk, ref bool fileErrorAbort)
        {
            string filename = Path.Combine(directory, tFile.Name);

            if (tFile.FileStatusIs(FileStatus.AltSHA1FromHeader | FileStatus.AltSHA1Verified))
            {
                return;
            }
            thWrk.Report(new bgwText2(filename));

            CHD.CHDManCheck res = CHD.ChdmanCheck(filename, thWrk, out string error);
            switch (res)
            {
                case CHD.CHDManCheck.Good:
                    tFile.FileStatusSet(
                        (tFile.AltSHA1 != null ? FileStatus.AltSHA1Verified : 0) |
                        (tFile.AltMD5 != null ? FileStatus.AltMD5Verified : 0)
                    );
                    return;
                case CHD.CHDManCheck.Corrupt:
                    thWrk.Report(new bgwShowError(filename, error));
                    tFile.GotStatus = GotStatus.Corrupt;
                    return;
                case CHD.CHDManCheck.CHDReturnError:
                case CHD.CHDManCheck.CHDUnknownError:
                    thWrk.Report(new bgwShowError(filename, error));
                    return;
                case CHD.CHDManCheck.ChdmanNotFound:
                    return;
                case CHD.CHDManCheck.CHDNotFound:
                    ReportError.Show("File: " + filename + " Error: Not Found scan Aborted.");
                    fileErrorAbort = true;
                    return;
                default:
                    ReportError.UnhandledExceptionHandler(error);
                    return;
            }
        }
    }
}
