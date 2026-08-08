using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CHDSharpLib;

namespace RomVaultCore.Utils;

internal static class ChdHardeningSelfTests
{
    public static bool Run(out string error)
    {
        error = "";
        try
        {
            Random random = new Random(0x524d4348);
            for (int i = 0; i < 512; i++)
            {
                byte[] input = new byte[random.Next(0, 8192)];
                random.NextBytes(input);
                ChdReconstructionManifest.TryDeserialize(input, out _, out _);
                using (MemoryStream stream = new MemoryStream(input, false))
                    CHD.CheckHeader(stream, out _, out _);
            }

            AssertInvalidHeader(0);
            AssertInvalidHeader(6);
            AssertInvalidHeader(uint.MaxValue);
            using (MemoryStream shortHeader = new MemoryStream(Encoding.ASCII.GetBytes("MComprHD"), false))
            {
                if (CHD.CheckHeader(shortHeader, out _, out _))
                    throw new InvalidDataException("A truncated CHD header was accepted.");
            }

            ChdReconstructionManifest unsafeManifest = MinimalManifest();
            unsafeManifest.Tracks[0].Name = "..\\outside.bin";
            AssertSerializeRejected(unsafeManifest, "path traversal");

            ChdReconstructionManifest duplicateManifest = MinimalManifest();
            duplicateManifest.Tracks.Add(new ChdManifestTrack { Number = 2, Name = "TRACK01.BIN", Size = 1, Sha256 = new byte[32] });
            AssertSerializeRejected(duplicateManifest, "duplicate track");

            ChdReconstructionManifest badHashManifest = MinimalManifest();
            badHashManifest.Tracks[0].Sha1 = new byte[19];
            AssertSerializeRejected(badHashManifest, "invalid hash length");

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ChdReconstructionManifest MinimalManifest()
    {
        return new ChdReconstructionManifest
        {
            Family = "cd",
            Storage = "archive",
            Dialect = "cue-exact",
            Tracks = new List<ChdManifestTrack>
            {
                new ChdManifestTrack { Number = 1, Name = "track01.bin", Size = 1, Sha256 = new byte[32] }
            }
        };
    }

    private static void AssertSerializeRejected(ChdReconstructionManifest manifest, string label)
    {
        try
        {
            manifest.Serialize();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidDataException("Manifest hardening did not reject " + label + ".");
    }

    private static void AssertInvalidHeader(uint version)
    {
        byte[] header = new byte[16];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("MComprHD"), 0, header, 0, 8);
        WriteBigEndian(header, 8, 124);
        WriteBigEndian(header, 12, version);
        using (MemoryStream stream = new MemoryStream(header, false))
        {
            if (CHD.CheckHeader(stream, out _, out _))
                throw new InvalidDataException("Unsupported CHD header version was accepted: " + version + ".");
        }
    }

    private static void WriteBigEndian(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
