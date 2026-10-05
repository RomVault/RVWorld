using System.Collections.Concurrent;

namespace TrrntZip
{
    public static class MainQueue
    {
        public static BlockingCollection<cFile> bccFile = new BlockingCollection<cFile>();


        public static int fileCount;
    }
}
