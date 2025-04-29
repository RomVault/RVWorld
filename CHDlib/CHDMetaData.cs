using CHDSharpLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CHDSharpLib;

internal static class CHDMetaData
{
    internal static uint CHD_MDFLAGS_CHECKSUM = 0x01;        // indicates data is checksummed

    internal static chd_error ReadMetaData(Stream file, CHDHeader chd, Message consoleOut)
    {
        using BinaryReader br = new BinaryReader(file, Encoding.UTF8, true);

        // List<byte[]>metaHashes contains the byte data that is hashed below to validate the meta data
        // each metaHash is 24 bytes:
        // 0-3  : is the byte data for the metaTag
        // 4-23 : is the SHA1 of the metaData

        List<byte[]> metaHashes = new List<byte[]>();

        // loop over the metadata, until metaoffset=0
        while (chd.metaoffset != 0)
        {
            file.Seek((long)chd.metaoffset, SeekOrigin.Begin);
            uint metaTag = br.ReadUInt32BE();
            uint metaLength = br.ReadUInt32BE();
            ulong metaNext = br.ReadUInt64BE();
            uint metaFlags = metaLength >> 24;
            metaLength &= 0x00ffffff;

            byte[] metaData = new byte[metaLength];
            file.Read(metaData, 0, metaData.Length);

            if (consoleOut != null)
            {
                consoleOut?.Invoke($"{(char)((metaTag >> 24) & 0xFF)}{(char)((metaTag >> 16) & 0xFF)}{(char)((metaTag >> 8) & 0xFF)}{(char)((metaTag >> 0) & 0xFF)}  Length: {metaLength}");
                if (Util.isAscii(metaData))
                    consoleOut?.Invoke($"Data: {Encoding.ASCII.GetString(metaData)}");
                else
                    consoleOut?.Invoke($"Data: Binary Data Length {metaData.Length}");
            }

            // take the 4 byte metaTag, and the metaData
            // SHA1 the metaData to 20 byte SHA1
            // metadata_hash return these 24 bytes in a byte[24]
            if ((metaFlags & CHD_MDFLAGS_CHECKSUM) != 0)
                metaHashes.Add(metadata_hash(metaTag, metaData));

            // set location of next meta data entry in the CHD (set to 0 if finished.)
            chd.metaoffset = metaNext;
        }

        if (chd.sha1 == null)
            return chd_error.CHDERR_NONE;

        // binary sort the metaHashes
        metaHashes.Sort(Util.ByteArrCompare);

        // build the final SHA1
        // starting with the 20 byte rawsha1 from the main CHD data
        // then add the 24 byte for each meta data entry
        using SHA1 sha1Total = SHA1.Create();
        sha1Total.TransformBlock(chd.rawsha1, 0, chd.rawsha1.Length, null, 0);

        for (int i = 0; i < metaHashes.Count; i++)
            sha1Total.TransformBlock(metaHashes[i], 0, metaHashes[i].Length, null, 0);

        byte[] tmp = new byte[0];
        sha1Total.TransformFinalBlock(tmp, 0, 0);

        // compare the calculated metaData + rawData SHA1 with sha1 from the CHD header
        if (!Util.IsAllZeroArray(chd.sha1) && !Util.ByteArrEquals(chd.sha1, sha1Total.Hash))
            return chd_error.CHDERR_INVALID_METADATA;

        return chd_error.CHDERR_NONE;
    }
    private static byte[] metadata_hash(uint metaTag, byte[] metaData)
    {
        // make 24 byte metadata hash
        // 0-3  :  metaTag
        // 4-23 :  sha1 of the metaData

        byte[] metaHash = new byte[24];
        metaHash[0] = (byte)((metaTag >> 24) & 0xff);
        metaHash[1] = (byte)((metaTag >> 16) & 0xff);
        metaHash[2] = (byte)((metaTag >> 8) & 0xff);
        metaHash[3] = (byte)((metaTag >> 0) & 0xff);
        using SHA1 sha1 = SHA1.Create();
        byte[] metaDataHash = sha1.ComputeHash(metaData);

        for (int i = 0; i < 20; i++)
            metaHash[4 + i] = metaDataHash[i];

        return metaHash;
    }
}
