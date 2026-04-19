using CHDReaderTest.Flac.FlacDeps;
using CUETools.Codecs.Flake;

namespace CHDSharpLib
{
    /// <summary>
    /// Per-decode-operation codec state and reusable buffers for CHD block readers.
    /// </summary>
    /// <remarks>
    /// CHD readers avoid allocating codec-specific state per block by carrying it through a <see cref="CHDCodec"/>
    /// instance that is reused within a single verification or streaming operation.
    /// </remarks>
    internal class CHDCodec
    {
        /// <summary>
        /// FLAC decode configuration for CD FLAC blocks.
        /// </summary>
        internal AudioPCMConfig FLAC_settings = null;

        /// <summary>
        /// FLAC decoder instance for CD FLAC blocks.
        /// </summary>
        internal AudioDecoder FLAC_audioDecoder = null;

        /// <summary>
        /// FLAC decode buffer for CD FLAC blocks.
        /// </summary>
        internal AudioBuffer FLAC_audioBuffer = null;


        /// <summary>
        /// AVHuff decode configuration.
        /// </summary>
        internal AudioPCMConfig AVHUFF_settings = null;

        /// <summary>
        /// AVHuff audio decoder instance.
        /// </summary>
        internal AudioDecoder AVHUFF_audioDecoder = null;


        /// <summary>
        /// Temporary sector buffer used by CD readers.
        /// </summary>
        internal byte[] bSector = null;

        /// <summary>
        /// Temporary subcode buffer used by CD readers.
        /// </summary>
        internal byte[] bSubcode = null;

        /// <summary>
        /// Temporary LZMA buffer used by LZMA readers.
        /// </summary>
        internal byte[] blzma = null;

        /// <summary>
        /// Huffman decode table used by general Huffman readers.
        /// </summary>
        internal ushort[] bHuffman = null;

        /// <summary>
        /// Huffman high-band decode table (AVHuff).
        /// </summary>
        internal ushort[] bHuffmanHi = null;

        /// <summary>
        /// Huffman low-band decode table (AVHuff).
        /// </summary>
        internal ushort[] bHuffmanLo = null;

        /// <summary>
        /// Huffman luma decode table (AVHuff).
        /// </summary>
        internal ushort[] bHuffmanY = null;

        /// <summary>
        /// Huffman chroma-blue decode table (AVHuff).
        /// </summary>
        internal ushort[] bHuffmanCB = null;

        /// <summary>
        /// Huffman chroma-red decode table (AVHuff).
        /// </summary>
        internal ushort[] bHuffmanCR = null;
    }
}
