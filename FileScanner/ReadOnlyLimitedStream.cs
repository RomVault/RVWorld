using System;
using System.IO;

namespace FileScanner;

/// <summary>
/// Read-only stream wrapper that limits reads to the first N bytes of an underlying stream.
/// </summary>
public sealed class ReadOnlyLimitedStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _length;
    private long _position;

    /// <summary>
    /// Creates a read-only view of the provided stream limited to <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="baseStream">Underlying stream to read from.</param>
    /// <param name="length">Maximum number of readable bytes.</param>
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

    /// <inheritdoc />
    public override bool CanRead => true;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => _length;
    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
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
}
