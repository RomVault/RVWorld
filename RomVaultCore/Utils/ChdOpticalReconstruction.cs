using CHDSharpLib;
using System;
using System.IO;
using System.Security.Cryptography;

namespace RomVaultCore.Utils;

internal static class ChdOpticalReconstruction
{
    /// <summary>
    /// Recreates a single-file cdrdao TOC payload from canonical 2448-byte CHD
    /// frames.  This preserves interleaved RW/RW_RAW subchannel bytes that CUE
    /// output cannot describe.  The integrity-checked manifest SHA-256 is the
    /// acceptance boundary.
    /// </summary>
    public static bool TryMaterializeSingleTocPayload(string chdPath, ChdReconstructionManifest manifest, string outputPath, out string error)
    {
        error = "";
        if (manifest == null || !string.Equals(manifest.Dialect, "toc-exact", StringComparison.OrdinalIgnoreCase))
        {
            error = "The CHD does not contain an integrity-checked TOC payload representation.";
            return false;
        }
        if (manifest.Tracks == null || manifest.Tracks.Count != 1)
        {
            error = "Exact native TOC reconstruction currently requires one source payload file.";
            return false;
        }
        ChdManifestTrack track = manifest.Tracks[0];
        if (track == null || track.Size < 0 || track.Sha256 == null || track.Sha256.Length != 32)
        {
            error = "The TOC payload identity is incomplete.";
            return false;
        }
        try
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            using (Stream logical = ChdLogicalStream.OpenRead(chdPath))
            using (FileStream output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (SHA256 sha256 = SHA256.Create())
            {
                if (logical.Length < track.Size)
                    throw new InvalidDataException("CHD logical stream is shorter than the integrity-checked TOC payload.");
                byte[] buffer = new byte[1024 * 1024];
                long remaining = track.Size;
                while (remaining > 0)
                {
                    int wanted = (int)Math.Min(buffer.Length, remaining);
                    int total = 0;
                    while (total < wanted)
                    {
                        int read = logical.Read(buffer, total, wanted - total);
                        if (read == 0) throw new EndOfStreamException();
                        total += read;
                    }
                    output.Write(buffer, 0, wanted);
                    sha256.TransformBlock(buffer, 0, wanted, null, 0);
                    remaining -= wanted;
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                output.Flush(true);
                if (!FixedTimeEquals(track.Sha256, sha256.Hash))
                    throw new InvalidDataException("Native TOC reconstruction does not match the embedded SHA-256 identity.");
            }
            return true;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            error = ex.Message;
            return false;
        }
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
        return difference == 0;
    }
}
