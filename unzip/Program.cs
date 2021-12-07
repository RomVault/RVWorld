using System;
using Compress.Support.Utils;

namespace unzip
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Arguments:");
                Console.WriteLine("RVUnzip.exe source.zip");
                Console.WriteLine("RVUnzip.exe source.zip -d destination");
                return;
            }
            string filename = args[0].Replace("\"","");
            string outDir = "";
            if (args.Length == 3)
            {
                if (args[1].ToLower() != "-d")
                {
                    Console.WriteLine("Unknown command line option.");
                    return;
                }
                outDir = args[2].Replace("\"","");
            }
            try
            {
                ArchiveExtract extract = new ArchiveExtract(consoleCallBack);
                extract.FullExtract(filename, outDir);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }

        }

        private static void consoleCallBack(string message)
        {
            Console.WriteLine(message);
        }

    }
}
