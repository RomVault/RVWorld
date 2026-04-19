using CHDSharpLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CHDSharpLib;

/// <summary>
/// Internal normalized representation of a CHD header and its decoded map.
/// </summary>
/// <remarks>
/// This is populated by <see cref="CHDHeaders"/> and then consumed by the decompression and metadata readers.
/// </remarks>
internal class CHDHeader
{
    /// <summary>
    /// Codec slots declared by the CHD header.
    /// </summary>
    public chd_codec[] compression;

    /// <summary>
    /// Resolved codec reader table corresponding to <see cref="compression"/>.
    /// </summary>
    public CHDReader[] chdReader;

    /// <summary>
    /// Total logical byte size represented by the CHD.
    /// </summary>
    public ulong totalbytes;

    /// <summary>
    /// Hunk size in bytes.
    /// </summary>
    public uint blocksize;

    /// <summary>
    /// Total number of hunks.
    /// </summary>
    public uint totalblocks;

    /// <summary>
    /// Per-hunk map entries describing storage offsets, sizes, and compression.
    /// </summary>
    public mapentry[] map;

    /// <summary>
    /// MD5 of compressed data (when present in the header).
    /// </summary>
    public byte[] md5;

    /// <summary>
    /// SHA1 of compressed data (when present in the header).
    /// </summary>
    public byte[] rawsha1;

    /// <summary>
    /// SHA1 including metadata (when present in the header).
    /// </summary>
    public byte[] sha1;

    /// <summary>
    /// Parent CHD MD5 (child CHDs only).
    /// </summary>
    public byte[] parentmd5;

    /// <summary>
    /// Parent CHD SHA1 (child CHDs only).
    /// </summary>
    public byte[] parentsha1;

    /// <summary>
    /// File offset of the first metadata entry, or 0 when no metadata is present.
    /// </summary>
    public ulong metaoffset;
}

/// <summary>
/// CHD hunk map entry describing how to locate and decode a given hunk.
/// </summary>
internal class mapentry
{
    /// <summary>
    /// Compression kind for this entry.
    /// </summary>
    public compression_type comptype;

    /// <summary>
    /// Compressed data length, in bytes (0 for <see cref="compression_type.COMPRESSION_SELF"/> entries).
    /// </summary>
    public uint length; // length of compressed data

    /// <summary>
    /// File offset of compressed data, or source index for <see cref="compression_type.COMPRESSION_SELF"/>.
    /// </summary>
    public ulong offset; // offset of compressed data in file. Also index of source block for COMPRESSION_SELF 

    /// <summary>
    /// Optional CRC32 for v3/v4 entries.
    /// </summary>
    public uint? crc = null; // V3 & V4

    /// <summary>
    /// Optional CRC16 for v5 entries.
    /// </summary>
    public ushort? crc16 = null; // V5

    /// <summary>
    /// For <see cref="compression_type.COMPRESSION_SELF"/> entries, points to the referenced entry.
    /// </summary>
    public mapentry selfMapEntry; // link to self mapentry data used in COMPRESSION_SELF (replaces offset index)

    /// <summary>
    /// Count of remaining references for caching decisions when decoding.
    /// </summary>
    public int UseCount;

    /// <summary>
    /// Temporary input buffer for compressed data.
    /// </summary>
    public byte[] buffIn = null;

    /// <summary>
    /// Cached decoded output used for repeated blocks.
    /// </summary>
    public byte[] buffOutCache = null;

    /// <summary>
    /// Per-entry decoded output buffer (used by some decode paths).
    /// </summary>
    public byte[] buffOut = null;

    /// <summary>
    /// Used by parallel decompression to keep blocks in order during hashing.
    /// </summary>
    public bool Processed = false;


    /// <summary>
    /// Weight used when selecting which repeated blocks to keep cached.
    /// </summary>
    public int UsageWeight;

    /// <summary>
    /// Indicates whether a cached decoded copy should be retained for this entry.
    /// </summary>
    public bool KeepBufferCopy = false;
}


/// <summary>
/// Callback used to emit progress or informational text during CHD operations.
/// </summary>
/// <param name="message">Message text.</param>
public delegate void Message(string message);

/// <summary>
/// Callback used to emit file-scoped messages during CHD operations.
/// </summary>
/// <param name="filename">The file currently being processed.</param>
/// <param name="message">Message text.</param>
public delegate void FileMessage(string filename, string message);

