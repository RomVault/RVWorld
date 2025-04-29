using CodePage;
using Compress.ZipFile;
using SortMethods;
using System;
using System.IO;

namespace Compress.StructuredZip
{
    public class StructuredZip : Zip, ICompress
    {
        public new ZipStructure ZipStruct { get; private set; }

        public new ZipReturn ZipFileOpen(string newFilename, long timestamp, bool readHeaders, int buffer = 4096)
        {
            ZipReturn zr = base.ZipFileOpen(newFilename, timestamp, readHeaders, buffer);
            if (zr != ZipReturn.ZipGood)
                return zr;

            ZipStruct = ValidateStructure();


            if (ExtraDataFoundOnEndOfFile || offset != 0)
                ZipStruct = ZipStructure.None;

            return ZipReturn.ZipGood;
        }

        public new ZipReturn ZipFileOpen(Stream inStream)
        {
            ZipReturn zr = base.ZipFileOpen(inStream);
            if (zr != ZipReturn.ZipGood)
                return zr;

            ZipStruct = ValidateStructure();
            return ZipReturn.ZipGood;
        }


        public new ZipReturn ZipFileCreate(string newFilename, ZipStructure zipType)
        {
            ZipStruct = zipType;
            ZipReturn zr = base.ZipFileCreate(newFilename);

            return zr;
        }

        public new ZipReturn ZipFileCreate(Stream zipFs, ZipStructure zipType)
        {
            ZipStruct = zipType;
            ZipReturn zr = base.ZipFileCreate(zipFs);

            return zr;
        }


        public new void ZipFileClose()
        {
            switch (ZipOpen)
            {
                case ZipOpenType.Closed:
                    return;

                case ZipOpenType.OpenRead:
                    zipFileCloseRead();
                    return;

                default:
                    int crc = CentralDirectoryWrite();

                    bool structureValid = true;
                    for (int i = 0; i < LocalFilesCount; i++)
                        structureValid &= ValidateFileHeader(ZipStruct, (ZipFileData)GetFileHeader(i));

                    if (!structureValid)
                    {
                        FileComment = "";
                        ZipStruct = ZipStructure.None;
                    }
                    else
                    {
                        FileComment = WriteComments(ZipStruct, crc);
                    }

                    EndOfCentralDirectoryWrite();
                    zipFileCloseWrite();
                    break;
            }
        }
        public void ZipCreateFake(ZipStructure zipType)
        {
            ZipStruct = zipType;
            base.ZipCreateFake();
        }

        public void ZipFileCloseFake(ulong fileOffset, out byte[] centralDir)
        {
            centralDir = null;
            if (ZipOpen != ZipOpenType.OpenFakeWrite)
            {
                return;
            }

            ZipFileFakeOpenMemoryStream();

            int crc = CentralDirectoryWrite(fileOffset);

            bool structureValid = true;
            for (int i = 0; i < LocalFilesCount; i++)
                structureValid &= ValidateFileHeader(ZipStruct, (ZipFileData)GetFileHeader(i));

            if (!structureValid)
            {
                FileComment = "";
                ZipStruct = ZipStructure.None;
            }
            else
            {
                FileComment = WriteComments(ZipStruct, crc);
            }

            EndOfCentralDirectoryWrite(fileOffset);
            centralDir = ZipFileFakeCloseMemoryStream();
        }


        public new ZipReturn ZipFileOpenWriteStream(bool raw, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, long? modTime = null, int? threadCount = null)
        {
            stream = null;
            ZipReturn zipValid = ZipReturn.ZipGood;

            //invalid torrentZip Input If:


            ushort expectedComressionMethod = StructuredArchive.GetCompressionType(ZipStruct);
            if (compressionMethod != expectedComressionMethod) zipValid = ZipReturn.ZipTrrntzipIncorrectCompressionUsed;


            int localFilesCount = LocalFilesCount;
            if (localFilesCount > 0)
            {
                // check that filenames are in trrntzip order
                string lastFilename = GetFileHeader(localFilesCount - 1).Filename;
                if (Sorters.TrrntZipStringCompare(lastFilename, filename) > 0)
                    zipValid = ZipReturn.ZipTrrntzipIncorrectFileOrder;

                // this should be move out to a fuction
                if (ZipStruct == ZipStructure.ZipTrrnt || ZipStruct == ZipStructure.ZipZSTD)
                {
                    // check that no un-needed directory entries are added
                    if (GetFileHeader(localFilesCount - 1).IsDirectory && filename.Length > lastFilename.Length)
                    {
                        if (Sorters.TrrntZipStringCompare(lastFilename, filename.Substring(0, lastFilename.Length)) == 0)
                        {
                            zipValid = ZipReturn.ZipTrrntzipIncorrectDirectoryAddedToZip;
                        }
                    }
                }
            }

            // this should be calling the zip date/time call
            if (ZipStruct == ZipStructure.ZipTrrnt)
                modTime = TrrntzipDosDateTime;
            else if (ZipStruct == ZipStructure.ZipZSTD)
                modTime = 0;

            // if we are requirering a trrrntzp file and it is not a trrntzip formated supplied stream then error out
            if (ZipStruct == ZipStructure.ZipTrrnt || ZipStruct == ZipStructure.ZipTDC || ZipStruct == ZipStructure.ZipZSTD)
            {
                if (zipValid != ZipReturn.ZipGood)
                    return zipValid;
            }
            ZipReturn zr = base.ZipFileOpenWriteStream(raw, filename, uncompressedSize, compressionMethod, out stream, modTime, threadCount);

            return zr;
        }
        internal ZipStructure ValidateStructure()
        {
            string lFileComment = FileComment;
            string zcrc = GetCRC();
            foreach (ZipStructure val in Enum.GetValues(typeof(ZipStructure)))
            {
                string id = StructuredArchive.GetZipCommentId(val);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (lFileComment.Length != id.Length + 8)
                    continue;

                if (lFileComment.Substring(0, id.Length) != id)
                    continue;

                if (lFileComment.Substring(id.Length) != zcrc)
                    continue;

                return validateFilesStructure(val);
            }
            return ZipStructure.None;
        }


