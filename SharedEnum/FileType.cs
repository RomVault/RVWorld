﻿﻿/// <summary>
/// Classifies how a file is represented in RomVault's tree and scan pipelines.
/// </summary>
/// <remarks>
/// There are two related concepts:
/// - container nodes (directories, archives, CHDs)
/// - leaf nodes (regular files and members inside containers)
///
/// CHD support uses:
/// - <see cref="CHD"/> for the CHD container node
/// - <see cref="FileCHD"/> for virtual members (tracks / descriptors) contained by a CHD
/// </remarks>
public enum FileType
{
    /// <summary>
    /// Uninitialized / unknown.
    /// </summary>
    UnSet = 0,
    /// <summary>
    /// Filesystem directory.
    /// </summary>
    Dir = 1,
    /// <summary>
    /// Zip archive container.
    /// </summary>
    Zip = 2,
    /// <summary>
    /// 7z archive container.
    /// </summary>
    SevenZip = 3,
    /// <summary>
    /// Regular filesystem file.
    /// </summary>
    File = 4,
    /// <summary>
    /// A file entry inside a zip archive.
    /// </summary>
    FileZip = 5,
    /// <summary>
    /// A file entry inside a 7z archive.
    /// </summary>
    FileSevenZip = 6,
    /// <summary>
    /// CHD container node.
    /// </summary>
    CHD = 7,
    /// <summary>
    /// Virtual member inside a CHD container (e.g. a track or descriptor).
    /// </summary>
    FileCHD = 8,

    /// <summary>
    /// Sentinel value used by UI/filtering to mean "file types only" (no containers).
    /// </summary>
    FileOnly = 100
}