/// <summary>
/// Minimal CHD reader and verifier.
/// </summary>
/// <remarks>
/// RomVault uses this to validate CHD headers and to optionally compute/verify CHD hashes (including metadata).
/// The library also provides <see cref="ChdLogicalStream"/> for streaming logical contents.
/// </remarks>
public static class CHD
{
    /// <summary>
    /// Optional callback that receives the file name currently being processed.
    /// </summary>
    public static Message fileProcessInfo;

    /// <summary>
    /// Optional callback that receives progress updates for the current file.
    /// </summary>
    public static Message progress;

    /// <summary>
    /// Optional callback used for diagnostic output.
    /// </summary>
    public static Message consoleOut;

    /// <summary>
    /// Number of parallel decode tasks to use when deep-checking CHDs.
    /// </summary>
    public static int taskCount = 8;

    /// <summary>
    /// Validates a CHD file and optionally performs a deep data+metadata verification pass.
    /// </summary>
    /// <param name="s">Readable stream positioned at the start of the CHD file.</param>
    /// <param name="filename">Filename used for diagnostics.</param>
    /// <param name="deepCheck">If true, decompresses and verifies block checksums and metadata.</param>
    /// <param name="chdVersion">Parsed CHD version on success.</param>
    /// <param name="chdSHA1">CHD SHA1 (metadata-aware when available).</param>
    /// <param name="chdMD5">CHD MD5 (compressed data hash when available).</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    public static chd_error CheckFile(Stream s, string filename, bool deepCheck, out uint? chdVersion, out byte[] chdSHA1, out byte[] chdMD5)
    {
        chdSHA1 = null;
        chdMD5 = null;
        chdVersion = null;

        if (!CheckHeader(s, out uint length, out uint version))
            return chd_error.CHDERR_INVALID_FILE;

        consoleOut?.Invoke($@"CHD Version {version}");
        chd_error valid = chd_error.CHDERR_INVALID_DATA;
        CHDHeader chd = null;
        try
        {
            switch (version)
            {
                case 1:
                    valid = CHDHeaders.ReadHeaderV1(s, out chd);
                    break;
                case 2:
                    valid = CHDHeaders.ReadHeaderV2(s, out chd);
                    break;
                case 3:
                    valid = CHDHeaders.ReadHeaderV3(s, out chd);
                    break;
                case 4:
                    valid = CHDHeaders.ReadHeaderV4(s, out chd);
                    break;
                case 5:
                    valid = CHDHeaders.ReadHeaderV5(s, out chd);
                    break;
                default:
                    {
                        consoleOut?.Invoke($@"Unknown version {version}");
                        return chd_error.CHDERR_UNSUPPORTED_VERSION;
                    }
            }
        }
        catch
        {
            valid = chd_error.CHDERR_INVALID_DATA;
        }

        if (valid != chd_error.CHDERR_NONE)
        {
            consoleOut?.Invoke($"Child CHD found, cannot be processed");
            return valid;
        }

        chdSHA1 = chd.sha1 ?? chd.rawsha1;
        chdMD5 = chd.md5;
        chdVersion = version;

        if (!Util.IsAllZeroArray(chd.parentmd5) || !Util.IsAllZeroArray(chd.parentsha1))
        {
            consoleOut?.Invoke($"Child CHD found, cannot be processed");
            return chd_error.CHDERR_REQUIRES_PARENT;
        }

        if (!deepCheck)
            return chd_error.CHDERR_NONE;

        if (((ulong)chd.totalblocks * (ulong)chd.blocksize) != chd.totalbytes)
        {
            consoleOut?.Invoke($"{(ulong)chd.totalblocks * (ulong)chd.blocksize} != {chd.totalbytes}");
        }


        string strComp = "";
        for (int i = 0; i < chd.compression.Length; i++)
        {
            strComp += $", {chd.compression[i].ToString().Substring(10)}";
        }
        fileProcessInfo?.Invoke($"{Path.GetFileName(filename)}, V:{version} {strComp}");

        CHDBlockRead.FindBlockReaders(chd);
        CHDBlockRead.FindRepeatedBlocks(chd, consoleOut);
        int blocksToKeep = (1024 * 1024 * 512) / (int)chd.blocksize;
        CHDBlockRead.KeepMostRepeatedBlocks(chd, blocksToKeep, consoleOut);

        valid = taskCount == 0 ? DecompressData(s, chd) : DecompressDataParallel(s, chd);

        if (valid != chd_error.CHDERR_NONE)
        {
            consoleOut?.Invoke($"Data Decompress Failed: {valid}");
            return valid;
        }

        valid = CHDMetaData.ReadMetaData(s, chd, consoleOut);

        if (valid != chd_error.CHDERR_NONE)
        {
            consoleOut?.Invoke($"Meta Data Failed: {valid}");
            return valid;
        }


        consoleOut?.Invoke($"Valid");
        return chd_error.CHDERR_NONE;
    }

