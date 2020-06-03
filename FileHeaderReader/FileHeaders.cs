using System.Collections.Generic;
using System.IO;

namespace FileHeaderReader
{
    public enum HeaderFileType
    {
        Nothing = 0,
        ZIP,
        GZ,
        SevenZip,
        RAR,

        CHD,

        A7800,
        Lynx,
        NES,
        FDS,
        PCE,
        PSID,
        SNES,
        SPC
    }

    public static class FileHeaderReader
    {
        private static readonly List<Detector> Detectors;

        static FileHeaderReader()
        {
            Detectors = new List<Detector>
            {
                // Standard archive types
                new Detector(HeaderFileType.ZIP, 22, 0, "", new Data(0, new byte[] {0x50, 0x4b, 0x03, 0x04})),
                new Detector(HeaderFileType.GZ, 18, 0, "", new Data(0, new byte[] {0x1f, 0x8b, 0x08})),
                new Detector(HeaderFileType.SevenZip, 6, 0, "", new Data(0, new byte[] {0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C})),
                new Detector(HeaderFileType.RAR, 6, 0, "", new Data(0, new byte[] {0x52, 0x61, 0x72, 0x21, 0x1A, 0x07})),

                // CHD
                new Detector(HeaderFileType.CHD, 76, 0, "", new Data(0, new[] {(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'})),

                // Headered files
                new Detector(HeaderFileType.A7800, 128, 128, "No-Intro_A7800.xml", new Data(1, new byte[] {0x41, 0x54, 0x41, 0x52, 0x49, 0x37, 0x38, 0x30, 0x30})),
                new Detector(HeaderFileType.A7800, 128, 128, "No-Intro_A7800.xml", new Data(100, new byte[] {0x41, 0x43, 0x54, 0x55, 0x41, 0x4C, 0x20, 0x43, 0x41, 0x52, 0x54, 0x20, 0x44, 0x41, 0x54, 0x41, 0x20, 0x53, 0x54, 0x41, 0x52, 0x54, 0x53, 0x20, 0x48, 0x45, 0x52, 0x45})),
                new Detector(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
                new Detector(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
                new Detector(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
                new Detector(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
                new Detector(HeaderFileType.FDS, 16, 16, "No-Intro_FDS.xml", new Data(0, new byte[] {0x46, 0x44, 0x53})),
                new Detector(HeaderFileType.Lynx, 64, 64, "No-Intro_LNX.xml", new Data(0, new byte[] {0x4C, 0x59, 0x4E, 0x58})),
                new Detector(HeaderFileType.Lynx, 64, 64, "No-Intro_LNX.xml", new Data(6, new byte[] {0x42, 0x53, 0x39})),
                new Detector(HeaderFileType.NES, 16, 16, "No-Intro_NES.xml", new Data(0, new byte[] {0x4E, 0x45, 0x53, 0x1A})),
                new Detector(HeaderFileType.NES, 16, 16, "NonGoodNES.xml", new Data(0, new byte[] {0x4E, 0x45, 0x53, 0x1A})),
                new Detector(HeaderFileType.NES, 16, 16, "nes.xml", new Data(0, new byte[] {0x4E, 0x45, 0x53, 0x1A})),
                new Detector(HeaderFileType.NES, 16, 16, "nes0.xml", new Data(0, new byte[] {0x4E, 0x45, 0x53, 0x1A})),
                new Detector(HeaderFileType.PCE, 512, 512, "pce.xml", new Data(0, new byte[] {0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0x02})),
                new Detector(HeaderFileType.PSID, 118, 118, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x76})),
                new Detector(HeaderFileType.PSID, 118, 118, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x03, 0x00, 0x7c})),
                new Detector(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c})),
                new Detector(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x7c})),
                new Detector(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x52, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c})),
                new Detector(HeaderFileType.SNES, 512, 512, "snes.xml", new Data(22, new byte[] {0xAA, 0xBB, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00})),
                new Detector(HeaderFileType.SNES, 512, 512, "snes.xml", new Data(22, new byte[] {0x53, 0x55, 0x50, 0x45, 0x52, 0x55, 0x46, 0x4F})),
                new Detector(HeaderFileType.SPC, 256, 256, "spc.xml", new Data(0, new byte[] {0x53, 0x4E, 0x45, 0x53, 0x2D, 0x53, 0x50, 0x43}))
            };
        }

        public static int GetFileHeaderLength(HeaderFileType FType)
        {
            foreach (Detector d in Detectors)
            {
                if (d.FType == FType)
                    return d.FileOffset;
            }

            return 0;
        }

        public static HeaderFileType GetFileTypeFromHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return HeaderFileType.Nothing;

            string theader = header.ToLower();
            foreach (Detector d in Detectors)
            {
                if (string.IsNullOrEmpty(d.HeaderId))
                {
                    continue;
                }

                if (theader == d.HeaderId.ToLower())
                {
                    return d.FType;
                }
            }
            return HeaderFileType.Nothing;
        }

        public static bool AltHeaderFile(HeaderFileType fileType)
        {
            return (fileType == HeaderFileType.A7800) ||
                   (fileType == HeaderFileType.FDS) ||
                   (fileType == HeaderFileType.Lynx) ||
                   (fileType == HeaderFileType.NES) ||
                   (fileType == HeaderFileType.PCE) ||
                   (fileType == HeaderFileType.PSID) ||
                   (fileType == HeaderFileType.SNES) ||
                   (fileType == HeaderFileType.SPC) ||
                   (fileType == HeaderFileType.CHD);
        }

        public static HeaderFileType GetType(Stream sIn, out int offset)
        {
            int headerSize = 128;
            if (sIn.Length < headerSize)
            {
                headerSize = (int)sIn.Length;
            }

            byte[] buffer = new byte[headerSize];

            headerSize = sIn.Read(buffer, 0, headerSize);

            return GetType(buffer, headerSize, out offset);
        }

        public static HeaderFileType GetType(byte[] buffer, int headerSize, out int offset)
        {
            foreach (Detector detector in Detectors)
            {
                if (headerSize < detector.Data.Value.Length + detector.Data.Offset)
                {
                    continue;
                }

                if (ByteComp(buffer, detector.Data))
                {
                    offset = detector.FileOffset;
                    return detector.FType;
                }
            }

            offset = 0;
            return HeaderFileType.Nothing;
        }

        private static bool ByteComp(byte[] buffer, Data d)
        {
            if (buffer.Length < d.Value.Length + d.Offset)
            {
                return false;
            }
            for (int i = 0; i < d.Value.Length; i++)
            {
                if (buffer[i + d.Offset] != d.Value[i])
                {
                    return false;
                }
            }
            return true;
        }

        private class Detector
        {
            public readonly HeaderFileType FType;
            public readonly int HeaderLength;
            public readonly int FileOffset;
            public readonly string HeaderId;
            public readonly Data Data;


            public Detector(HeaderFileType fType, int headerLength, int fileOffset, string headerId, Data data)
            {
                FType = fType;
                HeaderLength = headerLength;
                FileOffset = fileOffset;
                HeaderId = headerId.ToLower();
                Data = data;
            }
        }

        private class Data
        {
            public readonly int Offset;
            public readonly byte[] Value;

            public Data(int offset, byte[] value)
            {
                Offset = offset;
                Value = value;
            }
        }
    }
}