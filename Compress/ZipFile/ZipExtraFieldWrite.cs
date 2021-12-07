using System;
using System.Collections.Generic;

namespace Compress.ZipFile
{
    internal class ZipExtraFieldWrite
    {
        private readonly List<byte> _extraField;

        public ZipExtraFieldWrite()
        {
            _extraField = new List<byte>();
        }

        public byte[] ExtraField => _extraField.ToArray();

        public bool Zip64(ulong unCompressedSize, ulong compressedSize, ulong relativeOffsetOfLocalHeader, bool centralDir,
            out uint headerUnCompressedSize, out uint headerCompressedSize, out uint headerRelativeOffsetOfLocalHeader)
        {
            List<byte> eZip64 = new();

            if (!centralDir)
            {
                if (unCompressedSize >= 0xffffffff || compressedSize >= 0xffffffff)
                {
                    eZip64.AddRange(BitConverter.GetBytes(unCompressedSize));
                    eZip64.AddRange(BitConverter.GetBytes(compressedSize));
                    headerUnCompressedSize = 0xffffffff;
                    headerCompressedSize = 0xffffffff;
                }
                else
                {
                    headerUnCompressedSize = (uint)unCompressedSize;
                    headerCompressedSize = (uint)compressedSize;
                }

                headerRelativeOffsetOfLocalHeader = 0;
            }
            else
            {
                if (unCompressedSize >= 0xffffffff)
                {
                    headerUnCompressedSize = 0xffffffff;
                    eZip64.AddRange(BitConverter.GetBytes(unCompressedSize));
                }
                else
                {
                    headerUnCompressedSize = (uint)unCompressedSize;
                }
                if (compressedSize >= 0xffffffff)
                {
                    headerCompressedSize = 0xffffffff;
                    eZip64.AddRange(BitConverter.GetBytes(compressedSize));
                }
                else
                {
                    headerCompressedSize = (uint)compressedSize;
                }

                if (relativeOffsetOfLocalHeader >= 0xffffffff)
                {
                    headerRelativeOffsetOfLocalHeader = 0xffffffff;
                    eZip64.AddRange(BitConverter.GetBytes(relativeOffsetOfLocalHeader));
                }
                else
                {
                    headerRelativeOffsetOfLocalHeader = (uint)relativeOffsetOfLocalHeader;
                }

            }

            if (eZip64.Count == 0)
                return false;

            _extraField.AddRange(BitConverter.GetBytes((ushort)0x0001));
            _extraField.AddRange(BitConverter.GetBytes((ushort)eZip64.Count));
            _extraField.AddRange(eZip64);
            return true;
        }


        public void NtfsTime(long mTime, long aTime, long cTime)
        {
            _extraField.AddRange(BitConverter.GetBytes((ushort)0x000a));
            _extraField.AddRange(BitConverter.GetBytes((ushort)32));  // this block is 32 bytes long
            _extraField.AddRange(BitConverter.GetBytes((uint)0)); // Reserved
            _extraField.AddRange(BitConverter.GetBytes((ushort)0x0001)); // tag1 = 1
            _extraField.AddRange(BitConverter.GetBytes((ushort)24)); // size1  block size of date/times

            _extraField.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToNtfsDateTime(mTime)));
            _extraField.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToNtfsDateTime(aTime)));
            _extraField.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToNtfsDateTime(cTime)));
        }


        public void LinuxTime(long? mTime, long? aTime, long? cTime, bool centralDir)
        {
            List<byte> eTime = new();
            byte flags = 0;
            if (mTime != null)
            {
                flags |= 0x01;
                eTime.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToUnixDateTime((long)mTime)));
            }

            if (!centralDir)
            {
                if (aTime != null)
                {
                    flags |= 0x02;
                    eTime.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToUnixDateTime((long)aTime)));
                }
                if (cTime != null)
                {
                    flags |= 0x04;
                    eTime.AddRange(BitConverter.GetBytes(CompressUtils.UtcTicksToUnixDateTime((long)cTime)));
                }

            }

            if (flags == 0)
                return;

            _extraField.AddRange(BitConverter.GetBytes((ushort)0x5455));
            _extraField.AddRange(BitConverter.GetBytes((ushort)eTime.Count + 1));

            _extraField.Add(flags);
            _extraField.AddRange(eTime);
        }

    }
}
