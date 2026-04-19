using System;
using System.IO;

namespace FileScanner;

/// <summary>
/// Read-only stream wrapper exposing a seekable slice of an underlying seekable stream.
/// </summary>
public sealed class ReadOnlySliceStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    /// <summary>
    /// Creates a stream view over <paramref name="baseStream"/> starting at <paramref name="start"/> and spanning <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="baseStream">Underlying stream to read from.</param>
    /// <param name="start">Start offset within <paramref name="baseStream"/>.</param>
    /// <param name="length">Length of the exposed slice.</param>
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

    /// <inheritdoc />
    public override bool CanRead => true;
    /// <inheritdoc />
    public override bool CanSeek => true;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => _length;
    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override void Flush()
    {
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
