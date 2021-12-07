using System;

namespace Compress
{
    public enum ZipReturn
    {
        ZipGood,
        ZipFileLocked,
        ZipFileCountError,
        ZipSignatureError,
        ZipExtraDataOnEndOfZip,
        ZipUnsupportedCompression,
        ZipLocalFileHeaderError,
        ZipCentralDirError,
        ZipEndOfCentralDirectoryError,
        Zip64EndOfCentralDirError,
        Zip64EndOfCentralDirectoryLocatorError,
        ZipReadingFromOutputFile,
        ZipWritingToInputFile,
        ZipErrorGettingDataStream,
        ZipCRCDecodeError,
        ZipDecodeError,
        ZipFileNameToLong,
        ZipFileAlreadyOpen,
        ZipCannotFastOpen,
        ZipErrorOpeningFile,
        ZipErrorFileNotFound,
        ZipErrorReadingFile,
        ZipErrorTimeStamp,
        ZipErrorRollBackFile,
        ZipTryingToAccessADirectory,
        ZipErrorWritingToOutputStream,
        ZipTrrntzipIncorrectCompressionUsed,
        ZipTrrntzipIncorrectFileOrder,
        ZipTrrntzipIncorrectDirectoryAddedToZip,
        ZipTrrntZipIncorrectDataStream,
        ZipUntested

    }
    public enum OutputZipType
    {
        None,
        TrrntZip,
        rvZip
    }

    public enum ZipOpenType
    {
        Closed,
        OpenRead,
        OpenWrite,
        OpenFakeWrite
    }

    [Flags]
    public enum ZipStatus
    {
        None = 0x0,
        TrrntZip = 0x1, // for Zip this is a Trrntzip , for 7zip this is an rv7Zip
        ExtraData = 0x2,
        Trrnt7Zip = 0x4 // used by 7zip for a t7z
    }
}
