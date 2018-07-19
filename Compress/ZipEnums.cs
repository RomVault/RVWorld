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
        ZipCenteralDirError,
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
        ZipUntested

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
        TrrntZip = 0x1,
        ExtraData = 0x2,
        Trrnt7Zip = 0x4
    }
}
