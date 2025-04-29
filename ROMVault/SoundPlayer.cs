using System.Media;

namespace ROMVault
{
    internal static class RVPlayer
    {
        private static SoundPlayer snd = null;
        public static void PlaySound(string filename)
        {
            if (!RVIO.File.Exists(filename))
                return;

            PlayerClose();
            snd = new SoundPlayer(filename);
            snd.Play();
        }
        private static void PlayerClose()
        {
            if (snd == null)
                return;

            snd.Stop();
            snd.Dispose();
            snd = null;
        }

    }
}
