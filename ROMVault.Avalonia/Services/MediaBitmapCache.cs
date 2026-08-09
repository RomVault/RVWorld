using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace ROMVault.Avalonia.Services;

public sealed class MediaBitmapLease : IDisposable
{
    private Action? _release;

    internal MediaBitmapLease(Bitmap bitmap, Action release)
    {
        Bitmap = bitmap;
        _release = release;
    }

    public Bitmap Bitmap { get; }

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

/// <summary>
/// Size-limited LRU cache whose leases keep displayed bitmaps alive during eviction.
/// </summary>
public sealed class MediaBitmapCache : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _usage = new();
    private readonly long _maximumBytes;
    private long _estimatedBytes;
    private bool _disposed;

    public MediaBitmapCache(long maximumBytes = 192L * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumBytes = maximumBytes;
    }

    public async Task<MediaBitmapLease> AcquireAsync(
        string key,
        byte[] encodedBytes,
        int decodeWidth,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(encodedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decodeWidth);

        MediaBitmapLease? cached = TryAcquire(key);
        if (cached is not null)
        {
            return cached;
        }

        Bitmap bitmap = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream(encodedBytes, writable: false);
            return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
        }, cancellationToken);

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_entries.TryGetValue(key, out Entry? existing))
            {
                bitmap.Dispose();
                Touch(existing);
                existing.LeaseCount++;
                return CreateLease(existing);
            }

            long estimatedSize = Math.Max(
                1,
                (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4);
            var node = _usage.AddFirst(key);
            var entry = new Entry(key, bitmap, estimatedSize, node) { LeaseCount = 1 };
            _entries.Add(key, entry);
            _estimatedBytes += estimatedSize;
            EvictUnusedEntries();
            return CreateLease(entry);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Entry entry in _entries.Values)
            {
                entry.Bitmap.Dispose();
            }

            _entries.Clear();
            _usage.Clear();
            _estimatedBytes = 0;
        }
    }

    private MediaBitmapLease? TryAcquire(string key)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(key, out Entry? entry))
            {
                return null;
            }

            Touch(entry);
            entry.LeaseCount++;
            return CreateLease(entry);
        }
    }

    private MediaBitmapLease CreateLease(Entry entry) =>
        new(entry.Bitmap, () => Release(entry.Key));

    private void Release(string key)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(key, out Entry? entry))
            {
                return;
            }

            if (entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }

            EvictUnusedEntries();
        }
    }

    private void Touch(Entry entry)
    {
        _usage.Remove(entry.Node);
        _usage.AddFirst(entry.Node);
    }

    private void EvictUnusedEntries()
    {
        LinkedListNode<string>? node = _usage.Last;
        while (_estimatedBytes > _maximumBytes && node is not null)
        {
            LinkedListNode<string>? previous = node.Previous;
            Entry entry = _entries[node.Value];
            if (entry.LeaseCount == 0)
            {
                _entries.Remove(entry.Key);
                _usage.Remove(node);
                _estimatedBytes -= entry.EstimatedBytes;
                entry.Bitmap.Dispose();
            }

            node = previous;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class Entry
    {
        public Entry(
            string key,
            Bitmap bitmap,
            long estimatedBytes,
            LinkedListNode<string> node)
        {
            Key = key;
            Bitmap = bitmap;
            EstimatedBytes = estimatedBytes;
            Node = node;
        }

        public string Key { get; }
        public Bitmap Bitmap { get; }
        public long EstimatedBytes { get; }
        public LinkedListNode<string> Node { get; }
        public int LeaseCount { get; set; }
    }
}
