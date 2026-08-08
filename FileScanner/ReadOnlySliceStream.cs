using System;
using System.IO;

namespace FileScanner;

public sealed class ReadOnlySliceStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public ReadOnlySliceStream(Stream baseStream, long start, long length)
    {
        if (baseStream == null)
            throw new ArgumentNullException(nameof(baseStream));
        if (!baseStream.CanSeek)
            throw new ArgumentException("Base stream must support seeking.", nameof(baseStream));
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _baseStream = baseStream;
        _start = start;
        _length = length;
        _position = 0;
        _baseStream.Seek(_start, SeekOrigin.Begin);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
            return 0;

        long remaining = _length - _position;
        if (count > remaining)
            count = (int)remaining;

        _baseStream.Seek(_start + _position, SeekOrigin.Begin);
        int read = _baseStream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPos = offset;
                break;
            case SeekOrigin.Current:
                newPos = _position + offset;
                break;
            case SeekOrigin.End:
                newPos = _length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin));
        }

        if (newPos < 0)
            throw new IOException("Attempted to seek before beginning of stream.");
        if (newPos > _length)
            newPos = _length;

        _position = newPos;
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
