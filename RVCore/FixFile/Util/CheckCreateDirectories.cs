using RVCore.RvDB;
using RVIO;

namespace RVCore.FixFile.Util
{
  public static partial class FixFileUtils
    {
        //Recurse back up the RvFile Parents, checking that the Directories exists.
        //and are marked as got in the DB
        public static void CheckCreateDirectories(RvFile file)
        {
            if (file == DB.DirTree)
            {
                return;
            }

            string parentDir = file.FullName;
            if (Directory.Exists(parentDir) && file.GotStatus == GotStatus.Got)
            {
                return;
            }

            CheckCreateDirectories(file.Parent);
            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            file.GotStatus = GotStatus.Got;
        }
    }
}