        private ZipStructure validateFilesStructure(ZipStructure zipStructure)
        {
            int _localFilesCount = LocalFilesCount;

            for (int i = 0; i < _localFilesCount; i++)
            {
                if (!ValidateFileHeader(zipStructure, (ZipFileData)GetFileHeader(i)))
                    return ZipStructure.None;
            }

            for (int i = 0; i < _localFilesCount - 1; i++)
            {
                if (Sorters.TrrntZipStringCompare(GetFileHeader(i).Filename, GetFileHeader(i + 1).Filename) >= 0)
                    return ZipStructure.None;
            }

            // this should be move out to a fuction
            if (zipStructure == ZipStructure.ZipTrrnt || zipStructure == ZipStructure.ZipZSTD)
            {
                for (int i = 0; i < _localFilesCount - 1; i++)
                {
                    // see if we found a directory
                    string filename0 = GetFileHeader(i).Filename;
                    int filenameLength = filename0.Length;
                    if (filenameLength > 0 && filename0.Substring(filenameLength - 1, 1) != "/")
                        continue;

                    // see if the next file is in that directory
                    string filename1 = GetFileHeader(i + 1).Filename;
                    if (filename1.Length <= filename0.Length)
                        continue;

                    if (Sorters.TrrntZipStringCompare(filename0, filename1.Substring(0, filename0.Length)) != 0)
                        continue;

                    // if we found a file in the directory then we do not need the directory entry
                    return ZipStructure.None;
                }
            }
            return zipStructure;
        }


        internal static bool ValidateFileHeader(ZipStructure zipStructure, ZipFileData localFiles)
        {
            if (localFiles.GetStatus(LocalFileStatus.HeadersMismatch | LocalFileStatus.FilenameMisMatch | LocalFileStatus.DirectoryLengthError | LocalFileStatus.DateTimeMisMatch | LocalFileStatus.UnknownDataSource))
                return false;

            // Check: Version needed to extract?

            if (localFiles.ExtraDataFound)
                return false;

            ushort expectedComressionMethod = StructuredArchive.GetCompressionType(zipStructure);
            if (expectedComressionMethod != 8 && expectedComressionMethod != 93)
                return false;
            if (localFiles.CompressionMethod != expectedComressionMethod)
                return false;

            if (localFiles.Filename.Contains("\\"))
                return false;

            if (CodePage437.IsCodePage437(localFiles.Filename) != ((localFiles.GeneralPurposeBitFlag & (1 << 11)) == 0))
                return false;


            switch (StructuredArchive.GetZipDateTimeType(zipStructure))
            {
                case zipDateType.DateTime:
                    // any date time is good
                    break;
                case zipDateType.None:
                    if (localFiles.LastModified != 0)
                        return false;
                    break;
                case zipDateType.TrrntZip:
                    if (!IsTzipDate(localFiles.LastModified))
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }


        internal static string WriteComments(ZipStructure zipStruct, int crc)
        {
            string zipCommentId = StructuredArchive.GetZipCommentId(zipStruct);
            if (string.IsNullOrWhiteSpace(zipCommentId))
                return "";

            return zipCommentId + crc.ToString("X8");
        }


        public static long TrrntzipDateTime = 629870671200000000;
        public static long TrrntzipDosDateTime = 563657728;

        public static bool IsTzipDate(long ticks)
        {
            if (ticks <= 0xffffffff)
                return ticks == TrrntzipDosDateTime;
            else
                return ticks == TrrntzipDateTime;
        }


    }
}
