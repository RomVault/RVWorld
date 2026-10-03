using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SAM_UI_Avalonia.Services;

/// <inheritdoc cref="IFileScanService"/>
public sealed class FileScanService : IFileScanService
{
    public Task<IReadOnlyList<ScannedFile>> ExpandAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        // Snapshot the input so the background walk is not affected by the caller.
        List<string> roots = new(paths);

        return Task.Run<IReadOnlyList<ScannedFile>>(() => Expand(roots, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<ScannedFile> Expand(List<string> roots, CancellationToken cancellationToken)
    {
        List<ScannedFile> results = new();

        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(root))
                AddDirectory(root, results, cancellationToken);
            else if (File.Exists(root))
                AddFile(root, results);
        }

        return results;
    }

    /// <summary>
    /// Walks a directory tree iteratively so that a single unreadable folder
    /// does not abort the whole scan, and deep trees cannot overflow the stack.
    /// </summary>
    private static void AddDirectory(string root, List<ScannedFile> results, CancellationToken cancellationToken)
    {
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string current = pending.Pop();

            try
            {
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddFile(file, results);
                }

                foreach (string directory in Directory.EnumerateDirectories(current))
                {
                    pending.Push(directory);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders we are not allowed to read.
            }
            catch (DirectoryNotFoundException)
            {
                // Skip folders removed while the scan was running.
            }
            catch (IOException)
            {
                // Skip folders that cannot be read (for example a disconnected drive).
            }
        }
    }

    private static void AddFile(string path, List<ScannedFile> results)
    {
        try
        {
            FileInfo info = new(path);
            results.Add(new ScannedFile(info.FullName, info.Length));
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }
}
