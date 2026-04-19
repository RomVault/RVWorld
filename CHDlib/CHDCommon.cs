using System;

namespace CHDSharpLib;

/// <summary>
/// Shared CHD helpers and core CHD enums used by the reader, decompressor, and metadata pipeline.
/// </summary>
internal static class CHDCommon
{

    /// <summary>
    /// Converts a legacy numeric codec identifier (CHD v3/v4 headers) to a <see cref="chd_codec"/>.
    /// </summary>
    /// <param name="ct">Legacy codec identifier from the header.</param>
    /// <returns>The corresponding codec, or <see cref="chd_codec.CHD_CODEC_ERROR"/> when unknown.</returns>
    internal static chd_codec compTypeConv(uint ct)
    {
        switch (ct)
        {
            case 1: return chd_codec.CHD_CODEC_ZLIB;
            case 2: return chd_codec.CHD_CODEC_ZLIB;
            case 3: return chd_codec.CHD_CODEC_AVHUFF;
            default:
                return chd_codec.CHD_CODEC_ERROR;
        }
    }

    /// <summary>
    /// Converts CHD v3/v4 map flags to the v5-style <see cref="compression_type"/> representation.
    /// </summary>
    /// <param name="mapFlags">V3/V4 map flags.</param>
    /// <returns>Mapped compression type, or <see cref="compression_type.COMPRESSION_ERROR"/> on unknown input.</returns>
    internal static compression_type ConvMapFlagstoCompressionType(mapFlags mapFlags)
    {
        switch (mapFlags & mapFlags.MAP_ENTRY_FLAG_TYPE_MASK)
        {
            case mapFlags.MAP_ENTRY_TYPE_INVALID: return compression_type.COMPRESSION_ERROR;
            case mapFlags.MAP_ENTRY_TYPE_COMPRESSED: return compression_type.COMPRESSION_TYPE_0;
            case mapFlags.MAP_ENTRY_TYPE_UNCOMPRESSED: return compression_type.COMPRESSION_NONE;
            case mapFlags.MAP_ENTRY_TYPE_MINI: return compression_type.COMPRESSION_MINI;
            case mapFlags.MAP_ENTRY_TYPE_SELF_HUNK: return compression_type.COMPRESSION_SELF;
            case mapFlags.MAP_ENTRY_TYPE_PARENT_HUNK: return compression_type.COMPRESSION_PARENT;
            default:
                return compression_type.COMPRESSION_ERROR;
        }
    }

}



/// <summary>
/// CHD compression codec identifiers as stored in CHD headers.
/// </summary>
public enum chd_codec
{
    CHD_CODEC_NONE = 0, ///< No compression.
    CHD_CODEC_ZLIB = 0x7A6C6962, ///< zlib.
    CHD_CODEC_ZSTD = 0x7A737464, ///< zstd.
    CHD_CODEC_LZMA = 0x6C7A6D61, ///< lzma.
    CHD_CODEC_HUFFMAN = 0x68756666, ///< Huffman.
    CHD_CODEC_FLAC = 0x666C6163, ///< FLAC (audio).
    CHD_CODEC_CD_ZLIB = 0x63647A6C, ///< CD zlib.
    CHD_CODEC_CD_ZSTD = 0x63647A73, ///< CD zstd.
    CHD_CODEC_CD_LZMA = 0x63646C7A, ///< CD lzma.
    CHD_CODEC_CD_FLAC = 0x6364666C, ///< CD FLAC.
    CHD_CODEC_AVHUFF = 0x61766875, ///< AVHuff (video).
    CHD_CODEC_ERROR = 0x0eeeeeee ///< Unknown/invalid codec.
}



[Flags]
/// <summary>
/// CHD v3/v4 map entry flags.
/// </summary>
public enum mapFlags
{
    MAP_ENTRY_FLAG_TYPE_MASK = 0x000f, ///< Mask for the map entry type field.
    MAP_ENTRY_FLAG_NO_CRC = 0x0010, ///< Indicates no CRC is present for the entry.

    MAP_ENTRY_TYPE_INVALID = 0x0000, ///< Invalid entry.
    MAP_ENTRY_TYPE_COMPRESSED = 0x0001, ///< Standard compression.
    MAP_ENTRY_TYPE_UNCOMPRESSED = 0x0002, ///< Uncompressed data.
    MAP_ENTRY_TYPE_MINI = 0x0003, ///< Mini entry where offset stores literal data.
    MAP_ENTRY_TYPE_SELF_HUNK = 0x0004, ///< Same as another hunk in this file.
    MAP_ENTRY_TYPE_PARENT_HUNK = 0x0005 ///< Same as a hunk in the parent file.
}

