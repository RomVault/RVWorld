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

    public enum ZipOpenType
    {
        Closed,
        OpenRead,
        OpenWrite,
        OpenFakeWrite
    }
}
