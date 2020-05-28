using RVIO;

namespace Compress.Utils
{
    public static class DirUtil
    {
        public static void CreateDirForFile(string sFilename)
        {
            string strTemp = Path.GetDirectoryName(sFilename);

            if (string.IsNullOrEmpty(strTemp))
            {
                return;
            }

            if (Directory.Exists(strTemp))
            {
                return;
            }

            Directory.CreateDirectory(strTemp);
        }
    }
}