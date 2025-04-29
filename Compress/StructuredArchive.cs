using Compress.StructuredZip;

namespace Compress
{
    public enum ZipStructure
    {
        None = 0,    // No structure
        ZipTrrnt = 1, // Original Trrntzip
        ZipTDC = 2,   // Total DOS Collection, Date Time Deflate
        SevenZipTrrnt = 4, // this is the original t7z format
        ZipZSTD = 5,       // ZSTD Compression
        SevenZipSLZMA = 8, // Solid-LZMA this is rv7zip today
        SevenZipNLZMA = 9, // NonSolid-LZMA
        SevenZipSZSTD = 10, // Solid-zSTD
        SevenZipNZSTD = 11, // NonSolid-zSTD
    }

    public enum zipDateType
    {
        Undefined,
        None,
        TrrntZip,
        DateTime
    }

    public static class StructuredArchive
    {

        public static ushort GetCompressionType(ZipStructure zipStruct)
        {
            switch (zipStruct)
            {
                case ZipStructure.None:
                    return 0;

                case ZipStructure.ZipTrrnt:
                case ZipStructure.ZipTDC:
                    return 8;
                case ZipStructure.SevenZipTrrnt:
                    return ushort.MaxValue;
                case ZipStructure.ZipZSTD:
                    return 93;
                case ZipStructure.SevenZipSLZMA:
                case ZipStructure.SevenZipNLZMA:
                    return 14;
                case ZipStructure.SevenZipSZSTD:
                case ZipStructure.SevenZipNZSTD:
                    return 93;
            }
            return ushort.MaxValue;
        }

        public static string GetZipCommentId(ZipStructure zipStruct)
        {
            switch (zipStruct)
            {
                case ZipStructure.ZipTrrnt:
                    return "TORRENTZIPPED-";
                case ZipStructure.ZipTDC:
                    return "TDC-";
                case ZipStructure.ZipZSTD:
                    return "RVZSTD-";
                default:
                    return "";
            }
        }


        public static zipDateType GetZipDateTimeType(ZipStructure zipStruct)
        {
            switch (zipStruct)
            {
                case ZipStructure.ZipTrrnt:
                    return zipDateType.TrrntZip;

                case ZipStructure.ZipTDC:
                    return zipDateType.DateTime;

                case ZipStructure.ZipZSTD:
                    return zipDateType.None;

            }
            return zipDateType.Undefined;
        }
    }


}
