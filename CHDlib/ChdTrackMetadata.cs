using CHDSharpLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CHDSharpLib;

/// <summary>
/// Normalized description of a CD track as derived from CHD metadata.
/// </summary>
/// <remarks>
/// Frames are 75 Hz CD frames. <see cref="StartFrame"/> is computed as a running cursor over the track list,
/// with pregaps/postgaps taken into account, and is suitable for deriving byte offsets into the logical stream.
/// </remarks>
public sealed class ChdCdTrackInfo
{
    /// <summary>
    /// 1-based track number.
    /// </summary>
    public int TrackNo { get; set; }

    /// <summary>
    /// Track type as described by CHD metadata (for example: AUDIO, MODE1/2048, MODE1/2352).
    /// </summary>
    public string TrackType { get; set; }

    /// <summary>
    /// Start frame (75 Hz) of the track's INDEX 01 region within the logical stream.
    /// </summary>
    public long StartFrame { get; set; }

    /// <summary>
    /// Track length in frames (75 Hz).
    /// </summary>
    public long Frames { get; set; }

    /// <summary>
    /// Pregap length in frames (75 Hz).
    /// </summary>
    public long PreGapFrames { get; set; }

    /// <summary>
    /// Postgap length in frames (75 Hz).
    /// </summary>
    public long PostGapFrames { get; set; }

    /// <summary>
    /// Sector size in bytes (for example: 2048 for data tracks or 2352 for audio/raw sectors).
    /// </summary>
    public int SectorSize { get; set; }
}

/// <summary>
/// Reads and parses CHD metadata into higher-level track layout information.
/// </summary>
public static class ChdMetadata
{
    /// <summary>
    /// Attempts to read the CD track layout from CHD metadata.
    /// </summary>
    /// <param name="chdPath">CHD file path.</param>
    /// <param name="tracks">Parsed track layout.</param>
    /// <param name="error">Error message on failure.</param>
    /// <returns>True when track metadata is available and parsed; otherwise false.</returns>
    public static bool TryReadCdTrackLayout(string chdPath, out List<ChdCdTrackInfo> tracks, out string error)
    {
        tracks = new List<ChdCdTrackInfo>();
        error = "";
        if (string.IsNullOrWhiteSpace(chdPath) || !System.IO.File.Exists(chdPath))
        {
            error = "CHD not found.";
            return false;
        }

        try
        {
            using FileStream fs = System.IO.File.OpenRead(chdPath);
            if (!CHD.CheckHeader(fs, out _, out uint version))
            {
                error = "Invalid CHD header.";
                return false;
            }

            chd_error err;
            CHDHeader chd;
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
                    return false;
            }

            if (err != chd_error.CHDERR_NONE)
            {
                error = "Header read failed: " + err;
                return false;
            }

            List<(uint tag, byte[] data)> metas = ReadMetadataEntries(fs, chd);
            if (metas.Count == 0)
            {
                error = "No metadata entries.";
                return false;
            }

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
        catch (Exception ex)
        {
            error = ex.Message;
            tracks = new List<ChdCdTrackInfo>();
            return false;
        }
    }