    /// <summary>
    /// Expected header lengths indexed by CHD version.
    /// </summary>
    private static readonly uint[] HeaderLengths = new uint[] { 0, 76, 80, 120, 108, 124 };

    /// <summary>
    /// CHD file signature bytes ("MComprHD").
    /// </summary>
    private static readonly byte[] id = { (byte)'M', (byte)'C', (byte)'o', (byte)'m', (byte)'p', (byte)'r', (byte)'H', (byte)'D' };

    /// <summary>
    /// Reads the CHD header signature and returns the declared header length and version.
    /// </summary>
    /// <param name="file">Readable stream positioned at the start of the CHD file.</param>
    /// <param name="length">Declared header length.</param>
    /// <param name="version">Declared header version.</param>
    /// <returns>True when the signature and header length are consistent; otherwise false.</returns>
    public static bool CheckHeader(Stream file, out uint length, out uint version)
    {
        for (int i = 0; i < id.Length; i++)
        {
            byte b = (byte)file.ReadByte();
            if (b != id[i])
            {
                length = 0;
                version = 0;
                return false;
            }
        }

        using (BinaryReader br = new BinaryReader(file, Encoding.UTF8, true))
        {
            length = br.ReadUInt32BE();
            version = br.ReadUInt32BE();
            return HeaderLengths[version] == length;
        }
    }

    /// <summary>
    /// Decompresses the full CHD data stream sequentially and validates embedded MD5/SHA1 hashes when available.
    /// </summary>
    /// <param name="file">Readable stream positioned at the start of CHD content.</param>
    /// <param name="chd">Parsed CHD header.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error DecompressData(Stream file, CHDHeader chd)
    {
        // stores the FLAC decompression classes for this instance.
        CHDCodec codec = new CHDCodec();

        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        using MD5 md5Check = chd.md5 != null ? MD5.Create() : null;
        using SHA1 sha1Check = chd.rawsha1 != null ? SHA1.Create() : null;

        ArrayPool arrPool = new ArrayPool(chd.blocksize);

        byte[] buffer = new byte[chd.blocksize];

        int block = 0;
        ulong sizetoGo = chd.totalbytes;
        while (sizetoGo > 0)
        {
            /* progress */
            if ((block % 1000) == 0)
                progress?.Invoke($"Verifying, {(100 - sizetoGo * 100 / chd.totalbytes):N1}% complete...\r");

            mapentry mapEntry = chd.map[block];
            if (mapEntry.length > 0)
            {
                mapEntry.buffIn = arrPool.Rent();
                file.Seek((long)mapEntry.offset, System.IO.SeekOrigin.Begin);
                file.Read(mapEntry.buffIn, 0, (int)mapEntry.length);
            }

            /* read the block into the cache */
            chd_error err = CHDBlockRead.ReadBlock(mapEntry, arrPool, chd.chdReader, codec, buffer, (int)chd.blocksize);
            if (err != chd_error.CHDERR_NONE)
                return err;

            if (mapEntry.length > 0)
            {
                arrPool.Return(mapEntry.buffIn);
                mapEntry.buffIn = null;
            }

            int sizenext = sizetoGo > (ulong)chd.blocksize ? (int)chd.blocksize : (int)sizetoGo;

            md5Check?.TransformBlock(buffer, 0, sizenext, null, 0);
            sha1Check?.TransformBlock(buffer, 0, sizenext, null, 0);

            /* prepare for the next block */
            block++;
            sizetoGo -= (ulong)sizenext;

        }
        progress?.Invoke($"Verifying, 100.0% complete...");

        byte[] tmp = new byte[0];
        md5Check?.TransformFinalBlock(tmp, 0, 0);
        sha1Check?.TransformFinalBlock(tmp, 0, 0);

        // here it is now using the rawsha1 value from the header to validate the raw binary data.
        if (chd.md5 != null && !Util.IsAllZeroArray(chd.md5) && !Util.ByteArrEquals(chd.md5, md5Check.Hash))
        {
            return chd_error.CHDERR_DECOMPRESSION_ERROR;
        }
        if (chd.rawsha1 != null && !Util.IsAllZeroArray(chd.rawsha1) && !Util.ByteArrEquals(chd.rawsha1, sha1Check.Hash))
        {
            return chd_error.CHDERR_DECOMPRESSION_ERROR;
        }

        return chd_error.CHDERR_NONE;
    }



