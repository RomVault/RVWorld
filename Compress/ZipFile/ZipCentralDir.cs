using System.IO;
using System.Text;

namespace Compress.ZipFile
{
    public partial class Zip
    {
        private const uint EndOfCentralDirSignature = 0x06054b50;
        private const uint Zip64EndOfCentralDirSignature = 0x06064b50;
        private const uint Zip64EndOfCentralDirectoryLocator = 0x07064b50;

        private ZipReturn FindEndOfCentralDirSignature()
        {
            long fileSize = _zipFs.Length;
            long maxBackSearch = 0xffff;

            if (_zipFs.Length < maxBackSearch)
            {
                maxBackSearch = fileSize;
            }

            const long buffSize = 0x400;

            byte[] buffer = new byte[buffSize + 4];

            long backPosition = 4;
            while (backPosition < maxBackSearch)
            {
                backPosition += buffSize;
                if (backPosition > maxBackSearch)
                {
                    backPosition = maxBackSearch;
                }

                long readSize = backPosition > buffSize + 4 ? buffSize + 4 : backPosition;

                _zipFs.Position = fileSize - backPosition;

                _zipFs.Read(buffer, 0, (int)readSize);


                for (long i = readSize - 4; i >= 0; i--)
                {
                    if (buffer[i] != 0x50 || buffer[i + 1] != 0x4b || buffer[i + 2] != 0x05 || buffer[i + 3] != 0x06)
                    {
                        continue;
                    }

                    _zipFs.Position = fileSize - backPosition + i;
                    return ZipReturn.ZipGood;
                }
            }
            return ZipReturn.ZipCentralDirError;
        }


        private ZipReturn EndOfCentralDirRead()
        {
            using BinaryReader zipBr = new(_zipFs, Encoding.UTF8, true);
            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != EndOfCentralDirSignature)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            ushort tUShort = zipBr.ReadUInt16(); // NumberOfThisDisk
            if (tUShort != 0)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            tUShort = zipBr.ReadUInt16(); // NumberOfThisDiskCenterDir
            if (tUShort != 0)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            _localFilesCount = zipBr.ReadUInt16(); // TotalNumberOfEntriesDisk

            tUShort = zipBr.ReadUInt16(); // TotalNumber of entries in the central directory 
            if (tUShort != _localFilesCount)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            _centralDirSize = zipBr.ReadUInt32(); // SizeOfCentralDir
            _centralDirStart = zipBr.ReadUInt32(); // Offset

            ushort zipFileCommentLength = zipBr.ReadUInt16();

            FileComment = zipBr.ReadBytes(zipFileCommentLength);

            if (_zipFs.Position != _zipFs.Length)
            {
                ZipStatus |= ZipStatus.ExtraData;
            }
            if (offset != 0)
            {
                ZipStatus |= ZipStatus.ExtraData;
            }

            return ZipReturn.ZipGood;
        }


        private void EndOfCentralDirWrite()
        {
            using BinaryWriter bw = new(_zipFs, Encoding.UTF8, true);
            bw.Write(EndOfCentralDirSignature);
            bw.Write((ushort)0); // NumberOfThisDisk
            bw.Write((ushort)0); // NumberOfThisDiskCenterDir
            bw.Write((ushort)(_localFiles.Count >= 0xffff ? 0xffff : _localFiles.Count)); // TotalNumberOfEntriesDisk
            bw.Write((ushort)(_localFiles.Count >= 0xffff ? 0xffff : _localFiles.Count)); // TotalNumber of entries in the central directory 
            bw.Write((uint)(_centralDirSize >= 0xffffffff ? 0xffffffff : _centralDirSize));
            bw.Write((uint)(_centralDirStart >= 0xffffffff ? 0xffffffff : _centralDirStart));
            bw.Write((ushort)FileComment.Length);
            bw.Write(FileComment, 0, FileComment.Length);
        }




        private ZipReturn Zip64EndOfCentralDirRead()
        {
            using BinaryReader zipBr = new(_zipFs, Encoding.UTF8, true);
            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != Zip64EndOfCentralDirSignature)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            ulong tULong = zipBr.ReadUInt64(); // Size of zip64 end of central directory record
            if (tULong != 44)
            {
                return ZipReturn.Zip64EndOfCentralDirError;
            }

            zipBr.ReadUInt16(); // version made by

            ushort tUShort = zipBr.ReadUInt16(); // version needed to extract
            if (tUShort != 45)
            {
                return ZipReturn.Zip64EndOfCentralDirError;
            }

            uint tUInt = zipBr.ReadUInt32(); // number of this disk
            if (tUInt != 0)
            {
                return ZipReturn.Zip64EndOfCentralDirError;
            }

            tUInt = zipBr.ReadUInt32(); // number of the disk with the start of the central directory
            if (tUInt != 0)
            {
                return ZipReturn.Zip64EndOfCentralDirError;
            }

            _localFilesCount = (uint)zipBr.ReadUInt64(); // total number of entries in the central directory on this disk

            tULong = zipBr.ReadUInt64(); // total number of entries in the central directory
            if (tULong != _localFilesCount)
            {
                return ZipReturn.Zip64EndOfCentralDirError;
            }

            _zip64 = true;
            _centralDirSize = zipBr.ReadUInt64(); // size of central directory

            _centralDirStart = zipBr.ReadUInt64(); // offset of start of central directory with respect to the starting disk number

            return ZipReturn.ZipGood;
        }


        private void Zip64EndOfCentralDirWrite()
        {
            using BinaryWriter bw = new(_zipFs, Encoding.UTF8, true);
            bw.Write(Zip64EndOfCentralDirSignature);
            bw.Write((ulong)44); // Size of zip64 end of central directory record
            bw.Write((ushort)45); // version made by
            bw.Write((ushort)45); // version needed to extract
            bw.Write((uint)0); // number of this disk
            bw.Write((uint)0); // number of the disk with the start of the central directory
            bw.Write((ulong)_localFiles.Count); // total number of entries in the central directory on this disk
            bw.Write((ulong)_localFiles.Count); // total number of entries in the central directory
            bw.Write(_centralDirSize); // size of central directory
            bw.Write(_centralDirStart); // offset of start of central directory with respect to the starting disk number
        }



        private ZipReturn Zip64EndOfCentralDirectoryLocatorRead()
        {
            using BinaryReader zipBr = new(_zipFs, Encoding.UTF8, true);
            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != Zip64EndOfCentralDirectoryLocator)
            {
                return ZipReturn.ZipEndOfCentralDirectoryError;
            }

            uint tUInt = zipBr.ReadUInt32(); // number of the disk with the start of the zip64 end of central directory
            if (tUInt != 0)
            {
                return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;
            }

            _endOfCentralDir64 = zipBr.ReadUInt64(); // relative offset of the zip64 end of central directory record

            tUInt = zipBr.ReadUInt32(); // total number of disks
            if (tUInt > 1)
            {
                return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;
            }

            return ZipReturn.ZipGood;
        }

        private void Zip64EndOfCentralDirectoryLocatorWrite()
        {
            using BinaryWriter bw = new(_zipFs, Encoding.UTF8, true);
            bw.Write(Zip64EndOfCentralDirectoryLocator);
            bw.Write((uint)0); // number of the disk with the start of the zip64 end of central directory
            bw.Write(_endOfCentralDir64); // relative offset of the zip64 end of central directory record
            bw.Write((uint)1); // total number of disks
        }


    }
}
