using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace RomVaultCore.Utils;

internal static class ChdConversionSelfTests
{
    public static bool Run(string executable, ChdmanIdentity identity, out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "RomVault-conversion-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source.raw");
            byte[] bytes = new byte[1024 * 1024 + 17];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)(i * 73 + 11));
            File.WriteAllBytes(source, bytes);
            ChdEncodingProfileSpec archive = ChdEncodingProfile.ForFamily("raw", ChdStorageProfile.Archive);
            if (!ChdReconstructionManifest.TryCreateFromSource(source, archive, identity, out ChdReconstructionManifest manifest, out error))
                return false;
            string stage = Path.Combine(root, "stage.chd");
            string output = Path.Combine(root, "image.chd");
            string metadata = Path.Combine(root, "manifest.bin");
            File.WriteAllBytes(metadata, manifest.Serialize());
            ChdmanRunResult step = ChdmanService.Run(executable, $"createraw -i {ChdmanService.Quote(source)} -o {ChdmanService.Quote(stage)} -c none -hs {archive.HunkSize} -us 1 -f", root, 180000);
            if (step.Success) step = ChdmanService.Run(executable, $"addmeta -i {ChdmanService.Quote(stage)} -t {ChdEncodingProfile.MetadataTag} -ix 0 -vt {ChdmanService.Quote(archive.ToMetadata(identity))} -nocs", root, 30000);
            if (step.Success) step = ChdmanService.Run(executable, $"addmeta -i {ChdmanService.Quote(stage)} -t {ChdReconstructionManifest.MetadataTag} -ix 0 -vf {ChdmanService.Quote(metadata)} -nocs", root, 30000);
            if (step.Success) step = ChdmanService.Run(executable, $"copy -i {ChdmanService.Quote(stage)} -o {ChdmanService.Quote(output)} -c {archive.Codecs} -hs {archive.HunkSize} -f", root, 180000);
            if (!step.Success) { error = step.Output; return false; }
            int code = ChdStandaloneConverter.Convert(output, ChdStorageProfile.Playback, executable, out bool changed, out string report);
            if (code != 0 || !changed) { error = report; return false; }
            string extracted = Path.Combine(root, "extracted.raw");
            step = ChdmanService.Run(executable, $"extractraw -i {ChdmanService.Quote(output)} -o {ChdmanService.Quote(extracted)} -f", root, 180000);
            if (!step.Success || !Hash(source).SequenceEqual(Hash(extracted)) || !ChdEncodingProfile.TryRead(output, out ChdEncodingProfile converted) ||
                converted.Storage != "playback" || converted.Codecs != "zstd")
            {
                error = step.Success ? "Playback queue conversion changed logical bytes or wrote the wrong profile." : step.Output;
                return false;
            }
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    private static byte[] Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream);
    }
}
