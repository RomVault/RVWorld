using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Compress;
using Compress.ZipFile;
using RomVaultCore;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Services;

public sealed record MediaContent(
    byte[] Bytes,
    string CacheKey,
    RvFile Container,
    RvFile Entry);

public interface IMediaContentService
{
    Task<MediaContent?> ReadAsync(
        RvFile container,
        string searchPattern,
        int maximumBytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reads artwork and text entries without blocking the Avalonia UI thread.
/// </summary>
public sealed class MediaContentService : IMediaContentService
{
    public Task<MediaContent?> ReadAsync(
        RvFile container,
        string searchPattern,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        return Task.Run(
            () => ReadCore(container, searchPattern, maximumBytes, cancellationToken),
            cancellationToken);
    }

    private static MediaContent? ReadCore(
        RvFile container,
        string searchPattern,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Regex matcher = CreateMatcher(searchPattern);
        RvFile? entry = FindEntry(container, matcher);
        if (entry is null)
        {
            return null;
        }

        return container.FileType switch
        {
            FileType.Zip => ReadZipEntry(container, entry, maximumBytes, cancellationToken),
            FileType.Dir => ReadFileEntry(container, entry, maximumBytes, cancellationToken),
            _ => null
        };
    }

    private static RvFile? FindEntry(RvFile container, Regex matcher)
    {
        for (int index = 0; index < container.ChildCount; index++)
        {
            RvFile entry = container.Child(index);
            if (entry.GotStatus == GotStatus.Got && matcher.IsMatch(entry.Name))
            {
                return entry;
            }
        }

        return null;
    }

    private static MediaContent? ReadZipEntry(
        RvFile container,
        RvFile entry,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.ZipFileHeaderPosition is null)
        {
            return null;
        }

        var zip = new Zip();
        try
        {
            if (zip.ZipFileOpen(container.FullNameCase, container.FileModTimeStamp, false) != ZipReturn.ZipGood)
            {
                return null;
            }

            if (zip.ZipFileOpenReadStreamFromLocalHeaderPointer(
                    entry.ZipFileHeaderPosition.Value,
                    false,
                    out Stream stream,
                    out ulong streamSize,
                    out _) != ZipReturn.ZipGood)
            {
                return null;
            }

            using (stream)
            {
                if (streamSize > (ulong)maximumBytes || streamSize > int.MaxValue)
                {
                    return null;
                }

                byte[] bytes = ReadExactly(stream, (int)streamSize, cancellationToken);
                string cacheKey = $"zip|{container.FullNameCase}|{container.FileModTimeStamp}|{entry.Name}|{entry.ZipFileHeaderPosition}";
                return new MediaContent(bytes, cacheKey, container, entry);
            }
        }
        finally
        {
            zip.ZipFileClose();
        }
    }

    private static MediaContent? ReadFileEntry(
        RvFile container,
        RvFile entry,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        string path = entry.FullNameCase;
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        if (info.Length < 0 || info.Length > maximumBytes || info.Length > int.MaxValue)
        {
            return null;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        byte[] bytes = ReadExactly(stream, (int)info.Length, cancellationToken);
        string cacheKey = $"file|{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        return new MediaContent(bytes, cacheKey, container, entry);
    }

    private static byte[] ReadExactly(Stream stream, int length, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = stream.Read(bytes, offset, length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset != bytes.Length)
        {
            Array.Resize(ref bytes, offset);
        }

        return bytes;
    }

    internal static Regex CreateMatcher(string pattern)
    {
        if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            return new Regex(
                pattern[6..],
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }

        string expression = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return new Regex(
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }
}
