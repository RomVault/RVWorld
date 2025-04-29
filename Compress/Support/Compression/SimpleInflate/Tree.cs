namespace Compress.Support.Compression.SimpleInflate
{
    public class Tree
    {
        public int[] Codes = new int[288];
        public int[] num = new int[288];
        public int[] bitLen = new int[288];
        public int max;

        // Static tables
        public static readonly Tree StaticLitCodes = new Tree();
        public static readonly Tree StaticDistCodes = new Tree();

        static Tree()
        {
            // Fixed set of Huffman codes.
            byte[] lens = new byte[288 + 32];
            int n;
            for (n = 0; n <= 143; n++) lens[n] = 8;
            for (n = 144; n <= 255; n++) lens[n] = 9;
            for (n = 256; n <= 279; n++) lens[n] = 7;
            for (n = 280; n <= 287; n++) lens[n] = 8;
            for (n = 0; n < 32; n++) lens[288 + n] = 5;

            StaticLitCodes.Build(lens, 0, 288);
            StaticDistCodes.Build(lens, 288, 32);
        }

        public void Build(byte[] lens, int lensOffset, int symcount)
        {
            unchecked
            {
                int[] codes = new int[16];
                int[] first = new int[16];
                int[] counts = new int[16];

                int endcount = lensOffset + symcount;
                // Frequency count.
                for (int n = lensOffset; n < endcount; n++)
                    counts[lens[n]]++;

                // Distribute codes.
                counts[0] = codes[0] = first[0] = 0;
                for (int n = 1; n <= 15; n++)
                {
                    codes[n] = (codes[n - 1] + counts[n - 1]) << 1;
                    first[n] = first[n - 1] + counts[n - 1];
                }

                // Insert keys into the tree for each symbol.
                int lensOffsetLocal = lensOffset;
                for (int n = 0; n < symcount; n++)
                {
                    int len = lens[lensOffsetLocal++];
                    if (len == 0) continue;

                    int code = codes[len]++;
                    int slot = first[len]++;
                    Codes[slot] = code << (16 - len);
                    num[slot] = n;
                    bitLen[slot] = len;
                }

                max = first[15];
            }
        }

    }

}
