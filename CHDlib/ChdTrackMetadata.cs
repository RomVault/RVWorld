using CHDSharpLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CHDSharpLib;

public sealed class ChdCdTrackInfo
{
    public int TrackNo { get; set; }
    public string TrackType { get; set; }
    public long StartFrame { get; set; }
    public long Frames { get; set; }
    public long PreGapFrames { get; set; }
    public long PostGapFrames { get; set; }
    public int SectorSize { get; set; }
}

public sealed class ChdContainerInfo
{
    public uint Version { get; set; }
    public ulong LogicalSize { get; set; }
    public uint HunkSize { get; set; }
    public uint UnitSize { get; set; }
    public List<string> Codecs { get; set; } = new List<string>();
    public byte[] Sha1 { get; set; }
    public byte[] RawSha1 { get; set; }
    public byte[] ParentSha1 { get; set; }
    public bool RequiresParent { get; set; }
}

public static class ChdMetadata
{
    private const int MaxMetadataEntries = 4096;
    private const int MaxMetadataEntryBytes = 16 * 1024 * 1024;
    private const long MaxMetadataTotalBytes = 64L * 1024 * 1024;
    public static bool TryReadContainerInfo(string chdPath, out ChdContainerInfo info, out string error)
    {
        info = null;
        error = "";
        if (!TryOpenHeader(chdPath, out FileStream fs, out CHDHeader chd, out uint version, out error))
            return false;

        try
        {
            using (fs)
            {
                List<string> codecs = new List<string>();
                if (chd.compression != null)
                {
                    for (int i = 0; i < chd.compression.Length; i++)
                    {
                        string codec = CodecToString(chd.compression[i]);
                        if (!string.IsNullOrEmpty(codec))
                            codecs.Add(codec);
                    }
                }

                info = new ChdContainerInfo
                {
                    Version = version,
                    LogicalSize = chd.totalbytes,
                    HunkSize = chd.blocksize,
                    UnitSize = chd.unitbytes,
                    Codecs = codecs,
                    Sha1 = Clone(chd.sha1),
                    RawSha1 = Clone(chd.rawsha1),
                    ParentSha1 = Clone(chd.parentsha1),
                    RequiresParent = HasNonZero(chd.parentmd5) || HasNonZero(chd.parentsha1)
                };
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            info = null;
            return false;
        }
    }

    private static byte[] Clone(byte[] value)
    {
        if (value == null) return null;
        byte[] copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }

    private static bool HasNonZero(byte[] value)
    {
        if (value == null) return false;
        for (int i = 0; i < value.Length; i++) if (value[i] != 0) return true;
        return false;
    }

    public static bool TryReadTextMetadata(string chdPath, string wantedTag, int wantedIndex, out string text, out string error)
    {
        text = "";
        error = "";
        if (string.IsNullOrWhiteSpace(wantedTag) || wantedTag.Length != 4 || wantedIndex < 0)
        {
            error = "Metadata tag must contain exactly four characters and the index must be non-negative.";
            return false;
        }

        if (!TryOpenHeader(chdPath, out FileStream fs, out CHDHeader chd, out _, out error))
            return false;

        try
        {
            using (fs)
            {
                List<(uint tag, byte[] data)> metas = ReadMetadataEntries(fs, chd);
                int tagIndex = 0;
                for (int i = 0; i < metas.Count; i++)
                {
                    if (!string.Equals(TagToString(metas[i].tag), wantedTag, StringComparison.Ordinal))
                        continue;
                    if (tagIndex++ != wantedIndex)
                        continue;

                    if (!IsAsciiMetadata(metas[i].data))
                    {
                        error = "Metadata is not text.";
                        return false;
                    }

                    text = Encoding.UTF8.GetString(metas[i].data).TrimEnd('\0');
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        error = "Metadata not found.";
        return false;
    }

    public static bool TryReadBinaryMetadata(string chdPath, string wantedTag, int wantedIndex, out byte[] data, out string error)
    {
        data = null;
        error = "";
        if (string.IsNullOrWhiteSpace(wantedTag) || wantedTag.Length != 4 || wantedIndex < 0)
        {
            error = "Metadata tag must contain exactly four characters and the index must be non-negative.";
            return false;
        }

        if (!TryOpenHeader(chdPath, out FileStream fs, out CHDHeader chd, out _, out error))
            return false;

        try
        {
            using (fs)
            {
                List<(uint tag, byte[] data)> metas = ReadMetadataEntries(fs, chd);
                int tagIndex = 0;
                for (int i = 0; i < metas.Count; i++)
                {
                    if (!string.Equals(TagToString(metas[i].tag), wantedTag, StringComparison.Ordinal))
                        continue;
                    if (tagIndex++ != wantedIndex)
                        continue;
                    data = metas[i].data;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            data = null;
            return false;
        }

        error = "Metadata not found.";
        return false;
    }

    public static bool TryReadCdTrackLayout(string chdPath, out List<ChdCdTrackInfo> tracks, out string error)
    {
        return TryReadCdTrackLayout(chdPath, out tracks, out _, out error);
    }

    public static bool TryReadCdTrackLayout(string chdPath, out List<ChdCdTrackInfo> tracks, out bool isGdRom, out string error)
    {
        tracks = new List<ChdCdTrackInfo>();
        isGdRom = false;
        error = "";
        if (!TryOpenHeader(chdPath, out FileStream fs, out CHDHeader chd, out _, out error))
            return false;

        try
        {
            using (fs)
            {
                List<(uint tag, byte[] data)> metas = ReadMetadataEntries(fs, chd);
                if (metas.Count == 0)
                {
                    error = "No metadata entries.";
                    return false;
                }

                isGdRom = HasGdRomMetadata(metas);

                List<ChdCdTrackInfo> parsed = ParseCdTracks(metas);
                if (parsed.Count == 0)
                {
                    error = "No CD track metadata found.";
                    return false;
                }

                parsed.Sort((a, b) => a.TrackNo.CompareTo(b.TrackNo));
                long cursor = 0;
                for (int i = 0; i < parsed.Count; i++)
                {
                    parsed[i].StartFrame = cursor + Math.Max(0, parsed[i].PreGapFrames);
                    cursor += Math.Max(0, parsed[i].PreGapFrames) + Math.Max(0, parsed[i].Frames) + Math.Max(0, parsed[i].PostGapFrames);
                }

                tracks = parsed;
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            tracks = new List<ChdCdTrackInfo>();
            isGdRom = false;
            return false;
        }
    }

    private static bool TryOpenHeader(string chdPath, out FileStream fs, out CHDHeader chd, out uint version, out string error)
    {
        fs = null;
        chd = null;
        version = 0;
        error = "";
        if (string.IsNullOrWhiteSpace(chdPath) || !System.IO.File.Exists(chdPath))
        {
            error = "CHD not found.";
            return false;
        }

        try
        {
            fs = System.IO.File.OpenRead(chdPath);
            if (!CHD.CheckHeader(fs, out _, out version))
            {
                error = "Invalid CHD header.";
                fs.Dispose();
                fs = null;
                return false;
            }

            chd_error err;
            switch (version)
            {
                case 1:
                    err = CHDHeaders.ReadHeaderV1(fs, out chd);
                    break;
                case 2:
                    err = CHDHeaders.ReadHeaderV2(fs, out chd);
                    break;
                case 3:
                    err = CHDHeaders.ReadHeaderV3(fs, out chd);
                    break;
                case 4:
                    err = CHDHeaders.ReadHeaderV4(fs, out chd);
                    break;
                case 5:
                    err = CHDHeaders.ReadHeaderV5(fs, out chd);
                    break;
                default:
                    error = "Unsupported CHD version: " + version;
                    fs.Dispose();
                    fs = null;
                    return false;
            }

            if (err != chd_error.CHDERR_NONE)
            {
                error = "Header read failed: " + err;
                fs.Dispose();
                fs = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            fs?.Dispose();
            fs = null;
            chd = null;
            return false;
        }
    }

    private static bool HasGdRomMetadata(List<(uint tag, byte[] data)> metas)
    {
        for (int i = 0; i < metas.Count; i++)
        {
            if (TagToString(metas[i].tag).StartsWith("CHG", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static List<(uint tag, byte[] data)> ReadMetadataEntries(Stream file, CHDHeader chd)
    {
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);
        List<(uint tag, byte[] data)> list = new List<(uint tag, byte[] data)>();
        HashSet<ulong> visitedOffsets = new HashSet<ulong>();
        ulong metaoffset = chd.metaoffset;
        long metadataBytes = 0;
        while (metaoffset != 0)
        {
            if (list.Count >= MaxMetadataEntries || !visitedOffsets.Add(metaoffset) || metaoffset > (ulong)Math.Max(0, file.Length - 16))
                break;

            file.Seek((long)metaoffset, SeekOrigin.Begin);
            uint metaTag = br.ReadUInt32BE();
            uint metaLength = br.ReadUInt32BE();
            ulong metaNext = br.ReadUInt64BE();
            uint metaFlags = metaLength >> 24;
            metaLength &= 0x00ffffff;

            if (metaLength > MaxMetadataEntryBytes || metaLength > (ulong)Math.Max(0, file.Length - file.Position))
                break;
            metadataBytes += metaLength;
            if (metadataBytes > MaxMetadataTotalBytes)
                break;

            if (metaNext != 0 && (metaNext <= metaoffset || metaNext > (ulong)Math.Max(0, file.Length - 16)))
                break;

            byte[] metaData = new byte[metaLength];
            int total = 0;
            while (total < metaData.Length)
            {
                int read = file.Read(metaData, total, metaData.Length - total);
                if (read <= 0)
                    break;
                total += read;
            }
            if (total != metaData.Length)
                break;
            if ((metaFlags & CHDMetaData.CHD_MDFLAGS_CHECKSUM) != 0 || metaData.Length > 0)
                list.Add((metaTag, metaData));

            metaoffset = metaNext;
        }
        return list;
    }

    private static List<ChdCdTrackInfo> ParseCdTracks(List<(uint tag, byte[] data)> metas)
    {
        List<ChdCdTrackInfo> tracks = new List<ChdCdTrackInfo>();
        for (int i = 0; i < metas.Count; i++)
        {
            string tag = TagToString(metas[i].tag);
            if (!IsCdTrackTag(tag))
                continue;

            string text = IsAsciiMetadata(metas[i].data) ? Encoding.ASCII.GetString(metas[i].data) : "";
            if (string.IsNullOrWhiteSpace(text))
                continue;

            ChdCdTrackInfo ti = TryParseCdTrackText(text);
            if (ti != null && ti.TrackNo > 0 && ti.Frames > 0)
                tracks.Add(ti);
        }
        return tracks;
    }

    private static bool IsAsciiMetadata(byte[] data)
    {
        if (data == null)
            return false;

        for (int i = 0; i < data.Length; i++)
        {
            byte value = data[i];
            if (value != 0 && value < 32)
                return false;
        }

        return true;
    }

    private static bool IsCdTrackTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;
        return tag.StartsWith("CHT", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("CHG", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("CDR", StringComparison.OrdinalIgnoreCase);
    }

    private static string TagToString(uint tag)
    {
        char a = (char)((tag >> 24) & 0xFF);
        char b = (char)((tag >> 16) & 0xFF);
        char c = (char)((tag >> 8) & 0xFF);
        char d = (char)((tag >> 0) & 0xFF);
        return new string(new[] { a, b, c, d });
    }

    private static string CodecToString(chd_codec codec)
    {
        switch (codec)
        {
            case chd_codec.CHD_CODEC_ZLIB: return "zlib";
            case chd_codec.CHD_CODEC_ZSTD: return "zstd";
            case chd_codec.CHD_CODEC_LZMA: return "lzma";
            case chd_codec.CHD_CODEC_HUFFMAN: return "huff";
            case chd_codec.CHD_CODEC_FLAC: return "flac";
            case chd_codec.CHD_CODEC_CD_ZLIB: return "cdzl";
            case chd_codec.CHD_CODEC_CD_ZSTD: return "cdzs";
            case chd_codec.CHD_CODEC_CD_LZMA: return "cdlz";
            case chd_codec.CHD_CODEC_CD_FLAC: return "cdfl";
            case chd_codec.CHD_CODEC_AVHUFF: return "avhu";
            default: return "";
        }
    }

    private static ChdCdTrackInfo TryParseCdTrackText(string text)
    {
        try
        {
            int trackNo = TryGetInt(text, "TRACK");
            long frames = TryGetLong(text, "FRAMES");
            long pregap = TryGetLong(text, "PREGAP");
            long postgap = TryGetLong(text, "POSTGAP");
            string type = TryGetString(text, "TYPE");
            if (string.IsNullOrWhiteSpace(type))
            {
                string mode = TryGetString(text, "MODE");
                if (!string.IsNullOrWhiteSpace(mode))
                    type = mode;
            }

            int sector = ResolveSectorSize(type);
            if (sector <= 0)
                sector = 2352;

            return new ChdCdTrackInfo
            {
                TrackNo = trackNo,
                TrackType = type ?? "",
                Frames = frames,
                PreGapFrames = pregap,
                PostGapFrames = postgap,
                SectorSize = sector
            };
        }
        catch
        {
            return null;
        }
    }

    private static int ResolveSectorSize(string trackType)
    {
        if (string.IsNullOrWhiteSpace(trackType))
            return 2352;
        string t = trackType.Trim().ToUpperInvariant();
        if (t.Contains("2048"))
            return 2048;
        if (t.Contains("AUDIO"))
            return 2352;
        if (t.Contains("2352"))
            return 2352;
        if (t.Contains("MODE1") || t.Contains("MODE2"))
            return 2352;
        return 2352;
    }

    private static int TryGetInt(string text, string key)
    {
        string s = TryGetString(text, key);
        return int.TryParse(s, out int v) ? v : 0;
    }

    private static long TryGetLong(string text, string key)
    {
        string s = TryGetString(text, key);
        return long.TryParse(s, out long v) ? v : 0;
    }

    private static string TryGetString(string text, string key)
    {
        Match m = Regex.Match(text, key + @":\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (m.Success)
            return m.Groups[1].Value.Trim();
        m = Regex.Match(text, key + @":\s*([^\s]+)", RegexOptions.IgnoreCase);
        if (m.Success)
            return m.Groups[1].Value.Trim();
        return "";
    }
}
