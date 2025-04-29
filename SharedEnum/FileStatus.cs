using System;

[Flags]
public enum FileStatus
{
    HeaderFileTypeFromDAT = 1 << 0,
    SizeFromDAT = 1 << 1,
    CRCFromDAT = 1 << 2,
    SHA1FromDAT = 1 << 3,
    MD5FromDAT = 1 << 4,
    AltSizeFromDAT = 1 << 5,
    AltCRCFromDAT = 1 << 6,
    AltSHA1FromDAT = 1 << 7,
    AltMD5FromDAT = 1 << 8,

    HeaderFileTypeFromHeader = 1 << 9,
    SizeFromHeader = 1 << 10,
    CRCFromHeader = 1 << 11,
    SHA1FromHeader = 1 << 12,
    MD5FromHeader = 1 << 13,
    AltSizeFromHeader = 1 << 14,
    AltCRCFromHeader = 1 << 15,
    AltSHA1FromHeader = 1 << 16,
    AltMD5FromHeader = 1 << 17,

    SizeVerified = 1 << 18,
    CRCVerified = 1 << 19,
    SHA1Verified = 1 << 20,
    MD5Verified = 1 << 21,
    AltSizeVerified = 1 << 22,
    AltCRCVerified = 1 << 23,
    AltSHA1Verified = 1 << 24,
    AltMD5Verified = 1 << 25,

    SHA256Verified = 1 << 26,
    AltSHA256Verified = 1 << 27,

    DateFromDAT = 1 << 28,
    DeepScanned = 1 << 29,

   // ToSortPrimary = 1 << 30,
   // ToSortCache = 1 << 31,


    DatFlags = HeaderFileTypeFromDAT | SizeFromDAT | CRCFromDAT | SHA1FromDAT | MD5FromDAT | AltSizeFromDAT | AltCRCFromDAT | AltSHA1FromDAT | AltMD5FromDAT | DateFromDAT,
    DatAltFlags = AltSizeFromDAT | AltCRCFromDAT | AltSHA1FromDAT | AltMD5FromDAT,
    HeaderFlags = HeaderFileTypeFromHeader | SizeFromHeader | CRCFromHeader | SHA1FromHeader | MD5FromHeader | AltSizeFromHeader | AltCRCFromHeader | AltSHA1FromHeader | AltMD5FromHeader,
    VerifiedFlags = SizeVerified | CRCVerified | SHA1Verified | MD5Verified | AltSizeVerified | AltCRCVerified | AltSHA1Verified | AltMD5Verified | SHA256Verified | AltSHA256Verified
}