    /// <summary>
    /// Reads the CHD metadata linked list into raw tag/data tuples.
    /// </summary>
    /// <param name="file">Readable CHD stream.</param>
    /// <param name="chd">Parsed CHD header containing metadata offsets.</param>
    /// <returns>List of metadata entries.</returns>
    private static List<(uint tag, byte[] data)> ReadMetadataEntries(Stream file, CHDHeader chd)
    {
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);
        List<(uint tag, byte[] data)> list = new List<(uint tag, byte[] data)>();
        ulong metaoffset = chd.metaoffset;
        while (metaoffset != 0)
        {
            file.Seek((long)metaoffset, SeekOrigin.Begin);
            uint metaTag = br.ReadUInt32BE();
            uint metaLength = br.ReadUInt32BE();
            ulong metaNext = br.ReadUInt64BE();
            uint metaFlags = metaLength >> 24;
            metaLength &= 0x00ffffff;

            byte[] metaData = new byte[metaLength];
            file.Read(metaData, 0, metaData.Length);
            if ((metaFlags & CHDMetaData.CHD_MDFLAGS_CHECKSUM) != 0 || metaData.Length > 0)
                list.Add((metaTag, metaData));

            metaoffset = metaNext;
        }
        return list;
    }

    /// <summary>
    /// Filters and parses CD track metadata entries into normalized track info records.
    /// </summary>
    /// <param name="metas">Raw metadata entries.</param>
    /// <returns>Parsed track info list (may be empty).</returns>
    private static List<ChdCdTrackInfo> ParseCdTracks(List<(uint tag, byte[] data)> metas)
    {
        List<ChdCdTrackInfo> tracks = new List<ChdCdTrackInfo>();
        for (int i = 0; i < metas.Count; i++)
        {
            string tag = TagToString(metas[i].tag);
            if (!IsCdTrackTag(tag))
                continue;

            string text = Util.isAscii(metas[i].data) ? Encoding.ASCII.GetString(metas[i].data) : "";
            if (string.IsNullOrWhiteSpace(text))
                continue;

            ChdCdTrackInfo ti = TryParseCdTrackText(text);
            if (ti != null && ti.TrackNo > 0 && ti.Frames > 0)
                tracks.Add(ti);
        }
        return tracks;
    }

    /// <summary>
    /// Determines whether a metadata tag string represents a CD track entry.
    /// </summary>
    /// <param name="tag">FourCC tag text.</param>
    /// <returns>True when the tag is recognized as a CD track tag; otherwise false.</returns>
    private static bool IsCdTrackTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;
        return tag.StartsWith("CHT", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("CHG", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("CDR", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a FourCC tag value to its 4-character string form.
    /// </summary>
    /// <param name="tag">FourCC tag encoded as a 32-bit integer.</param>
    /// <returns>4-character tag string.</returns>
    private static string TagToString(uint tag)
    {
        char a = (char)((tag >> 24) & 0xFF);
        char b = (char)((tag >> 16) & 0xFF);
        char c = (char)((tag >> 8) & 0xFF);
        char d = (char)((tag >> 0) & 0xFF);
        return new string(new[] { a, b, c, d });
    }

    /// <summary>
    /// Attempts to parse a CD track metadata record from textual key/value content.
    /// </summary>
    /// <param name="text">Metadata text block.</param>
    /// <returns>Parsed track info or null when parsing fails.</returns>
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

    /// <summary>
    /// Resolves sector size from a track type string.
    /// </summary>
    /// <param name="trackType">Track type string.</param>
    /// <returns>Sector size in bytes.</returns>
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

    /// <summary>
    /// Reads an integer field from a key/value metadata record.
    /// </summary>
    /// <param name="text">Metadata text block.</param>
    /// <param name="key">Key name.</param>
    /// <returns>Parsed integer value, or 0 on failure.</returns>
    private static int TryGetInt(string text, string key)
    {
        string s = TryGetString(text, key);
        return int.TryParse(s, out int v) ? v : 0;
    }

    /// <summary>
    /// Reads an integer field from a key/value metadata record as a 64-bit value.
    /// </summary>
    /// <param name="text">Metadata text block.</param>
    /// <param name="key">Key name.</param>
    /// <returns>Parsed value, or 0 on failure.</returns>
    private static long TryGetLong(string text, string key)
    {
        string s = TryGetString(text, key);
        return long.TryParse(s, out long v) ? v : 0;
    }

    /// <summary>
    /// Extracts a string field from a key/value metadata record.
    /// </summary>
    /// <param name="text">Metadata text block.</param>
    /// <param name="key">Key name.</param>
    /// <returns>Extracted string, or an empty string when not present.</returns>
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
