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


            while ((strTemp.Length > 0) && !Directory.Exists(strTemp))
            {
                int pos = strTemp.LastIndexOf(Path.DirectorySeparatorChar);
                if (pos < 0)
                {
                    pos = 0;
                }
                strTemp = strTemp.Substring(0, pos);
            }

            while (sFilename.IndexOf(Path.DirectorySeparatorChar, strTemp.Length + 1) > 0)
            {
                strTemp = sFilename.Substring(0, sFilename.IndexOf(Path.DirectorySeparatorChar, strTemp.Length + 1));
                Directory.CreateDirectory(strTemp);
            }
        }
    }
}