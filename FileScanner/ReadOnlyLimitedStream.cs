using System;
using System.IO;

namespace FileScanner;

public sealed class ReadOnlyLimitedStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _length;
    private long _position;

    public ReadOnlyLimitedStream(Stream baseStream, long length)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (!baseStream.CanRead)
            throw new ArgumentException("Base stream must be readable.", nameof(baseStream));
        _length = length;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
            return 0;
        long remaining = _length - _position;
        if (count > remaining)
            count = (int)remaining;
        int read = _baseStream.Read(buffer, offset, count);
        _position += read;
        return read;
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
}
