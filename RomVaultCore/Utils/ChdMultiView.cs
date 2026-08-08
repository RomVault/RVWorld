using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RomVaultCore.RvDB;

namespace RomVaultCore.Utils;

internal static class ChdMultiView
{
    public static bool RunSelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-multiview-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            byte[] raw = new byte[2352 * 3];
            byte[] iso = new byte[2048 * 3];
            for (int sector = 0; sector < 3; sector++)
            {
                for (int i = 0; i < 2352; i++) raw[sector * 2352 + i] = (byte)(sector * 31 + i);
                Buffer.BlockCopy(raw, sector * 2352 + 16, iso, sector * 2048, 2048);
            }
            string rawPath = Path.Combine(root, "track01.bin");
            string isoPath = Path.Combine(root, "disc.iso");
            File.WriteAllBytes(rawPath, raw);
            uint crc = 0xffffffff;
            for (int i = 0; i < iso.Length; i++) crc = UpdateCrc32(crc, iso[i]);
            crc ^= 0xffffffff;
            ChdManifestView view = new ChdManifestView
            {
                Name = "iso",
                Dialect = "iso-from-mode1-2352",
                Tracks = new List<ChdManifestTrack>
                {
                    new ChdManifestTrack
                    {
                        Number = 1,
                        Name = "disc.iso",
                        Size = iso.Length,
                        Crc32 = new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc },
                        Sha1 = Hash(iso, SHA1.Create()),
                        Md5 = Hash(iso, MD5.Create()),
                        Sha256 = Hash(iso, SHA256.Create())
                    }
                }
            };
            if (!TryMaterializeIsoView(rawPath, view, isoPath, out error) || !File.ReadAllBytes(isoPath).SequenceEqual(iso))
                return false;
            File.WriteAllBytes(rawPath, new byte[17]);
            if (TryMaterializeIsoView(rawPath, view, isoPath, out _))
                throw new InvalidDataException("A non-sector-aligned ISO view was accepted.");
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
    public static bool TryAttachEquivalentIsoView(ChdReconstructionManifest manifest, string descriptorPath, RvFile destination, out bool attempted, out string error)
    {
        attempted = false;
        error = "";
        try
        {
            if (manifest == null || destination == null || !string.Equals(Path.GetExtension(descriptorPath), ".cue", StringComparison.OrdinalIgnoreCase))
                return true;
            string stem = Path.GetFileNameWithoutExtension(descriptorPath) ?? "";
            RvFile iso = null;
            for (int i = 0; i < destination.ChildCount; i++)
            {
                RvFile child = destination.Child(i);
                if (child?.IsFile != true || !string.Equals(Path.GetExtension(child.Name), ".iso", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetFileNameWithoutExtension(child.Name), stem, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (iso != null)
                {
                    attempted = true;
                    error = "More than one same-stem ISO view is ambiguous.";
                    return false;
                }
                iso = child;
            }
            if (iso == null)
                return true;
            attempted = true;

            if (manifest.Tracks == null || manifest.Tracks.Count != 1)
            {
                error = "An ISO view is only reversible for a proven single-track data CUE.";
                return false;
            }
            string cue = Encoding.UTF8.GetString(manifest.DescriptorBytes ?? Array.Empty<byte>());
            MatchCollection trackLines = Regex.Matches(cue, @"^\s*TRACK\s+\d+\s+(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (trackLines.Count != 1)
            {
                error = "The same-stem ISO cannot be proven equivalent to a multi-track CUE.";
                return false;
            }
            string type = trackLines[0].Groups[1].Value.ToUpperInvariant();
            int sectorSize = type == "MODE1/2048" ? 2048 : type == "MODE1/2352" ? 2352 : 0;
            if (sectorSize == 0)
            {
                error = "Only MODE1/2048 and MODE1/2352 have an unambiguous ISO view.";
                return false;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(descriptorPath)) ?? Environment.CurrentDirectory;
            string trackPath = ResolveWithin(directory, manifest.Tracks[0].Name);
            if (!File.Exists(trackPath))
            {
                error = "The primary CUE track required to prove the ISO view is missing.";
                return false;
            }
            ChdManifestTrack derived = HashIsoView(trackPath, sectorSize, iso.Name);
            if (!MatchesDat(derived, iso, out error))
                return false;

            manifest.Schema = ChdReconstructionManifest.CurrentSchema;
            manifest.Views.RemoveAll(view => string.Equals(view?.Name, "iso", StringComparison.OrdinalIgnoreCase));
            manifest.Views.Add(new ChdManifestView
            {
                Name = "iso",
                Dialect = sectorSize == 2048 ? "iso-from-mode1-2048" : "iso-from-mode1-2352",
                Tracks = new List<ChdManifestTrack> { derived }
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryMaterializeIsoView(string primaryTrackPath, ChdManifestView view, string outputPath, out string error)
    {
        error = "";
        try
        {
            if (view?.Tracks == null || view.Tracks.Count != 1)
                throw new InvalidDataException("The ISO reconstruction view is invalid.");
            int sectorSize = string.Equals(view.Dialect, "iso-from-mode1-2048", StringComparison.OrdinalIgnoreCase) ? 2048
                : string.Equals(view.Dialect, "iso-from-mode1-2352", StringComparison.OrdinalIgnoreCase) ? 2352 : 0;
            if (sectorSize == 0)
                throw new InvalidDataException("Unsupported ISO reconstruction dialect: " + view.Dialect);
            using (FileStream input = File.OpenRead(primaryTrackPath))
            using (FileStream output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                CopyIsoPayload(input, output, sectorSize);
            ChdManifestTrack actual = HashIsoView(outputPath, 2048, view.Tracks[0].Name);
            if (!MatchesTrack(actual, view.Tracks[0]))
                throw new InvalidDataException("Materialized ISO view does not match its embedded DAT hashes.");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return false;
        }
    }

    public static ChdManifestView GetIsoView(ChdReconstructionManifest manifest)
    {
        return manifest?.Views?.FirstOrDefault(view => string.Equals(view?.Name, "iso", StringComparison.OrdinalIgnoreCase));
    }

    private static ChdManifestTrack HashIsoView(string path, int sectorSize, string outputName)
    {
        uint crc = 0xffffffff;
        using (SHA1 sha1 = SHA1.Create())
        using (MD5 md5 = MD5.Create())
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream input = File.OpenRead(path))
        using (HashingWriteStream sink = new HashingWriteStream(sha1, md5, sha256, value => crc = UpdateCrc32(crc, value)))
        {
            CopyIsoPayload(input, sink, sectorSize);
            sink.Complete();
            crc ^= 0xffffffff;
            return new ChdManifestTrack
            {
                Number = 1,
                Name = outputName,
                Size = sink.Length,
                Crc32 = new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc },
                Sha1 = sha1.Hash,
                Md5 = md5.Hash,
                Sha256 = sha256.Hash
            };
        }
    }

    private static void CopyIsoPayload(Stream input, Stream output, int sectorSize)
    {
        if (sectorSize != 2048 && sectorSize != 2352)
            throw new InvalidDataException("Invalid MODE1 sector size.");
        if (input.Length % sectorSize != 0)
            throw new InvalidDataException("MODE1 track length is not sector aligned.");
        byte[] sector = new byte[sectorSize];
        long sectors = input.Length / sectorSize;
        for (long i = 0; i < sectors; i++)
        {
            ReadExactly(input, sector, 0, sector.Length);
            output.Write(sector, sectorSize == 2352 ? 16 : 0, 2048);
        }
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0) throw new EndOfStreamException();
            offset += read;
            count -= read;
        }
    }

    private static bool MatchesDat(ChdManifestTrack actual, RvFile expected, out string error)
    {
        error = "";
        long size = expected.Size.HasValue && expected.Size.Value <= long.MaxValue ? (long)expected.Size.Value : -1;
        bool hasHash = (expected.SHA1?.Length ?? 0) > 0 || (expected.MD5?.Length ?? 0) > 0 || (expected.CRC?.Length ?? 0) > 0;
        if (!hasHash || size < 0)
        {
            error = "The ISO DAT entry needs a size and at least one hash before equivalence can be proven.";
            return false;
        }
        if (actual.Size != size || !OptionalEqual(expected.CRC, actual.Crc32) || !OptionalEqual(expected.SHA1, actual.Sha1) || !OptionalEqual(expected.MD5, actual.Md5))
        {
            error = "The same-stem ISO is not an exact MODE1 data view of the CUE track according to the DAT hashes.";
            return false;
        }
        return true;
    }

    private static bool MatchesTrack(ChdManifestTrack actual, ChdManifestTrack expected)
    {
        return actual.Size == expected.Size && OptionalEqual(expected.Crc32, actual.Crc32) &&
               OptionalEqual(expected.Sha1, actual.Sha1) && OptionalEqual(expected.Md5, actual.Md5) &&
               OptionalEqual(expected.Sha256, actual.Sha256);
    }

    private static bool OptionalEqual(byte[] expected, byte[] actual)
    {
        if (expected == null || expected.Length == 0) return true;
        if (actual == null || expected.Length != actual.Length) return false;
        int diff = 0;
        for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
        return diff == 0;
    }

    private static string ResolveWithin(string directory, string relative)
    {
        string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative ?? ""));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsafe primary track path.");
        return path;
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        return crc;
    }

    private static byte[] Hash(byte[] data, HashAlgorithm algorithm)
    {
        using (algorithm)
            return algorithm.ComputeHash(data ?? Array.Empty<byte>());
    }

    private sealed class HashingWriteStream : Stream
    {
        private readonly HashAlgorithm _sha1;
        private readonly HashAlgorithm _md5;
        private readonly HashAlgorithm _sha256;
        private readonly Action<byte> _crc;
        private bool _completed;
        private long _length;

        public HashingWriteStream(HashAlgorithm sha1, HashAlgorithm md5, HashAlgorithm sha256, Action<byte> crc)
        {
            _sha1 = sha1;
            _md5 = md5;
            _sha256 = sha256;
            _crc = crc;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_completed) throw new InvalidOperationException();
            _sha1.TransformBlock(buffer, offset, count, null, 0);
            _md5.TransformBlock(buffer, offset, count, null, 0);
            _sha256.TransformBlock(buffer, offset, count, null, 0);
            for (int i = offset; i < offset + count; i++) _crc(buffer[i]);
            _length += count;
        }

        public void Complete()
        {
            if (_completed) return;
            _sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            _md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            _sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            _completed = true;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
