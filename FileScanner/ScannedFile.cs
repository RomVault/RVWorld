using Compress;
using RomVaultCore.Utils;
using SortMethods;
using System.Collections.Generic;

namespace FileScanner;

/// <summary>
/// Represents the result of scanning a filesystem node or a container member.
/// </summary>
/// <remarks>
/// This is the transient data model produced by the scanner and later merged into the DB tree
/// (<see cref="RomVaultCore.RvDB.RvFile"/>).
///
/// For container types (directory/zip/7z/CHD), child entries are stored in <see cref="_scannedFiles"/>.
/// For CHDs, the container node is <see cref="FileType.CHD"/> and member entries are emitted as
/// <see cref="FileType.FileCHD"/> so the merge pipeline can treat them like archive members.
/// </remarks>
public class ScannedFile
{
    // common

    /// <summary>
    /// Name of the file or container member, relative to its parent container.
    /// </summary>
    public string Name;
    /// <summary>
    /// Timestamp used to validate scan results against the filesystem.
    /// </summary>
    public long FileModTimeStamp;
    /// <summary>
    /// The scanned node type.
    /// </summary>
    public FileType FileType;
    /// <summary>
    /// Whether the content is considered present and valid.
    /// </summary>
    public GotStatus GotStatus;

    // directory or archived directory 

    public ZipStructure ZipStruct;
    public string Comment;
    private List<ScannedFile> _scannedFiles;


    // file or archived file 
    public ulong? LocalHeaderOffset;

    public bool DeepScanned;
    public FileStatus StatusFlags;

    public int Index;

    public HeaderFileType HeaderFileType;
    public ulong? Size;
    public byte[] CRC;
    public byte[] SHA1;
    public byte[] MD5;
    public byte[] SHA256;

    public ulong? AltSize;
    public byte[] AltCRC;
    public byte[] AltSHA1;
    public byte[] AltMD5;
    public byte[] AltSHA256;

    public uint? CHDVersion;
    /// <summary>
    /// High-level CHD scan status string intended for UI/tooltips.
    /// </summary>
    public string? ChdStatus;
    /// <summary>
    /// Scan method used (e.g. streaming vs extraction).
    /// </summary>
    public string? ChdScanMethod;
    /// <summary>
    /// Hash match mode used when mapping CHD members to DAT expectations.
    /// </summary>
    public string? ChdHashMatchMode;
    /// <summary>
    /// Descriptor matching mode (external/synthetic/true) when a CUE/GDI is involved.
    /// </summary>
    public string? ChdDescriptorMatch;

    public bool SearchFound = false;

    public ScannedFile(FileType ft)
    {
        FileType = ft;
        if (IsDirectory)
            _scannedFiles = [];
    }

    public void Add(ScannedFile child)
    {
        _scannedFiles.Add(child);
    }
    public void AddRange(List<ScannedFile> list)
    {
        _scannedFiles.AddRange(list);
    }

    public int Count => _scannedFiles.Count;

    public ScannedFile this[int index] => _scannedFiles[index];

    public void FileStatusSet(FileStatus flag)
    {
        StatusFlags |= flag;
    }

    /// <summary>
    /// Returns true when this scanned node is a container (directory, archive, or CHD).
    /// </summary>
    public bool IsDirectory => FileType == FileType.Dir || FileType == FileType.Zip || FileType == FileType.SevenZip || FileType == FileType.CHD;

    public void Sort()
    {
        ScannedFile[] files = _scannedFiles.ToArray();
        _scannedFiles.Clear();

        compareFunc<ScannedFile> cf;
        switch (FileType)
        {
            case FileType.SevenZip:
                cf = CompareName7Zip;
                break;
            case FileType.Zip:
                cf = CompareNameTrrntZip;
                break;
            case FileType.CHD:
            case FileType.Dir:
                cf = CompareNameDir;
                break;
            default:
                throw new System.Exception("Unknown Archive Type in SortArchive");
        }
        foreach (ScannedFile file in files)
        {
            int found = BinarySearch.ListSearch(_scannedFiles, file, cf, out int index);
            _scannedFiles.Insert(index, file);
        }
    }
    private static int CompareNameDir(ScannedFile var1, ScannedFile var2)
    {
        return Sorters.DirectoryNameCompareCase(var1.Name, var2.Name);
    }
    private static int CompareNameTrrntZip(ScannedFile var1, ScannedFile var2)
    {
        return Sorters.TrrntZipStringCompareCase(var1.Name, var2.Name);
    }
    private static int CompareName7Zip(ScannedFile var1, ScannedFile var2)
    {
        return Sorters.Trrnt7ZipStringCompare(var1.Name, var2.Name);
    }

}
