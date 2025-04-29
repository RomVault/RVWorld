using CHDReaderTest.Flac.FlacDeps;
using CUETools.Codecs.Flake;

namespace CHDSharpLib
{
    internal class CHDCodec
    {
        internal AudioPCMConfig FLAC_settings = null;
        internal AudioDecoder FLAC_audioDecoder = null;
        internal AudioBuffer FLAC_audioBuffer = null;


        internal AudioPCMConfig AVHUFF_settings = null;
        internal AudioDecoder AVHUFF_audioDecoder = null;


        internal byte[] bSector = null;
        internal byte[] bSubcode = null;

        internal byte[] blzma = null;

        internal ushort[] bHuffman = null;

        internal ushort[] bHuffmanHi = null;
        internal ushort[] bHuffmanLo = null;

        internal ushort[] bHuffmanY = null;
        internal ushort[] bHuffmanCB = null;
        internal ushort[] bHuffmanCR = null;
    }
}