    /// <summary>
    /// Decompresses the full CHD data stream in parallel and validates embedded MD5/SHA1 hashes when available.
    /// </summary>
    /// <param name="file">Readable stream positioned at the start of CHD content.</param>
    /// <param name="chd">Parsed CHD header.</param>
    /// <returns>A <see cref="chd_error"/> indicating success or failure.</returns>
    internal static chd_error DecompressDataParallel(Stream file, CHDHeader chd)
    {
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        using MD5 md5Check = chd.md5 != null ? MD5.Create() : null;
        using SHA1 sha1Check = chd.rawsha1 != null ? SHA1.Create() : null;

        using BlockingCollection<int> blocksToDecompress = new BlockingCollection<int>(taskCount * 100);
        using BlockingCollection<int> blocksToHash = new BlockingCollection<int>(taskCount * 100);
        chd_error errMaster = chd_error.CHDERR_NONE;

        List<Task> allTasks = new List<Task>();

        var ts = new CancellationTokenSource();
        CancellationToken ct = ts.Token;

        ArrayPool arrPoolIn = new ArrayPool(chd.blocksize);
        ArrayPool arrPoolOut = new ArrayPool(chd.blocksize);
        ArrayPool arrPoolCache = new ArrayPool(chd.blocksize);

        int blocksToKeep = (1024 * 1024 * 512) / (int)chd.blocksize;
        SemaphoreSlim aheadLock = new SemaphoreSlim(blocksToKeep, blocksToKeep);

        Task producerThread = Task.Factory.StartNew(() =>
        {

            try
            {
                uint blockPercent = chd.totalblocks / 100;
                if (blockPercent == 0)
                    blockPercent = 1;

                for (int block = 0; block < chd.totalblocks; block++)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    /* progress */
                    if ((block % blockPercent) == 0)
                    {
                        //arrPoolIn.ReadStats(out int issuedArraysTotalIn, out int returnedArraysTotalIn);
                        //arrPoolOut.ReadStats(out int issuedArraysTotalOut, out int returnedArraysTotalOut);
                        //arrPoolCache.ReadStats(out int issuedArraysTotalCache, out int returnedArraysTotalCache);
                        //progress?.Invoke($"Verifying: {(long)block * 100 / chd.totalblocks:N0}%     Load buffer: {blocksToDecompress.Count}   Hash buffer: {blocksToHash.Count}  {issuedArraysTotalIn},{returnedArraysTotalIn} | {issuedArraysTotalOut},{returnedArraysTotalOut} | {issuedArraysTotalCache},{returnedArraysTotalCache}\r");

                        //progress?.Invoke($"Verifying: {(long)block * 100 / chd.totalblocks:N0}%     Load buffer: {blocksToDecompress.Count}    Hash buffer: {blocksToHash.Count}");;

                        progress?.Invoke($"Verifying: {(long)block * 100 / chd.totalblocks:N0}%");
                    }
                    mapentry mapentry = chd.map[block];

                    if (mapentry.length > 0)
                    {
                        if (file.Position != (long)mapentry.offset)
                            file.Seek((long)mapentry.offset, System.IO.SeekOrigin.Begin);

                        mapentry.buffIn = arrPoolIn.Rent();
                        file.Read(mapentry.buffIn, 0, (int)mapentry.length);
                    }

                    blocksToDecompress.Add(block, ct);
                }
                // this must be done to tell all the decompression threads to stop working and return.
                for (int i = 0; i < taskCount; i++)
                    blocksToDecompress.Add(-1, ct);

            }
            catch 
            {
                if (ct.IsCancellationRequested)
                    return;

                if (errMaster == chd_error.CHDERR_NONE)
                    errMaster = chd_error.CHDERR_INVALID_FILE;
                ts.Cancel();
            }
        });
        allTasks.Add(producerThread);




