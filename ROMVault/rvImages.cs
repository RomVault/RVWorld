using Compress.ZipFile;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ROMVault
{
    public static class rvImages
    {
        private static Dictionary<string, Bitmap> bmps = new Dictionary<string, Bitmap>();
        public static Bitmap GetBitmap(string bitmapName, bool duplicate = true)
        {
            if (bmps.TryGetValue(bitmapName, out Bitmap bmp))
            {
                return duplicate ? new Bitmap(bmp) : bmp;
            }

            if (File.Exists($"graphics.zip"))
            {
                Zip zf = new Zip();
                zf.ZipFileOpen("graphics.zip", -1, true);
                for (int i = 0; i < zf.LocalFilesCount; i++)
                {
                    if (zf.GetFileHeader(i).Filename == bitmapName + ".png")
                    {
                        zf.ZipFileOpenReadStream(i, out Stream stream, out ulong streamSize);
                        byte[] bBmp = new byte[(int)streamSize];
                        stream.Read(bBmp, 0, (int)streamSize);
                        Bitmap bmpf;
                        using (MemoryStream ms = new MemoryStream(bBmp))
                            bmpf = new Bitmap(ms);
                        bmps.Add(bitmapName, new Bitmap(bmpf));
                        zf.ZipFileCloseReadStream();
                        zf.ZipFileClose();
                        return bmpf;

                    }
                }
                zf.ZipFileClose();
            }

            if (File.Exists($"graphics\\{bitmapName}.png"))
            {
                Bitmap bmpf = new Bitmap($"graphics\\{bitmapName}.png");
                bmps.Add(bitmapName, new Bitmap(bmpf));
                return bmpf;
            }


            object bmObj = rvImages1.ResourceManager.GetObject(bitmapName);

            Bitmap bm = null;
            if (bmObj != null)
            {
                bm = (Bitmap)bmObj;
                bmps.Add(bitmapName, new Bitmap(bm));
            }

            return bm;
        }
    }
}