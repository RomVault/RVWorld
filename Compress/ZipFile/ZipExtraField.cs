using System;
using System.Diagnostics;
using System.Text;
using Compress.Utils;

namespace Compress.ZipFile
{
    internal partial class LocalFile
    {
        private static class ZipExtraField
        {
            public static ZipReturn ReadLocalExtraField(byte[] extraField, byte[] bFileName, LocalFile lf, bool centralDir)
            {
                int extraFieldLength = extraField.Length;

                lf.Zip64 = false;
                int blockPos = 0;
                while (extraFieldLength > blockPos)
                {
                    ushort type = BitConverter.ToUInt16(extraField, blockPos);
                    blockPos += 2;
                    ushort blockLength = BitConverter.ToUInt16(extraField, blockPos);
                    blockPos += 2;

                    int pos = blockPos;
                    switch (type)
                    {
                        /*
                                -Zip64 Extended Information Extra Field (0x0001):
                                ===============================================
    
                                The following is the layout of the zip64 extended
                                information "extra" block. If one of the size or
                                offset fields in the Local or Central directory
                                record is too small to hold the required data,
                                a zip64 extended information record is created.
                                The order of the fields in the zip64 extended
                                information record is fixed, but the fields will
                                only appear if the corresponding Local or Central
                                directory record field is set to 0xFFFF or 0xFFFFFFFF.
    
                                Note: all fields stored in Intel low-byte/high-byte order.
    
                                Value      Size       Description
                                -----      ----       -----------
                        (ZIP64) 0x0001     2 bytes    Tag for this "extra" block type
                                Size       2 bytes    Size of this "extra" block
                                Original
                                Size       8 bytes    Original uncompressed file size
                                Compressed
                                Size       8 bytes    Size of compressed data
                                Relative Header
                                Offset     8 bytes    Offset of local header record
                                Disk Start
                                Number     4 bytes    Number of the disk on which
                                                    this file starts
    
                                This entry in the Local header must include BOTH original
                                and compressed file size fields.  If encrypting the
                                central directory and bit 13 of the general purpose bit
                                flag is set indicating masking, the value stored in the
                                Local Header for the original file size will be zero.
                        */
                        case 0x0001:
                            lf.Zip64 = true;
                            if (centralDir)
                            {
                                if (lf.UncompressedSize == 0xffffffff)
                                {
                                    lf.UncompressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                if (lf._compressedSize == 0xffffffff)
                                {
                                    lf._compressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                if (lf.RelativeOffsetOfLocalHeader == 0xffffffff)
                                {
                                    lf.RelativeOffsetOfLocalHeader = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                            }
                            else
                            {
                                if (lf._localHeaderUncompressedSize == 0xffffffff)
                                {
                                    lf._localHeaderUncompressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                if (lf._localHeaderCompressedSize == 0xffffffff)
                                {
                                    lf._localHeaderCompressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                            }

                            break;


                        /* PKWARE's authenticity verification */
                        case 0x0007: // Not Needed
                            break;


                        /*
                                 -PKWARE Win95/WinNT Extra Field (0x000a):
                                  =======================================
    
                                  The following description covers PKWARE's "NTFS" attributes
                                  "extra" block, introduced with the release of PKZIP 2.50 for
                                  Windows. (Last Revision 20001118)
    
                                  (Note: At this time the Mtime, Atime and Ctime values may
                                  be used on any WIN32 system.)
                                 [Info-ZIP note: In the current implementations, this field has
                                  a fixed total data size of 32 bytes and is only stored as local
                                  extra field.]
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (NTFS)  0x000a        Short       Tag for this "extra" block type
                                  TSize         Short       Total Data Size for this block
                                  Reserved      Long        for future use
                                  Tag1          Short       NTFS attribute tag value #1
                                  Size1         Short       Size of attribute #1, in bytes
                                  (var.)        SubSize1    Attribute #1 data
                                  .
                                  .
                                  .
                                  TagN          Short       NTFS attribute tag value #N
                                  SizeN         Short       Size of attribute #N, in bytes
                                  (var.)        SubSizeN    Attribute #N data
    
                                  For NTFS, values for Tag1 through TagN are as follows:
                                  (currently only one set of attributes is defined for NTFS)
    
                                  Tag        Size       Description
                                  -----      ----       -----------
                                  0x0001     2 bytes    Tag for attribute #1
                                  Size1      2 bytes    Size of attribute #1, in bytes (24)
                                  Mtime      8 bytes    64-bit NTFS file last modification time
                                  Atime      8 bytes    64-bit NTFS file last access time
                                  Ctime      8 bytes    64-bit NTFS file creation time
    
                                  The total length for this block is 28 bytes, resulting in a
                                  fixed size value of 32 for the TSize field of the NTFS block.
    
                                  The NTFS filetimes are 64-bit unsigned integers, stored in Intel
                                  (least significant byte first) byte order. They determine the
                                  number of 1.0E-07 seconds (1/10th microseconds!) past WinNT "epoch",
                                  which is "01-Jan-1601 00:00:00 UTC".
                        */
                        case 0x000a:
                            pos += 4; // Reserved      Long        for future use
                            int tag1 = BitConverter.ToInt16(extraField, pos); // Tag1          Short       NTFS attribute tag value #1
                            pos += 2;
                            int size1 = BitConverter.ToInt16(extraField, pos); // Size1         Short       Size of attribute #1, in bytes
                            pos += 2;
                            if (tag1 == 0x0001 && size1 == 24
                            ) // (currently only one set of attributes is defined for NTFS)
                            {
                                lf.mTime = ZipUtils.FileTimeToUTCTime(BitConverter.ToInt64(extraField, pos)); // Mtime      8 bytes    64-bit NTFS file last modification time
                                pos += 8;
                                lf.aTime = ZipUtils.FileTimeToUTCTime(BitConverter.ToInt64(extraField, pos)); // Atime      8 bytes    64-bit NTFS file last access time
                                pos += 8;
                                lf.cTime = ZipUtils.FileTimeToUTCTime(BitConverter.ToInt64(extraField, pos)); // Ctime      8 bytes    64-bit NTFS file creation time
                                pos += 8;

                                Debug.WriteLine("modtime = " + new DateTime((long)lf.mTime));
                                Debug.WriteLine("Acctime = " + new DateTime((long)lf.aTime));
                                Debug.WriteLine("Cretime = " + new DateTime((long)lf.cTime));
                            }

                            break;


                        /*
                                  -Windows NT Security Descriptor Extra Field (0x4453):
                                  ===================================================
    
                                  The following is the layout of the NT Security Descriptor (another
                                  type of ACL) extra block.  (Last Revision 19960922)
    
                                  Local-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (SD)    0x4453        Short       tag for this extra block type ("SD")
                                  TSize         Short       total data size for this block
                                  BSize         Long        uncompressed SD data size
                                  Version       Byte        version of uncompressed SD data format
                                  CType         Short       compression type
                                  EACRC         Long        CRC value for uncompressed SD data
                                  (var.)        variable    compressed SD data
    
                                  Central-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (SD)    0x4453        Short       tag for this extra block type ("SD")
                                  TSize         Short       total data size for this block (4)
                                  BSize         Long        size of uncompressed local SD data
    
                                  The value of CType is interpreted according to the "compression
                                  method" section above; i.e., 0 for stored, 8 for deflated, etc.
                                  Version specifies how the compressed data are to be interpreted
                                  and allows for future expansion of this extra field type.  Currently
                                  only version 0 is defined.
    
                                  For version 0, the compressed data are to be interpreted as a single
                                  valid Windows NT SECURITY_DESCRIPTOR data structure, in self-relative
                                  format.
    
                        */
                        case 0x4453: // Not Needed
                            break;


                        /*
                                 -FWKCS MD5 Extra Field (0x4b46):
                                  ==============================
    
                                  The FWKCS Contents_Signature System, used in automatically
                                  identifying files independent of filename, optionally adds
                                  and uses an extra field to support the rapid creation of
                                  an enhanced contents_signature.
                                  There is no local-header version; the following applies
                                  only to the central header.  (Last Revision 19961207)
    
                                  Central-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (MD5)   0x4b46        Short       tag for this extra block type ("FK")
                                  TSize         Short       total data size for this block (19)
                                  "MD5"         3 bytes     extra-field signature
                                  MD5hash       16 bytes    128-bit MD5 hash of uncompressed data
                                                            (low byte first)
    
                                  When FWKCS revises a .ZIP file central directory to add
                                  this extra field for a file, it also replaces the
                                  central directory entry for that file's uncompressed
                                  file length with a measured value.
    
                                  FWKCS provides an option to strip this extra field, if
                                  present, from a .ZIP file central directory. In adding
                                  this extra field, FWKCS preserves .ZIP file Authenticity
                                  Verification; if stripping this extra field, FWKCS
                                  preserves all versions of AV through PKZIP version 2.04g.
    
                                  FWKCS, and FWKCS Contents_Signature System, are
                                  trademarks of Frederick W. Kantor.
    
                                  (1) R. Rivest, RFC1321.TXT, MIT Laboratory for Computer
                                      Science and RSA Data Security, Inc., April 1992.
                                      ll.76-77: "The MD5 algorithm is being placed in the
                                      public domain for review and possible adoption as a
                                      standard."
                        */
                        case 0x4B46: // Not Needed
                            break;


                        /*
                                 -Extended Timestamp Extra Field:
                                  ==============================
    
                                  The following is the layout of the extended-timestamp extra block.
                                  (Last Revision 19970118)
    
                                  Local-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (time)  0x5455        Short       tag for this extra block type ("UT")
                                  TSize         Short       total data size for this block
                                  Flags         Byte        info bits
                                  (ModTime)     Long        time of last modification (UTC/GMT)
                                  (AcTime)      Long        time of last access (UTC/GMT)
                                  (CrTime)      Long        time of original creation (UTC/GMT)
    
                                  Central-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (time)  0x5455        Short       tag for this extra block type ("UT")
                                  TSize         Short       total data size for this block
                                  Flags         Byte        info bits (refers to local header!)
                                  (ModTime)     Long        time of last modification (UTC/GMT)
    
                                  The central-header extra field contains the modification time only,
                                  or no timestamp at all.  TSize is used to flag its presence or
                                  absence.  But note:
    
                                      If "Flags" indicates that Modtime is present in the local header
                                      field, it MUST be present in the central header field, too!
                                      This correspondence is required because the modification time
                                      value may be used to support trans-timezone freshening and
                                      updating operations with zip archives.
    
                                  The time values are in standard Unix signed-long format, indicating
                                  the number of seconds since 1 January 1970 00:00:00.  The times
                                  are relative to Coordinated Universal Time (UTC), also sometimes
                                  referred to as Greenwich Mean Time (GMT).  To convert to local time,
                                  the software must know the local timezone offset from UTC/GMT.
    
                                  The lower three bits of Flags in both headers indicate which time-
                                  stamps are present in the LOCAL extra field:
    
                                        bit 0           if set, modification time is present
                                        bit 1           if set, access time is present
                                        bit 2           if set, creation time is present
                                        bits 3-7        reserved for additional timestamps; not set
    
                                  Those times that are present will appear in the order indicated, but
                                  any combination of times may be omitted.  (Creation time may be
                                  present without access time, for example.)  TSize should equal
                                  (1 + 4*(number of set bits in Flags)), as the block is currently
                                  defined.  Other timestamps may be added in the future.
                        */
                        case 0x5455:
                            byte flags = extraField[pos];
                            pos += 1;
                            if ((flags & 1) == 1)
                            {
                                lf.mTime = ZipUtils.SetDateTimeFromUnixSeconds(BitConverter.ToInt32(extraField, pos)); // (ModTime)     Long        time of last modification (UTC/GMT)
                                Debug.WriteLine("Umodtime = " + new DateTime((long)lf.mTime));
                                pos += 4;
                            }

                            if (!centralDir)
                            {
                                if ((flags & 2) == 2)
                                {
                                    lf.aTime = ZipUtils.SetDateTimeFromUnixSeconds(BitConverter.ToInt32(extraField, pos)); // (AcTime)      Long        time of last access (UTC/GMT)
                                    Debug.WriteLine("UAcctime = " + new DateTime((long)lf.aTime));
                                    pos += 4;
                                }

                                if ((flags & 4) == 4)
                                {
                                    lf.cTime = ZipUtils.SetDateTimeFromUnixSeconds(BitConverter.ToInt32(extraField, pos)); // (CrTime)      Long        time of original creation (UTC/GMT)
                                    Debug.WriteLine("UCretime = " + new DateTime((long)lf.cTime));
                                    pos += 4;
                                }
                            }

                            break;


                        /*
                                 -Info-ZIP Unix Extra Field (type 1):
                                  ==================================
    
                                  The following is the layout of the old Info-ZIP extra block for
                                  Unix.  It has been replaced by the extended-timestamp extra block
                                  (0x5455) and the Unix type 2 extra block (0x7855).
                                  (Last Revision 19970118)
    
                                  Local-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (Unix1) 0x5855        Short       tag for this extra block type ("UX")
                                  TSize         Short       total data size for this block
                                  AcTime        Long        time of last access (UTC/GMT)
                                  ModTime       Long        time of last modification (UTC/GMT)
                                  UID           Short       Unix user ID (optional)
                                  GID           Short       Unix group ID (optional)
    
                                  Central-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (Unix1) 0x5855        Short       tag for this extra block type ("UX")
                                  TSize         Short       total data size for this block
                                  AcTime        Long        time of last access (GMT/UTC)
                                  ModTime       Long        time of last modification (GMT/UTC)
    
                                  The file access and modification times are in standard Unix signed-
                                  long format, indicating the number of seconds since 1 January 1970
                                  00:00:00.  The times are relative to Coordinated Universal Time
                                  (UTC), also sometimes referred to as Greenwich Mean Time (GMT).  To
                                  convert to local time, the software must know the local timezone
                                  offset from UTC/GMT.  The modification time may be used by non-Unix
                                  systems to support inter-timezone freshening and updating of zip
                                  archives.
    
                                  The local-header extra block may optionally contain UID and GID
                                  info for the file.  The local-header TSize value is the only
                                  indication of this.  Note that Unix UIDs and GIDs are usually
                                  specific to a particular machine, and they generally require root
                                  access to restore.
    
                                  This extra field type is obsolete, but it has been in use since
                                  mid-1994.  Therefore future archiving software should continue to
                                  support it.  Some guidelines:
    
                                      An archive member should either contain the old "Unix1"
                                      extra field block or the new extra field types "time" and/or
                                      "Unix2".
    
                                      If both the old "Unix1" block type and one or both of the new
                                      block types "time" and "Unix2" are found, the "Unix1" block
                                      should be considered invalid and ignored.
    
                                      Unarchiving software should recognize both old and new extra
                                      field block types, but the info from new types overrides the
                                      old "Unix1" field.
    
                                      Archiving software should recognize "Unix1" extra fields for
                                      timestamp comparison but never create it for updated, freshened
                                      or new archive members.  When copying existing members to a new
                                      archive, any "Unix1" extra field blocks should be converted to
                                      the new "time" and/or "Unix2" types.
                        */
                        case 0x5855:
                            lf.aTime = ZipUtils.SetDateTimeFromUnixSeconds(BitConverter.ToInt32(extraField, pos)); // AcTime        Long        time of last access (UTC/GMT)
                            Debug.WriteLine("UAcctime = " + new DateTime((long)lf.aTime));
                            pos += 4;
                            lf.mTime = ZipUtils.SetDateTimeFromUnixSeconds(BitConverter.ToInt32(extraField, pos)); // ModTime       Long        time of last modification (UTC/GMT)
                            Debug.WriteLine("Umodtime = " + new DateTime((long)lf.mTime));
                            pos += 4;
                            break;


                        /*
                                 -Info-ZIP Unicode Path Extra Field:
                                  =================================
    
                                  Stores the UTF-8 version of the entry path as stored in the
                                  local header and central directory header.
                                  (Last Revision 20070912)
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (UPath) 0x7075        Short       tag for this extra block type ("up")
                                  TSize         Short       total data size for this block
                                  Version       Byte        version of this extra field, currently 1
                                  NameCRC32     Long        CRC-32 checksum of standard name field
                                  UnicodeName   variable    UTF-8 version of the entry file name
    
                                  Currently Version is set to the number 1.  If there is a need
                                  to change this field, the version will be incremented.  Changes
                                  may not be backward compatible so this extra field should not be
                                  used if the version is not recognized.
    
                                  The NameCRC32 is the standard zip CRC32 checksum of the File Name
                                  field in the header.  This is used to verify that the header
                                  File Name field has not changed since the Unicode Path extra field
                                  was created.  This can happen if a utility renames the entry but
                                  does not update the UTF-8 path extra field.  If the CRC check fails,
                                  this UTF-8 Path Extra Field should be ignored and the File Name field
                                  in the header should be used instead.
    
                                  The UnicodeName is the UTF-8 version of the contents of the File
                                  Name field in the header, without any trailing NUL.  The standard
                                  name field in the Zip entry header remains filled with the entry
                                  name coded in the local machine's extended ASCII system charset.
                                  As UnicodeName is defined to be UTF-8, no UTF-8 byte order mark
                                  (BOM) is used.  The length of this field is determined by
                                  subtracting the size of the previous fields from TSize.
                                  If both the File Name and Comment fields are UTF-8, the new General
                                  Purpose Bit Flag, bit 11 (Language encoding flag (EFS)), should be
                                  used to indicate that both the header File Name and Comment fields
                                  are UTF-8 and, in this case, the Unicode Path and Unicode Comment
                                  extra fields are not needed and should not be created.  Note that,
                                  for backward compatibility, bit 11 should only be used if the native
                                  character set of the paths and comments being zipped up are already
                                  in UTF-8.  The same method, either general purpose bit 11 or extra
                                  fields, should be used in both the Local and Central Directory Header
                                  for a file.
    
                                  Utilisation rules:
                                  1. This field shall never be created for names consisting solely of
                                     7-bit ASCII characters.
                                  2. On a system that already uses UTF-8 as system charset, this field
                                     shall not repeat the string pattern already stored in the Zip
                                     entry's standard name field. Instead, a field of exactly 9 bytes
                                     (70 75 05 00 01 and 4 bytes CRC) should be created.
                                     In this form with 5 data bytes, the field serves as indicator
                                     for the UTF-8 encoding of the standard Zip header's name field.
                                  3. This field shall not be used whenever the calculated CRC-32 of
                                     the entry's standard name field does not match the provided
                                     CRC checksum value.  A mismatch of the CRC check indicates that
                                     the standard name field was changed by some non-"up"-aware
                                     utility without synchronizing this UTF-8 name e.f. block.
                        */
                        case 0x7075:
                            pos += 1;
                            uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
                            pos += 4;

                            CRC crcTest = new CRC();
                            crcTest.SlurpBlock(bFileName, 0, bFileName.Length);
                            uint fCRC = crcTest.Crc32ResultU;

                            if (nameCRC32 == fCRC)
                            {
                                int charLen = blockLength - 5;
                                if (centralDir)
                                    lf.FileName = Encoding.UTF8.GetString(extraField, pos, charLen);
                                else
                                    lf._localHeaderFilename = Encoding.UTF8.GetString(extraField, pos, charLen);
                            }

                            break;


                        /*
                                 -Info-ZIP UNIX Extra Field (type 2):
                                  ==================================
    
                                  The following is the layout of the new Info-ZIP extra block for
                                  Unix.  (Last Revision 19960922)
    
                                  Local-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (Unix2) 0x7855        Short       tag for this extra block type ("Ux")
                                  TSize         Short       total data size for this block (4)
                                  UID           Short       Unix user ID
                                  GID           Short       Unix group ID
    
                                  Central-header version:
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (Unix2) 0x7855        Short       tag for this extra block type ("Ux")
                                  TSize         Short       total data size for this block (0)
    
                                  The data size of the central-header version is zero; it is used
                                  solely as a flag that UID/GID info is present in the local-header
                                  extra field.  If additional fields are ever added to the local
                                  version, the central version may be extended to indicate this.
    
                                  Note that Unix UIDs and GIDs are usually specific to a particular
                                  machine, and they generally require root access to restore.
    
                        */
                        case 0x7855: // Not Needed
                            break;


                        /*
                                 -Info-ZIP New Unix Extra Field:
                                  ====================================
    
                                  Currently stores Unix UIDs/GIDs up to 32 bits.
                                  (Last Revision 20080509)
    
                                  Value         Size        Description
                                  -----         ----        -----------
                          (UnixN) 0x7875        Short       tag for this extra block type ("ux")
                                  TSize         Short       total data size for this block
                                  Version       1 byte      version of this extra field, currently 1
                                  UIDSize       1 byte      Size of UID field
                                  UID           Variable    UID for this entry
                                  GIDSize       1 byte      Size of GID field
                                  GID           Variable    GID for this entry
    
                                  Currently Version is set to the number 1.  If there is a need
                                  to change this field, the version will be incremented.  Changes
                                  may not be backward compatible so this extra field should not be
                                  used if the version is not recognized.
    
                                  UIDSize is the size of the UID field in bytes.  This size should
                                  match the size of the UID field on the target OS.
    
                                  UID is the UID for this entry in standard little endian format.
    
                                  GIDSize is the size of the GID field in bytes.  This size should
                                  match the size of the GID field on the target OS.
    
                                  GID is the GID for this entry in standard little endian format.
    
                                  If both the old 16-bit Unix extra field (tag 0x7855, Info-ZIP Unix2)
                                  and this extra field are present, the values in this extra field
                                  supercede the values in that extra field.
                        */
                        case 0x7875: // Not Needed
                            break;



                        /*      UNKNOWN    */
                        case 0xe57a:
                            break;


                        default:
                            break;
                    }

                    blockPos += blockLength;

                }

                return ZipReturn.ZipGood;
            }
        }
    }
}