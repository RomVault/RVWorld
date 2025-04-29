namespace Compress.Support.Compression.SimpleInflate
{
    public class Inflate
    {
        private int _bits, _count;

        private byte[] _bIn;
        private int _indexIn; //public int endIn;
        private byte[] _bOut;
        private int _indexOut; //public int endOut;

        private readonly Tree _dynamicLitCodes = new Tree();
        private readonly Tree _dynamicDistCodes = new Tree();
        private readonly Tree _lenCodes = new Tree();


        private Tree _litCodes;
        private Tree _distCodes;
        
        // Table to bit-reverse a byte.
        private static readonly byte[] ReverseTable = new byte[256];


        static Inflate()
        {
            for (int i = 0; i < 256; i++)
            {
                ReverseTable[i] = (byte)(
                    ((i & 0x80) >> 7) | ((i & 0x40) >> 5) | ((i & 0x20) >> 3) | ((i & 0x10) >> 1) |
                    ((i & 0x08) << 1) | ((i & 0x04) << 3) | ((i & 0x02) << 5) | ((i & 0x01) << 7)
                );
            }
        }

        public static int Rev16(int n)
        {
            return (ReverseTable[n & 0xff] << 8) | ReverseTable[(n >> 8) & 0xff];
        }

        private int Bits(int n)
        {
            int v = _bits & ((1 << n) - 1);
            _bits >>= n;
            _count -= n;
            while (_count < 16)
            {
                _bits |= _bIn[_indexIn++] << _count;
                _count += 8;
            }
            return v;
        }

        private void Copy(byte[] src, int index, int len)
        {
            while (len-- > 0)
            {
                _bOut[_indexOut++] = src[index++];
            }
        }


        private int Decode(Tree tree)
        {
            unchecked
            {
                // Find the next prefix code.
                int lo = 0;
                int hi = tree.max;

                int search = Rev16(_bits);
                while (lo < hi)
                {
                    int guess = (lo + hi) >> 1;
                    if (search < tree.Codes[guess]) hi = guess;
                    else lo = guess + 1;
                }

                Bits(tree.bitLen[lo - 1]);
                return tree.num[lo - 1];
            }

        }

        private static readonly byte[] LenBits = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0, 0, 0 };
        private static readonly int[] LenBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258, 0, 0 };
        private static readonly byte[] DistBits = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 0, 0 };
        private static readonly int[] DistBase = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577, 0, 0 };
        private void Run(int sym)
        {
            int length = Bits(LenBits[sym]) + LenBase[sym];
            int dsym = Decode(_distCodes);
            int offs = Bits(DistBits[dsym]) + DistBase[dsym];
            Copy(_bOut, _indexOut - offs, length);
        }

        private void Block()
        {
            for (; ; )
            {
                int sym = Decode(_litCodes);
                if (sym < 256)
                {
                    _bOut[_indexOut++] = (byte)sym;
                }
                else if (sym > 256)
                {
                    Run(sym - 257);
                }
                else // == 256
                    break;
            }
        }


        private void Stored()
        {
            // Uncompressed data block.

            // skip any remaining unused bits to get back byte aligned.
            Bits(_count & 7);

            // read the numbers of bytes to directly copy
            int len = Bits(16);
            // inverted length bits
            int invLen = Bits(16);

            // copy the input stream to the output stream for len bytes
            Copy(_bIn, _indexIn, len);
            _indexIn += len;

            // reload the 
            Bits(16);
        }

        private void TreeFixed()
        {
            _litCodes = Tree.StaticLitCodes;
            _distCodes = Tree.StaticDistCodes;
        }


        private static readonly byte[] Order = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
        private void TreeDynamic()
        {
            unchecked
            {
                byte[] lenlens = new byte[19];
                byte[] lens = new byte[288 + 32];
                int nlit = 257 + Bits(5);
                int ndist = 1 + Bits(5);
                int nlen = 4 + Bits(4);
                for (int n = 0; n < nlen; n++)
                    lenlens[Order[n]] = (byte)Bits(3);

                // Build the tree for decoding code lengths.
                _lenCodes.Build(lenlens, 0, 19);

                // Decode code lengths.
                for (int n = 0; n < nlit + ndist;)
                {
                    int sym = Decode(_lenCodes);
                    switch (sym)
                    {
                        case 16:
                            for (int i = 3 + Bits(2); i > 0; i--, n++)
                                lens[n] = lens[n - 1];
                            break;
                        case 17:
                            for (int i = 3 + Bits(3); i > 0; i--, n++)
                                lens[n] = 0;
                            break;
                        case 18:
                            for (int i = 11 + Bits(7); i > 0; i--, n++)
                                lens[n] = 0;
                            break;
                        default:
                            lens[n++] = (byte)sym;
                            break;
                    }
                }

                // Build lit/dist trees.
                _dynamicLitCodes.Build(lens, 0, nlit);
                _dynamicDistCodes.Build(lens, nlit, ndist);

                _litCodes = _dynamicLitCodes;
                _distCodes = _dynamicDistCodes;
            }
        }

        public int InflateBuffer(byte[] outbuffer, byte[] inbuffer)
        {
            int last;
            // We assume we can buffer 2 extra bytes from off the end of 'in'.
            _bIn = inbuffer;
            _bOut = outbuffer;
            _bits = 0;
            _count = 0;

            Bits(0);

            do
            {
                last = Bits(1);
                switch (Bits(2))
                {
                    case 0: Stored(); break;
                    case 1: TreeFixed(); Block(); break;
                    case 2: TreeDynamic(); Block(); break; // 87% block()
                                                       //case 3:
                    default: throw new System.InvalidOperationException("Invalid Initial bits");
                }
            } while (last == 0);

            return 1;
        }
    }
}
