using Compress;
using RomVaultCore.Utils;
using SortMethods;
using System.Collections.Generic;

namespace FileScanner;

public class ScannedFile
{
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

    public bool IsDirectory => FileType == FileType.Dir || FileType == FileType.Zip || FileType == FileType.SevenZip;

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