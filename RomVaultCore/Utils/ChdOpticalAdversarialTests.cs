using System;
using System.IO;
using System.Linq;
using System.Text;

namespace RomVaultCore.Utils;

internal static class ChdOpticalAdversarialTests
{
    public static bool Run(ChdmanIdentity identity, out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "RomVault-optical-hardening-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string track = Path.Combine(root, "träck-日本.bin");
            byte[] trackBytes = new byte[2352 * 8];
            for (int i = 0; i < trackBytes.Length; i++) trackBytes[i] = unchecked((byte)(i * 17 + 3));
            File.WriteAllBytes(track, trackBytes);
            string cue = Path.Combine(root, "dïsc-日本.cue");
            File.WriteAllText(cue,
                "FILE \"träck-日本.bin\" BINARY\r\n" +
                "  TRACK 01 MODE1/2352\r\n" +
                "    INDEX 01 00:00:00\r\n",
                new UTF8Encoding(false));
            byte[] sbi = new byte[] { 0x53, 0x42, 0x49, 0x00, 0x00, 0x02, 0x00, 0x01, 0x7f };
            File.WriteAllBytes(Path.ChangeExtension(cue, ".sbi"), sbi);
            ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", ChdStorageProfile.Playback);
            if (!ChdReconstructionManifest.TryCreateFromSource(cue, profile, identity, out ChdReconstructionManifest manifest, out error))
                return false;
            if (!string.Equals(manifest.Dialect, "cue-mode1-2352", StringComparison.Ordinal) || manifest.Auxiliaries.Count != 1 ||
                !manifest.Auxiliaries[0].Bytes.SequenceEqual(sbi) || manifest.Auxiliaries[0].Sha256?.Length != 32)
            {
                error = "Unicode filenames affected the media dialect or the SBI content identity was not preserved.";
                return false;
            }
            if (!ChdReconstructionManifest.TryDeserialize(manifest.Serialize(), out ChdReconstructionManifest roundTrip, out error) ||
                roundTrip.Auxiliaries.Count != 1 || !roundTrip.Auxiliaries[0].Bytes.SequenceEqual(sbi))
                return false;

            string traversal = Path.Combine(root, "traversal.cue");
            File.WriteAllText(traversal,
                "FILE \"..\\escape.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n",
                new UTF8Encoding(false));
            if (ChdReconstructionManifest.TryCreateFromSource(traversal, profile, identity, out _, out _))
            {
                error = "A descriptor path traversal was accepted.";
                return false;
            }

            string malformed = Path.Combine(root, "malformed.cue");
            File.WriteAllText(malformed, "FILE \"unterminated.bin BINARY\r\nTRACK 01 MODE1/2352\r\n", new UTF8Encoding(false));
            if (ChdReconstructionManifest.TryCreateFromSource(malformed, profile, identity, out _, out _))
            {
                error = "A malformed descriptor with no readable payload was accepted.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }
}
