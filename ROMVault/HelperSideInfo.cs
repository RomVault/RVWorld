using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodePage;
using Compress;
using Compress.ZipFile;
using RomVaultCore.RvDB;
using File = RVIO.File;
using Path = RVIO.Path;

namespace ROMVault
{
    public static class HelperSideInfo
    {
        private static Regex WildcardToRegex(string pattern)
        {
            if (pattern.ToLower().StartsWith("regex:"))
                return new Regex(pattern.Substring(6), RegexOptions.IgnoreCase);

            return new Regex("^" + Regex.Escape(pattern).
            Replace("\\*", ".*").
            Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }


        private static bool LoadBytes(RvFile tGame, string filename, out byte[] memBuffer)
        {
            memBuffer = null;

            Regex rSearch = WildcardToRegex(filename);

            int cCount = tGame.ChildCount;
            if (cCount == 0)
                return false;

            int found = -1;
            for (int i = 0; i < cCount; i++)
            {
                RvFile rvf = tGame.Child(i);
                if (rvf.GotStatus != GotStatus.Got)
                    continue;
                if (!rSearch.IsMatch(rvf.Name)) 
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

                            if (zf.ZipFileOpenReadStreamFromLocalHeaderPointer((ulong)imagefile.ZipFileHeaderPosition, false,
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
                            RvFile imagefile = tGame.Child(found);
                            string artwork = imagefile.FullNameCase;
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



        public static bool LoadText(this TextBox txtBox, RvFile tGame, string filename)
        {
            txtBox.ClearText();
            if (!LoadBytes(tGame, filename, out byte[] memBuffer))
                return false;

            string txt = Encoding.ASCII.GetString(memBuffer);
            txt = txt.Replace("\r\n", "\r\n\r\n");
            txtBox.Text = txt;

            //var f = new Font("Consolas", 7, FontStyle.Regular, GraphicsUnit.Pixel, 255);
            //txtBox.SelectAll();
            //txtBox.SelectionFont = f;
            //txtBox.SelectionCharOffset = 0;

            return true;
        }
        public static bool LoadNFO(this TextBox txtBox,RvFile tGame,string search)
        {
            if (!LoadBytes(tGame, search, out byte[] memBuffer))
                return false;

            string txt = CodePage437.GetStringLF(memBuffer);
            txt = txt.Replace("\r\n", "\n");
            txt = txt.Replace("\r", "\n");
            txt = txt.Replace("\n", "\r\n");
            txtBox.Text = txt;

            //var f = new Font("Consolas", 7, FontStyle.Regular, GraphicsUnit.Pixel, 255);
            //txtBox.SelectAll();
            //txtBox.SelectionFont = f;
            //txtBox.SelectionCharOffset = 0;

            return true;
        }

        public static void ClearText(this TextBox txtBox)
        {
            txtBox.Text = "";
        }

    }
}


