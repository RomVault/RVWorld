using System.IO;
using Compress.Support.Compression.Deflate;
using Compress.Support.Utils;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong

namespace Compress.ZipFile
{
    public partial class Zip
    {
        private Stream _compressionStream;

        /*
         raw is true if we are just going to copy the raw data stream from the source to the destination zip file
         trrntzip is thue if the source zip is a valid trrntzip file
         compressionMethod must be set to 8 to make a valid trrntzip file.

         if raw is false then compressionMthod must be 0,8 or 93 (zstd)
         */

        public ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream, TimeStamps timeStamp = null)
        {
            stream = null;
            if (ZipOpen != ZipOpenType.OpenWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            ZipReturn validTrrntzip = ZipReturn.ZipGood;

            //invalid torrentZip Input If:
            if (compressionMethod != 8) validTrrntzip = ZipReturn.ZipTrrntzipIncorrectCompressionUsed;
            if (raw && !trrntzip) validTrrntzip = ZipReturn.ZipTrrntZipIncorrectDataStream;

            int localFilesCount = _localFiles.Count;
            if (localFilesCount > 0)
            {
                // check that filenames are in trrntzip order
                string lastFilename = _localFiles[localFilesCount - 1].Filename;
                if (CompressUtils.TrrntZipStringCompare(lastFilename, filename) > 0)
                    validTrrntzip = ZipReturn.ZipTrrntzipIncorrectFileOrder;

                // check that no un-needed directory entries are added
                if (_localFiles[localFilesCount - 1].IsDirectory && filename.Length > lastFilename.Length)
                {
                    if (CompressUtils.TrrntZipStringCompare(lastFilename, filename.Substring(0, lastFilename.Length)) == 0)
                    {
                        validTrrntzip = ZipReturn.ZipTrrntzipIncorrectDirectoryAddedToZip;
                    }
                }
            }

            // if we are requirering a trrrntzp file and it is not a trrntzip formated supplied stream then error out
            if (writeZipType == OutputZipType.TrrntZip)
            {
                if (validTrrntzip != ZipReturn.ZipGood)
                    return validTrrntzip;
            }

            ZipLocalFile lf = new ZipLocalFile(filename, timeStamp);
            lf.SetStatus(LocalFileStatus.TrrntZip, validTrrntzip == ZipReturn.ZipGood);

            ZipReturn retVal = lf.LocalFileOpenWriteStream(_zipFs, raw, uncompressedSize, compressionMethod, out stream);
            if (retVal != ZipReturn.ZipGood)
                return retVal;

            if (filename.Length > 0)
                lf.IsDirectory = (filename.Substring(filename.Length - 1, 1) == "/");

            _compressionStream = stream;
            _localFiles.Add(lf);

            return ZipReturn.ZipGood;
        }

        public void ZipFileAddZeroLengthFile()
        {
            ZipLocalFile.LocalFileAddZeroLengthFile(_zipFs);
        }


        public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
        {
            if (_compressionStream is ZlibBaseStream dfStream)
            {
                dfStream.Flush();
                dfStream.Close();
                dfStream.Dispose();
            }
            _compressionStream = null;

            return _localFiles[_localFiles.Count - 1].LocalFileCloseWriteStream(_zipFs, crc32);
        }

        public ZipReturn ZipFileRollBack()
        {
            if (ZipOpen != ZipOpenType.OpenWrite)
            {
                return ZipReturn.ZipWritingToInputFile;
            }

            int fileCount = _localFiles.Count;
            if (fileCount == 0)
            {
                return ZipReturn.ZipErrorRollBackFile;
            }

            _localFiles.RemoveAt(fileCount - 1);
            _zipFs.Position = (long)_localFiles[fileCount - 1].RelativeOffsetOfLocalHeader;
            return ZipReturn.ZipGood;
        }

    }
}
