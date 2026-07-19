using Compress;
using SortMethods;
using StorageList;
using System;
using System.Collections.Generic;

namespace FileScanner;

public class ScannedFile
{
    private static readonly Comparison<ScannedFile> CompareDirectoryNames = CompareNameDir;
    private static readonly Comparison<ScannedFile> CompareZipNames = CompareNameTrrntZip;
    private static readonly Comparison<ScannedFile> CompareSevenZipNames = CompareName7Zip;

    // common

    public string Name;
    public long FileModTimeStamp;
    public FileType FileType;
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

    public bool SearchFound = false;

    public ScannedFile(FileType ft)
    {
        FileType = ft;
    }

    public void Add(ScannedFile child)
    {
        if (_scannedFiles == null)
            _scannedFiles = new List<ScannedFile>(1);
        _scannedFiles.Add(child);
    }
    public void AddRange(List<ScannedFile> list)
    {
        if (list.Count == 0)
            return;
        if (_scannedFiles == null)
            _scannedFiles = new List<ScannedFile>(list.Count);
        _scannedFiles.AddRange(list);
    }

    public int Count => _scannedFiles?.Count ?? 0;

    public ScannedFile this[int index] => _scannedFiles[index];

    public void FileStatusSet(FileStatus flag)
    {
        StatusFlags |= flag;
    }

    public bool IsDirectory => FileType == FileType.Dir || FileType == FileType.Zip || FileType == FileType.SevenZip;

    public void Sort()
    {
        if ((_scannedFiles?.Count ?? 0) <= 1)
            return;

        switch (FileType)
        {
            case FileType.SevenZip:
                _scannedFiles.Sort(CompareSevenZipNames);
                break;
            case FileType.Zip:
                _scannedFiles.Sort(CompareZipNames);
                break;
            case FileType.Dir:
                _scannedFiles.Sort(CompareDirectoryNames);
                break;
            default:
                throw new System.Exception("Unknown Archive Type in SortArchive");
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
