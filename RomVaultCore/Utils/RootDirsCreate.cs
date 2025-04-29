using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.Utils
{
    public static class RootDirsCreate
    {
        public static void CheckDatRoot()
        {
            try
            {
                if (!Directory.Exists(Settings.rvSettings.DatRoot))
                    Directory.CreateDirectory(Settings.rvSettings.DatRoot);
            }
            catch { }
        }
        public static void CheckRomRoot()
        {
            try
            {
                string lDir = DB.DirRoot.Child(0).FullName;
                if (!Directory.Exists(lDir))
                    Directory.CreateDirectory(lDir);
            }
            catch { }
        }
        public static void CheckToSort()
        {
            try
            {
                if (!Directory.Exists(DB.GetToSortPrimary().Name))
                    Directory.CreateDirectory(DB.GetToSortPrimary().Name);
            }
            catch { }
        }
    }
}
