using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RomVaultCore.Scanner;

namespace RomVaultCore.Utils;

/// <summary>
/// Migrates older reconstruction manifests to the current authenticated schema by obtaining SHA-256
/// from an independent, canonical chdman extraction.  Existing SHA-256 values
/// are never invented from weaker DAT hashes.
/// </summary>
internal static class ChdManifestPayloadHasher
{
    public static bool EnsureSha256(string chdPath, string chdmanPath, string workingRoot, ChdReconstructionManifest manifest, out string error)
    {
        error = "";
        if (manifest == null)
        {
            error = "Reconstruction manifest is missing.";
            return false;
        }
        if (AllPresent(manifest))
        {
            manifest.Schema = ChdReconstructionManifest.CurrentSchema;
            return true;
        }

        string root = Path.Combine(ResolveDirectory(workingRoot), "rv-chd-sha256-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            IChdExtractor extractor = new ChdmanChdExtractor(chdmanPath, root);
            Dictionary<ChdManifestTrack, string> extracted = new Dictionary<ChdManifestTrack, string>();
            string family = (manifest.Family ?? "").Trim().ToLowerInvariant();
            if (family == "cd" || family == "gdi")
            {
                string extension = family == "gdi" && !string.Equals(manifest.Dialect, "redump-gdrom-cue", StringComparison.OrdinalIgnoreCase) ? ".gdi" : ".cue";
                string descriptor = Path.Combine(root, "disc" + extension);
                if (!extractor.ExtractCd(chdPath, descriptor, out error))
                    return false;
                List<string> payloads = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !string.Equals(path, descriptor, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (!MapTracks(manifest.Tracks, payloads, extracted, out error))
                    return false;
            }
            else
            {
                string output = Path.Combine(root, family == "dvd" || family == "psp" ? "disc.iso" : family == "hdd" ? "disk.img" : family == "laserdisc" ? "disc.avi" : "image.raw");
                bool ok;
                if (family == "dvd" || family == "psp") ok = extractor.ExtractDvd(chdPath, output, out error);
                else if (family == "hdd") ok = extractor.ExtractHardDisk(chdPath, output, out error);
                else if (family == "laserdisc") ok = extractor.ExtractLaserDisc(chdPath, output, out error);
                else ok = extractor.ExtractRaw(chdPath, output, out error);
                if (!ok)
                    return false;
                if (!MapTracks(manifest.Tracks, new List<string> { output }, extracted, out error))
                    return false;
            }

            foreach (KeyValuePair<ChdManifestTrack, string> item in extracted)
                item.Key.Sha256 = HashFile(item.Value).Sha256;

            for (int i = 0; i < (manifest.Views?.Count ?? 0); i++)
            {
                ChdManifestView view = manifest.Views[i];
                if (view?.Tracks == null || view.Tracks.All(track => track?.Sha256?.Length == 32))
                    continue;
                if (!string.Equals(view.Name, "iso", StringComparison.OrdinalIgnoreCase) || manifest.Tracks.Count != 1 ||
                    !extracted.TryGetValue(manifest.Tracks[0], out string primary))
                {
                    error = "An older alternate reconstruction view cannot be upgraded to SHA-256 safely.";
                    return false;
                }
                string iso = Path.Combine(root, "view.iso");
                if (!ChdMultiView.TryMaterializeIsoView(primary, view, iso, out error))
                    return false;
                if (view.Tracks.Count != 1)
                {
                    error = "The ISO reconstruction view is ambiguous.";
                    return false;
                }
                view.Tracks[0].Sha256 = HashFile(iso).Sha256;
            }
            manifest.Schema = ChdReconstructionManifest.CurrentSchema;
            if (!AllPresent(manifest))
            {
                error = "Canonical extraction did not produce SHA-256 for every manifest payload.";
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

    private static bool MapTracks(List<ChdManifestTrack> expected, List<string> candidates, Dictionary<ChdManifestTrack, string> result, out string error)
    {
        error = "";
        expected = expected ?? new List<ChdManifestTrack>();
        List<FileHashes> hashes = candidates.Where(File.Exists).Select(HashFile).ToList();
        HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < expected.Count; i++)
        {
            ChdManifestTrack track = expected[i];
            List<FileHashes> matches = hashes.Where(item => !used.Contains(item.Path) && Matches(track, item)).ToList();
            if (matches.Count != 1)
            {
                error = "Canonical extraction produced " + matches.Count + " exact matches for " + (track?.Name ?? "<unnamed>") + ".";
                return false;
            }
            used.Add(matches[0].Path);
            result[track] = matches[0].Path;
        }
        return true;
    }

    private static bool Matches(ChdManifestTrack expected, FileHashes actual)
    {
        return expected != null && expected.Size == actual.Size && OptionalEqual(expected.Crc32, actual.Crc32) &&
               OptionalEqual(expected.Sha1, actual.Sha1) && OptionalEqual(expected.Md5, actual.Md5) &&
               OptionalEqual(expected.Sha256, actual.Sha256);
    }

    private static bool OptionalEqual(byte[] expected, byte[] actual)
    {
        if (expected == null || expected.Length == 0) return true;
        if (actual == null || actual.Length != expected.Length) return false;
        int difference = 0;
        for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
        return difference == 0;
    }

    private static bool AllPresent(ChdReconstructionManifest manifest)
    {
        return manifest.Tracks != null && manifest.Tracks.Count > 0 && manifest.Tracks.All(track => track?.Sha256?.Length == 32) &&
               (manifest.Views ?? new List<ChdManifestView>()).All(view => view?.Tracks != null && view.Tracks.All(track => track?.Sha256?.Length == 32));
    }

    private static FileHashes HashFile(string path)
    {
        uint crc = 0xffffffff;
        using (SHA1 sha1 = SHA1.Create())
        using (MD5 md5 = MD5.Create())
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha1.TransformBlock(buffer, 0, read, null, 0);
                md5.TransformBlock(buffer, 0, read, null, 0);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                for (int i = 0; i < read; i++) crc = UpdateCrc32(crc, buffer[i]);
            }
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            crc ^= 0xffffffff;
            return new FileHashes
            {
                Path = path,
                Size = stream.Length,
                Crc32 = new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc },
                Sha1 = sha1.Hash,
                Md5 = md5.Hash,
                Sha256 = sha256.Hash
            };
        }
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        return crc;
    }

    private static string ResolveDirectory(string value)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                return Path.GetFullPath(value);
        }
        catch { }
        return Path.GetTempPath();
    }

    private sealed class FileHashes
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public byte[] Crc32 { get; set; }
        public byte[] Sha1 { get; set; }
        public byte[] Md5 { get; set; }
        public byte[] Sha256 { get; set; }
    }
}