        for (int i = 0; i < taskCount; i++)
        {
            Task decompressionThread = Task.Factory.StartNew(() =>
            {
                try
                {
                    CHDCodec codec = new CHDCodec();
                    while (true)
                    {
                        aheadLock.Wait(ct);
                        int block = blocksToDecompress.Take(ct);
                        if (block == -1)
                            return;
                        mapentry mapentry = chd.map[block];
                        mapentry.buffOut = arrPoolOut.Rent();
                        chd_error err = CHDBlockRead.ReadBlock(mapentry, arrPoolCache, chd.chdReader, codec, mapentry.buffOut, (int)chd.blocksize);
                        if (err != chd_error.CHDERR_NONE)
                        {
                            ts.Cancel();
                            errMaster = err;
                            return;
                        }
                        blocksToHash.Add(block, ct);

                        if (mapentry.length > 0)
                        {
                            arrPoolIn.Return(mapentry.buffIn);
                            mapentry.buffIn = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (ct.IsCancellationRequested)
                        return;
                    if (errMaster == chd_error.CHDERR_NONE)
                        errMaster = chd_error.CHDERR_DECOMPRESSION_ERROR;
                    ts.Cancel();
                }
            });

            allTasks.Add(decompressionThread);

        }

        ulong sizetoGo = chd.totalbytes;
        int proc = 0;
        Task hashingThread = Task.Factory.StartNew(() =>
        {
            try
            {
                while (true)
                {
                    int item = blocksToHash.Take(ct);

                    chd.map[item].Processed = true;
                    while (chd.map[proc].Processed == true)
                    {
                        int sizenext = sizetoGo > (ulong)chd.blocksize ? (int)chd.blocksize : (int)sizetoGo;

                        mapentry mapentry = chd.map[proc];

                        md5Check?.TransformBlock(mapentry.buffOut, 0, sizenext, null, 0);
                        sha1Check?.TransformBlock(mapentry.buffOut, 0, sizenext, null, 0);

                        arrPoolOut.Return(mapentry.buffOut);
                        mapentry.buffOut = null;
                        aheadLock.Release();

                        /* prepare for the next block */
                        sizetoGo -= (ulong)sizenext;

                        proc++;
                        if (proc == chd.totalblocks)
                            return;
                    }
                }
            }
            catch
            {
                if (ct.IsCancellationRequested)
                    return;

                if (errMaster == chd_error.CHDERR_NONE)
                    errMaster = chd_error.CHDERR_DECOMPRESSION_ERROR;
                ts.Cancel();
            }

        });
        allTasks.Add(hashingThread);

        Task.WaitAll(allTasks.ToArray());


        progress?.Invoke($"Verifying, 100% complete.");

        if (consoleOut != null)
        {
            arrPoolIn.ReadStats(out int issuedArraysTotal, out int returnedArraysTotal);
            consoleOut.Invoke($"In: Issued Arrays Total {issuedArraysTotal},  returned Arrays Total {returnedArraysTotal}, block size {chd.blocksize}");
            arrPoolOut.ReadStats(out issuedArraysTotal, out returnedArraysTotal);
            consoleOut.Invoke($"Out: Issued Arrays Total {issuedArraysTotal},  returned Arrays Total {returnedArraysTotal}, block size {chd.blocksize}");
            arrPoolCache.ReadStats(out issuedArraysTotal, out returnedArraysTotal);
            consoleOut.Invoke($"Cache: Issued Arrays Total {issuedArraysTotal},  returned Arrays Total {returnedArraysTotal}, block size {chd.blocksize}");
        }

        if (errMaster != chd_error.CHDERR_NONE)
            return errMaster;

        byte[] tmp = new byte[0];
        md5Check?.TransformFinalBlock(tmp, 0, 0);
        sha1Check?.TransformFinalBlock(tmp, 0, 0);

        // here it is now using the rawsha1 value from the header to validate the raw binary data.
        if (chd.md5 != null && !Util.IsAllZeroArray(chd.md5) && !Util.ByteArrEquals(chd.md5, md5Check.Hash))
        {
            return chd_error.CHDERR_DECOMPRESSION_ERROR;
        }
        if (chd.rawsha1 != null && !Util.IsAllZeroArray(chd.rawsha1) && !Util.ByteArrEquals(chd.rawsha1, sha1Check.Hash))
        {
            return chd_error.CHDERR_DECOMPRESSION_ERROR;
        }

        return chd_error.CHDERR_NONE;
    }

}
