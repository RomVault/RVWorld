using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAM_UI_Avalonia.Services;

/// <summary>
/// A single file discovered by a scan.
/// </summary>
/// <param name="FullPath">Full path to the file.</param>
/// <param name="Length">Size of the file in bytes.</param>
public readonly record struct ScannedFile(string FullPath, long Length);

/// <summary>
/// Expands dropped files and directories into a flat list of files.
/// </summary>
public interface IFileScanService
{
    /// <summary>
    /// Recursively expands the supplied paths into the files they contain.
    /// Directories are walked recursively; files are returned as-is.
    /// Paths that do not exist, or that cannot be read, are skipped.
    /// </summary>
    Task<IReadOnlyList<ScannedFile>> ExpandAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default);
}
