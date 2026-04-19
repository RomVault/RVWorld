using CHDSharpLib.Utils;
using System;
using System.IO;

namespace CHDSharpLib;

/// <summary>
/// Exposes the logical, decompressed byte stream represented by a CHD file.
/// </summary>
/// <remarks>
/// This stream reads CHD hunks sequentially and is intentionally non-seekable. RomVault uses it to hash
/// DVD/ISO contents and, when metadata is available, to hash CD tracks without writing temporary files.
/// </remarks>
public sealed class ChdLogicalStream : Stream
{
    /// <summary>
    /// Underlying CHD file stream.
    /// </summary>
    private readonly FileStream _file;

    /// <summary>
    /// Parsed CHD header and map used for decoding.
    /// </summary>
    private readonly CHDHeader _chd;

    /// <summary>
    /// Pool for temporary buffers used during decoding.
    /// </summary>
    private readonly ArrayPool _arrPool;

    /// <summary>
    /// Codec state container used across reads.
    /// </summary>
    private readonly CHDCodec _codec;

    /// <summary>
    /// Reusable block-sized output buffer.
    /// </summary>
    private readonly byte[] _blockBuffer;

    /// <summary>
    /// Current hunk index being streamed.
    /// </summary>
    private uint _blockIndex;

    /// <summary>
    /// Current offset into <see cref="_blockBuffer"/>.
    /// </summary>
    private int _blockPos;

    /// <summary>
    /// Current logical stream position, in bytes.
    /// </summary>
    private ulong _position;

    /// <summary>
    /// Disposal guard.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Opens a CHD file for logical (decompressed) sequential reading.
    /// </summary>
    /// <param name="chdPath">Path to the CHD file.</param>
    /// <returns>A non-seekable stream of the CHD's logical contents.</returns>
    public static ChdLogicalStream OpenRead(string chdPath)
    {
        if (string.IsNullOrWhiteSpace(chdPath))
            throw new ArgumentNullException(nameof(chdPath));
        return new ChdLogicalStream(chdPath);
    }

    private ChdLogicalStream(string chdPath)
    {
        _file = System.IO.File.OpenRead(chdPath);
        if (!CHD.CheckHeader(_file, out uint headerLen, out uint version))
            throw new InvalidDataException("Invalid CHD header.");

        CHDHeader chd;
        switch (version)
        {
            case 1:
                CHDHeaders.ReadHeaderV1(_file, out chd);
                break;
            case 2:
                CHDHeaders.ReadHeaderV2(_file, out chd);
                break;
            case 3:
                CHDHeaders.ReadHeaderV3(_file, out chd);
                break;
            case 4:
                CHDHeaders.ReadHeaderV4(_file, out chd);
                break;
            case 5:
                CHDHeaders.ReadHeaderV5(_file, out chd);
                break;
            default:
                throw new NotSupportedException("Unsupported CHD version: " + version);
        }

        _chd = chd;
        CHDBlockRead.FindRepeatedBlocks(_chd, null);
        CHDBlockRead.FindBlockReaders(_chd);

        _arrPool = new ArrayPool(_chd.blocksize);
        _codec = new CHDCodec();
        _blockBuffer = new byte[_chd.blocksize];

        _blockIndex = 0;
        _blockPos = 0;
        _position = 0;
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => (long)_chd.totalbytes;
    /// <inheritdoc />
    public override long Position
    {
        get => (long)_position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ChdLogicalStream));
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        if (_position >= _chd.totalbytes)
            return 0;

        int totalRead = 0;
        while (count > 0 && _position < _chd.totalbytes)
        {
            EnsureBlockLoaded();

            int blockAvail = (int)_chd.blocksize - _blockPos;
            ulong remainingTotal = _chd.totalbytes - _position;
            int avail = (int)Math.Min((ulong)blockAvail, remainingTotal);
            int toCopy = Math.Min(avail, count);
            Array.Copy(_blockBuffer, _blockPos, buffer, offset, toCopy);

            offset += toCopy;
            count -= toCopy;
            totalRead += toCopy;

            _blockPos += toCopy;
            _position += (ulong)toCopy;

            if (_blockPos >= (int)_chd.blocksize)
            {
                _blockIndex++;
                _blockPos = 0;
            }
        }

        return totalRead;
    }

    /// <summary>
    /// Ensures the current hunk is decoded into <see cref="_blockBuffer"/> when starting a new hunk.
    /// </summary>
    private void EnsureBlockLoaded()
    {
        if (_blockPos != 0)
            return;

        if (_blockIndex >= _chd.totalblocks)
            return;

        mapentry mapEntry = _chd.map[_blockIndex];
        if (mapEntry.length > 0)
        {
            mapEntry.buffIn = _arrPool.Rent();
            _file.Seek((long)mapEntry.offset, SeekOrigin.Begin);
            _file.Read(mapEntry.buffIn, 0, (int)mapEntry.length);
        }

        chd_error err = CHDBlockRead.ReadBlock(mapEntry, _arrPool, _chd.chdReader, _codec, _blockBuffer, (int)_chd.blocksize);
        if (err != chd_error.CHDERR_NONE)
            throw new InvalidDataException("CHD decompression error: " + err);

        if (mapEntry.length > 0)
        {
            _arrPool.Return(mapEntry.buffIn);
            mapEntry.buffIn = null;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            try { _file.Dispose(); } catch { }
        }
        _disposed = true;
        base.Dispose(disposing);
    }
}
