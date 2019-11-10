using System.Drawing;

namespace ROMVault
{
    public static class rvImages
    {
        public static Bitmap TickBoxLocked => GetBitmap("TickBoxLocked");

        public static Bitmap TickBoxTicked => GetBitmap("TickBoxTicked");

        public static Bitmap TickBoxUnTicked => GetBitmap("TickBoxUnTicked");

        public static Bitmap ExpandBoxMinus => GetBitmap("ExpandBoxMinus");

        public static Bitmap ExpandBoxPlus => GetBitmap("ExpandBoxPlus");

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