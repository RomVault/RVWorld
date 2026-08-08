using CHDSharpLib.Utils;
using System;
using System.IO;

namespace CHDSharpLib;

public sealed class ChdLogicalStream : Stream
{
    private readonly FileStream _file;
    private readonly CHDHeader _chd;
    private readonly ArrayPool _arrPool;
    private readonly CHDCodec _codec;
    private readonly byte[] _blockBuffer;

    private uint _blockIndex;
    private int _blockPos;
    private ulong _position;
    private bool _disposed;

    public static ChdLogicalStream OpenRead(string chdPath)
    {
        if (string.IsNullOrWhiteSpace(chdPath))
            throw new ArgumentNullException(nameof(chdPath));
        return new ChdLogicalStream(chdPath);
    }

    private ChdLogicalStream(string chdPath)
    {
        _file = System.IO.File.OpenRead(chdPath);
        try
        {
            if (!CHD.CheckHeader(_file, out _, out uint version))
                throw new InvalidDataException("Invalid CHD header.");

            CHDHeader chd;
            chd_error error;
            switch (version)
            {
                case 1:
                    error = CHDHeaders.ReadHeaderV1(_file, out chd);
                    break;
                case 2:
                    error = CHDHeaders.ReadHeaderV2(_file, out chd);
                    break;
                case 3:
                    error = CHDHeaders.ReadHeaderV3(_file, out chd);
                    break;
                case 4:
                    error = CHDHeaders.ReadHeaderV4(_file, out chd);
                    break;
                case 5:
                    error = CHDHeaders.ReadHeaderV5(_file, out chd);
                    break;
                default:
                    throw new NotSupportedException("Unsupported CHD version: " + version);
            }

            if (error != chd_error.CHDERR_NONE)
                throw new InvalidDataException("CHD header read failed: " + error);

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
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => (long)_chd.totalbytes;
    public override long Position
    {
        get => (long)_position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ChdLogicalStream));
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
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

    private void EnsureBlockLoaded()
    {
        if (_blockPos != 0)
            return;

        if (_blockIndex >= _chd.totalblocks)
            return;

        mapentry mapEntry = _chd.map[_blockIndex];
        bool rented = false;
        try
        {
            if (mapEntry.length > 0)
            {
                mapEntry.buffIn = _arrPool.Rent();
                rented = true;
                _file.Seek((long)mapEntry.offset, SeekOrigin.Begin);
                int total = 0;
                while (total < (int)mapEntry.length)
                {
                    int read = _file.Read(mapEntry.buffIn, total, (int)mapEntry.length - total);
                    if (read <= 0)
                        throw new EndOfStreamException("Unexpected end of CHD block data.");
                    total += read;
                }
            }

            chd_error err = CHDBlockRead.ReadBlock(mapEntry, _arrPool, _chd.chdReader, _codec, _blockBuffer, (int)_chd.blocksize);
            if (err != chd_error.CHDERR_NONE)
                throw new InvalidDataException("CHD decompression error: " + err);
        }
        finally
        {
            if (rented)
            {
                _arrPool.Return(mapEntry.buffIn);
                mapEntry.buffIn = null;
            }
        }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

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
