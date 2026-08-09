using CHDSharpLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RomVaultCore.Utils;

/// <summary>
/// Independent conformance checks for the RVWorld v1 encoding profile.  The
/// checks deliberately compare chdman's extractor with CHDSharpLib's decoder
/// and require repeated encodes to be byte-for-byte deterministic.
/// </summary>
internal static class ChdV1Conformance
{
    public static bool Run(string executable, out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "RomVault-v1-conformance-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            if (!RunRaw(executable, root, out error))
                return false;
            if (!RunOptical(executable, root, out error))
                return false;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static bool RunRaw(string executable, string root, out string error)
    {
        error = "";
        string dir = Path.Combine(root, "raw");
        Directory.CreateDirectory(dir);
        string source = Path.Combine(dir, "source.raw");
        string first = Path.Combine(dir, "first.chd");
        string second = Path.Combine(dir, "second.chd");
        string plain = Path.Combine(dir, "plain.chd");
        string extracted = Path.Combine(dir, "extracted.raw");
        WriteFixture(source, 2 * 1024 * 1024 + 113, 0x31);
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("raw", ChdStorageProfile.Playback);
        string args = $"createraw -i {ChdmanService.Quote(source)} -o {{0}} -c {profile.Codecs} -hs {profile.HunkSize} -us 1 -f";
        ChdmanRunResult step = ChdmanService.Run(executable, string.Format(args, ChdmanService.Quote(first)), dir, 180000);
        if (step.Success)
            step = ChdmanService.Run(executable, string.Format(args, ChdmanService.Quote(second)), dir, 180000);
        if (!step.Success)
        {
            error = "v1 raw deterministic encode failed: " + step.Output;
            return false;
        }
        if (!FilesEqual(first, second))
        {
            error = "v1 zstd raw encoding is not byte-for-byte deterministic.";
            return false;
        }
        step = ChdmanService.Run(executable, $"copy -i {ChdmanService.Quote(first)} -o {ChdmanService.Quote(plain)} -c none -hs {profile.HunkSize} -f", dir, 180000);
        if (step.Success)
            step = ChdmanService.Run(executable, $"extractraw -i {ChdmanService.Quote(first)} -o {ChdmanService.Quote(extracted)} -f", dir, 180000);
        if (!step.Success)
        {
            error = "v1 raw differential decode failed: " + step.Output;
            return false;
        }
        if (!FilesEqual(source, extracted) || !LogicalStreamsEqual(first, plain))
        {
            error = "v1 zstd raw bytes differ between source, chdman extraction, and the native decoder.";
            return false;
        }
        return true;
    }

    private static bool RunOptical(string executable, string root, out string error)
    {
        error = "";
        string dir = Path.Combine(root, "optical");
        Directory.CreateDirectory(dir);
        string track1 = Path.Combine(dir, "track01.bin");
        string track2 = Path.Combine(dir, "track02.bin");
        string cue = Path.Combine(dir, "source.cue");
        string first = Path.Combine(dir, "first.chd");
        string second = Path.Combine(dir, "second.chd");
        string plain = Path.Combine(dir, "plain.chd");
        WriteFixture(track1, 300 * 2352, 0x47);
        WriteFixture(track2, 150 * 2352, 0x59);
        File.WriteAllText(cue,
            "FILE \"track01.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n" +
            "FILE \"track02.bin\" BINARY\r\n" +
            "  TRACK 02 AUDIO\r\n" +
            "    FLAGS PRE\r\n" +
            "    INDEX 01 00:00:00\r\n",
            new UTF8Encoding(false));
        ChdEncodingProfileSpec profile = ChdEncodingProfile.ForFamily("cd", ChdStorageProfile.Playback);
        string args = $"createcd -i {ChdmanService.Quote(cue)} -o {{0}} -c {profile.Codecs} -hs {profile.HunkSize} -f";
        ChdmanRunResult step = ChdmanService.Run(executable, string.Format(args, ChdmanService.Quote(first)), dir, 180000);
        if (step.Success)
            step = ChdmanService.Run(executable, string.Format(args, ChdmanService.Quote(second)), dir, 180000);
        if (!step.Success)
        {
            error = "v1 optical deterministic encode failed: " + step.Output;
            return false;
        }
        if (!FilesEqual(first, second))
        {
            error = "v1 cdzs optical encoding is not byte-for-byte deterministic.";
            return false;
        }
        step = ChdmanService.Run(executable, $"copy -i {ChdmanService.Quote(first)} -o {ChdmanService.Quote(plain)} -c none -hs {profile.HunkSize} -f", dir, 180000);
        string descriptor = Path.Combine(dir, "out.cue");
        string pattern = Path.Combine(dir, "out-track%02t.bin");
        if (step.Success)
            step = ChdmanService.Run(executable, $"extractcd -i {ChdmanService.Quote(first)} -o {ChdmanService.Quote(descriptor)} -ob {ChdmanService.Quote(pattern)} -sb -f", dir, 180000);
        if (!step.Success)
        {
            error = "v1 optical differential decode failed: " + step.Output;
            return false;
        }
        List<string> outputs = Directory.GetFiles(dir, "out-track*.bin").OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        if (outputs.Count != 2 || !FilesEqual(track1, outputs[0]) || !FilesEqual(track2, outputs[1]) || !LogicalStreamsEqual(first, plain))
        {
            error = "v1 cdzs optical bytes differ between source, chdman extraction, and the native decoder.";
            return false;
        }
        return true;
    }

    private static bool LogicalStreamsEqual(string left, string right)
    {
        using (Stream a = ChdLogicalStream.OpenRead(left))
        using (Stream b = ChdLogicalStream.OpenRead(right))
        {
            if (a.Length != b.Length)
                return false;
            byte[] ab = new byte[1024 * 1024];
            byte[] bb = new byte[ab.Length];
            while (true)
            {
                int ar = ReadFull(a, ab);
                int br = ReadFull(b, bb);
                if (ar != br)
                    return false;
                if (ar == 0)
                    return true;
                for (int i = 0; i < ar; i++)
                    if (ab[i] != bb[i])
                        return false;
            }
        }
    }

    private static int ReadFull(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static bool FilesEqual(string left, string right)
    {
        FileInfo a = new FileInfo(left);
        FileInfo b = new FileInfo(right);
        if (!a.Exists || !b.Exists || a.Length != b.Length)
            return false;
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(left))
        {
            byte[] first = sha.ComputeHash(stream);
            using (FileStream other = File.OpenRead(right))
            {
                byte[] second = sha.ComputeHash(other);
                return first.SequenceEqual(second);
            }
        }
    }

    private static void WriteFixture(string path, int length, int seed)
    {
        byte[] buffer = new byte[64 * 1024];
        int position = 0;
        using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            while (position < length)
            {
                int count = Math.Min(buffer.Length, length - position);
                for (int i = 0; i < count; i++)
                    buffer[i] = unchecked((byte)((position + i) * 131 + seed));
                stream.Write(buffer, 0, count);
                position += count;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}
