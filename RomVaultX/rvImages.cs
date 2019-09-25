using System.Drawing;

namespace RomVaultX
{
    public static class RvImages
    {
        public static Bitmap TickBoxDisabled
        {
            get { return GetBitmap("TickBoxDisabled"); }
        }

        public static Bitmap TickBoxTicked
        {
            get { return GetBitmap("TickBoxTicked"); }
        }

        public static Bitmap TickBoxUnTicked
        {
            get { return GetBitmap("TickBoxUnTicked"); }
        }

        public static Bitmap ExpandBoxMinus
        {
            get { return GetBitmap("ExpandBoxMinus"); }
        }

        public static Bitmap ExpandBoxPlus
        {
            get { return GetBitmap("ExpandBoxPlus"); }
        }

        public static Bitmap GetBitmap(string bitmapName)
        {
            object bmObj = rvImages1.ResourceManager.GetObject(bitmapName);

            Bitmap bm = null;
            if (bmObj != null)
            {
                bm = (Bitmap) bmObj;
            }

            return bm;
        }
    }
}