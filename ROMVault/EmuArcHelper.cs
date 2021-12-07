using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Compress;
using Compress.ZipFile;
using RomVaultCore.RvDB;
using File = RVIO.File;
using Path = RVIO.Path;

namespace ROMVault
{
    public static class EmuArcHelper
    {

        private static bool LoadBytes(RvFile tGame, string filename, out byte[] memBuffer)
        {
            memBuffer = null;

            int cCount = tGame.ChildCount;
            if (cCount == 0)
                return false;

            int found = -1;
            for (int i = 0; i < cCount; i++)
            {
                RvFile rvf = tGame.Child(i);
                if (rvf.Name != filename || rvf.GotStatus != GotStatus.Got)
                    continue;
                found = i;
                break;
            }

            if (found == -1)
                return false;

            try
            {
                switch (tGame.FileType)
                {
                    case FileType.Zip:
                        {
                            RvFile imagefile = tGame.Child(found);
                            if (imagefile.ZipFileHeaderPosition == null)
                                return false;

                            Zip zf = new Zip();
                            if (zf.ZipFileOpen(tGame.FullNameCase, tGame.FileModTimeStamp, false) != ZipReturn.ZipGood)
                                return false;

                            if (zf.ZipFileOpenReadStreamQuick((ulong)imagefile.ZipFileHeaderPosition, false,
                                    out Stream stream, out ulong streamSize, out ushort _) != ZipReturn.ZipGood)
                            {
                                zf.ZipFileClose();
                                return false;
                            }

                            memBuffer = new byte[streamSize];
                            stream.Read(memBuffer, 0, (int)streamSize);
                            zf.ZipFileClose();
                            return true;
                        }
                    case FileType.Dir:
                        {
                            string dirPath = tGame.FullNameCase;
                            string artwork = Path.Combine(dirPath, filename);
                            if (!File.Exists(artwork))
                                return false;

                            RVIO.FileStream.OpenFileRead(artwork, out Stream stream);
                            memBuffer = new byte[stream.Length];
                            stream.Read(memBuffer, 0, memBuffer.Length);
                            stream.Close();
                            stream.Dispose();
                            return true;
                        }
                    default:
                        return false;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

        }


        public static bool TryLoadImage(this PictureBox pic, RvFile tGame, string filename)
        {
            return pic.LoadImage(tGame, filename + ".png") || pic.LoadImage(tGame, filename + ".jpg");
        }


        public static bool LoadImage(this PictureBox picBox, RvFile tGame, string filename)
        {
            picBox.ClearImage();
            if (!LoadBytes(tGame, filename, out byte[] memBuffer))
                return false;
            using (MemoryStream ms = new MemoryStream(memBuffer, false))
            {
                picBox.Image = Image.FromStream(ms);
            }

            return true;
        }

        public static void ClearImage(this PictureBox picBox)
        {
            if (picBox.Image != null)
            {
                Image tmp = picBox.Image;
                tmp.Dispose();
            }

            picBox.Image = null;
        }



        public static bool LoadText(this RichTextBox txtBox, RvFile tGame, string filename)
        {
            txtBox.ClearText();
            if (!LoadBytes(tGame, filename, out byte[] memBuffer))
                return false;

            string txt = Encoding.ASCII.GetString(memBuffer);
            txt = txt.Replace("\r\n", "\r\n\r\n");
            txtBox.Text = txt;

            return true;
        }

        public static void ClearText(this RichTextBox txtBox)
        {
            txtBox.Text = "";
        }

    }
}


