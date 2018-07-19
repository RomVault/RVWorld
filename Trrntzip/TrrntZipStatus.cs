using System;

namespace Trrntzip
{
    [Flags]
    public enum TrrntZipStatus
    {
        Unknown = 0,
        ValidTrrntzip = 1,
        CorruptZip = 2,
        NotTrrntzipped = 4,
        BadDirectorySeparator = 8,
        Unsorted = 16,
        ExtraDirectoryEnteries = 32,
        RepeatFilesFound = 64
    }
}