/// <summary>
/// Normalized CHD map entry compression types (v5-style), including internal pseudo-types used by the decoder.
/// </summary>
public enum compression_type
{
    COMPRESSION_TYPE_0 = 0, ///< Uses codec slot #0 from the CHD header.
    COMPRESSION_TYPE_1 = 1, ///< Uses codec slot #1 from the CHD header.
    COMPRESSION_TYPE_2 = 2, ///< Uses codec slot #2 from the CHD header.
    COMPRESSION_TYPE_3 = 3, ///< Uses codec slot #3 from the CHD header.
    COMPRESSION_NONE = 4, ///< No compression; implicit length equals hunk size.
    COMPRESSION_SELF = 5, ///< Same as another block in this CHD (deduplicated).
    COMPRESSION_PARENT = 6, ///< Same as a block in the parent CHD.

    COMPRESSION_RLE_SMALL = 7, ///< Start of small RLE run (4-bit length) used by v5 map encoding.
    COMPRESSION_RLE_LARGE = 8, ///< Start of large RLE run (8-bit length) used by v5 map encoding.
    COMPRESSION_SELF_0 = 9, ///< Same as the last <see cref="COMPRESSION_SELF"/> block.
    COMPRESSION_SELF_1 = 10, ///< Same as the last <see cref="COMPRESSION_SELF"/> block + 1.
    COMPRESSION_PARENT_SELF = 11, ///< Same block in the parent.
    COMPRESSION_PARENT_0 = 12, ///< Same as the last <see cref="COMPRESSION_PARENT"/> block.
    COMPRESSION_PARENT_1 = 13, ///< Same as the last <see cref="COMPRESSION_PARENT"/> block + 1.

    COMPRESSION_MINI = 100, ///< CHD v3/v4 mini entry where offset stores literal data.
    COMPRESSION_ERROR = 101 ///< Internal error value for unknown/invalid compression.
};



/// <summary>
/// CHD error codes surfaced by the reader/decoder pipeline.
/// </summary>
public enum chd_error
{
    CHDERR_NONE, ///< No error.
    CHDERR_NO_INTERFACE, ///< Required interface is not available.
    CHDERR_OUT_OF_MEMORY, ///< Allocation failure.
    CHDERR_INVALID_FILE, ///< Not a valid CHD file.
    CHDERR_INVALID_PARAMETER, ///< Invalid parameter passed to an API.
    CHDERR_INVALID_DATA, ///< File contents are invalid or corrupted.
    CHDERR_FILE_NOT_FOUND, ///< File was not found.
    CHDERR_REQUIRES_PARENT, ///< CHD is a child and requires a parent CHD.
    CHDERR_FILE_NOT_WRITEABLE, ///< File cannot be written.
    CHDERR_READ_ERROR, ///< Read error from underlying stream.
    CHDERR_WRITE_ERROR, ///< Write error to underlying stream.
    CHDERR_CODEC_ERROR, ///< Codec initialization or operation error.
    CHDERR_INVALID_PARENT, ///< Parent CHD is invalid or mismatched.
    CHDERR_HUNK_OUT_OF_RANGE, ///< Requested hunk is out of range.
    CHDERR_DECOMPRESSION_ERROR, ///< Decompression failed.
    CHDERR_COMPRESSION_ERROR, ///< Compression failed.
    CHDERR_CANT_CREATE_FILE, ///< File creation failed.
    CHDERR_CANT_VERIFY, ///< Verification failed.
    CHDERR_NOT_SUPPORTED, ///< Operation not supported.
    CHDERR_METADATA_NOT_FOUND, ///< Requested metadata is not present.
    CHDERR_INVALID_METADATA_SIZE, ///< Metadata size is invalid.
    CHDERR_UNSUPPORTED_VERSION, ///< Unsupported CHD version.
    CHDERR_VERIFY_INCOMPLETE, ///< Verification did not complete.
    CHDERR_INVALID_METADATA, ///< Metadata is invalid or cannot be parsed.
    CHDERR_INVALID_STATE, ///< Operation is invalid for the current state.
    CHDERR_OPERATION_PENDING, ///< An asynchronous operation is pending.
    CHDERR_NO_ASYNC_OPERATION, ///< No asynchronous operation is active.
    CHDERR_UNSUPPORTED_FORMAT, ///< Unsupported format variant.
    CHDERR_CANNOT_OPEN_FILE ///< File cannot be opened.
};
