using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RomVaultCore.Utils;

/// <summary>
/// Two-shard Reed-Solomon recovery volumes for collections of independent CHDs.
/// Recovery always writes separate candidates and can reconstruct up to two
/// missing or corrupt files in the same block stripe.
/// </summary>
public static class ChdCollectionParity
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RVCHDPAR");
    private const int Schema = 1;
    private const int FooterBytes = 32;
    private const int MaxManifestBytes = 256 * 1024 * 1024;
    private static readonly byte[,] MultiplyTable = BuildMultiplyTable();

    public static int Create(string collectionRoot, string parityPath, int groupSize, int blockSize, out string report)
    {
        report = "";
        if (!Directory.Exists(collectionRoot)) { report = "CHD collection root was not found."; return 2; }
        if (File.Exists(parityPath)) { report = "Recovery volume already exists; refusing to overwrite it."; return 2; }
        if (groupSize < 2 || groupSize > 32) { report = "Parity group size must be between 2 and 32."; return 2; }
        if (blockSize < 64 * 1024 || blockSize > 16 * 1024 * 1024 || (blockSize & (blockSize - 1)) != 0)
        { report = "Parity block size must be a power of two from 64 KiB through 16 MiB."; return 2; }

        string root = FullDirectory(collectionRoot);
        string output = Path.GetFullPath(parityPath);
        string temp = output + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            List<string> files = Directory.GetFiles(root, "*.chd", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0) { report = "The collection contains no CHD files."; return 2; }
            ParityManifest manifest = BuildManifest(root, files, groupSize, blockSize);
            byte[] manifestBytes = SerializeManifest(manifest);
            byte[] manifestDigest = Hash(manifestBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? Environment.CurrentDirectory);

            using (FileStream destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(destination, Encoding.UTF8, true))
            using (SHA256 parityHash = SHA256.Create())
            {
                writer.Write(Magic);
                writer.Write(Schema);
                writer.Write(manifestBytes.Length);
                writer.Write(manifestDigest);
                writer.Write(manifestBytes);
                writer.Flush();
                byte[] p = new byte[blockSize];
                byte[] q = new byte[blockSize];
                byte[] input = new byte[blockSize];
                for (int groupIndex = 0; groupIndex < manifest.Groups.Count; groupIndex++)
                {
                    ParityGroup group = manifest.Groups[groupIndex];
                    for (int block = 0; block < group.MaxBlocks; block++)
                    {
                        Array.Clear(p, 0, p.Length);
                        Array.Clear(q, 0, q.Length);
                        for (int fileIndex = 0; fileIndex < group.Files.Count; fileIndex++)
                        {
                            ReadSourceBlock(root, group.Files[fileIndex], block, input);
                            byte coefficient = (byte)(fileIndex + 1);
                            for (int offset = 0; offset < blockSize; offset++)
                            {
                                byte value = input[offset];
                                p[offset] ^= value;
                                q[offset] ^= MultiplyTable[coefficient, value];
                            }
                        }
                        WriteHashed(destination, parityHash, p);
                        WriteHashed(destination, parityHash, q);
                    }
                }
                parityHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                writer.Write(parityHash.Hash);
                writer.Flush();
                destination.Flush(true);
            }
            File.Move(temp, output);
            report = "Created RomVault collection recovery volume" + Environment.NewLine +
                     "collection=" + ChdDiagnosticFormatter.RedactPath(root) + Environment.NewLine +
                     "volume=" + ChdDiagnosticFormatter.RedactPath(output) + Environment.NewLine +
                     "files=" + files.Count + Environment.NewLine +
                     "dataShardsPerGroup=" + groupSize + Environment.NewLine +
                     "recoveryShards=2" + Environment.NewLine +
                     "blockSize=" + blockSize;
            return 0;
        }
        catch (Exception ex)
        {
            report = "Could not create CHD recovery volume: " + ex.Message;
            return 5;
        }
        finally { Delete(temp); }
    }

    public static int Verify(string parityPath, string collectionRoot, out string report)
    {
        report = "";
        try
        {
            if (!TryOpen(parityPath, out ParityVolume volume, out report)) return 2;
            string root = FullDirectory(collectionRoot);
            if (!VerifyParityDigest(parityPath, volume, out string parityError)) { report = parityError; return 5; }
            int badFiles = 0;
            int badBlocks = 0;
            for (int groupIndex = 0; groupIndex < volume.Manifest.Groups.Count; groupIndex++)
            {
                ParityGroup group = volume.Manifest.Groups[groupIndex];
                GroupState state = AnalyzeGroup(root, group, volume.Manifest.BlockSize);
                badFiles += state.Affected.Count(value => value);
                for (int i = 0; i < state.Good.Length; i++) badBlocks += state.Good[i].Count(value => !value);
            }
            report = "RomVault collection recovery verification" + Environment.NewLine +
                     "volume=" + ChdDiagnosticFormatter.RedactPath(parityPath) + Environment.NewLine +
                     "files=" + volume.Manifest.Groups.Sum(group => group.Files.Count) + Environment.NewLine +
                     "badFiles=" + badFiles + Environment.NewLine +
                     "badBlocks=" + badBlocks + Environment.NewLine +
                     "recoveryVolume=PASS";
            return badFiles == 0 ? 0 : 5;
        }
        catch (Exception ex) { report = "Could not verify CHD recovery volume: " + ex.Message; return 5; }
    }

    public static int Repair(string parityPath, string collectionRoot, string outputRoot, out string report)
    {
        report = "";
        if (!Directory.Exists(collectionRoot)) { report = "CHD collection root was not found."; return 2; }
        try
        {
            if (!TryOpen(parityPath, out ParityVolume volume, out report)) return 2;
            if (!VerifyParityDigest(parityPath, volume, out string parityError)) { report = parityError; return 5; }
            string root = FullDirectory(collectionRoot);
            string repairRoot = FullDirectoryCreate(outputRoot);
            int repaired = 0;
            List<string> failures = new List<string>();
            using (FileStream parity = File.OpenRead(parityPath))
            {
                long groupOffset = volume.ParityOffset;
                for (int groupIndex = 0; groupIndex < volume.Manifest.Groups.Count; groupIndex++)
                {
                    ParityGroup group = volume.Manifest.Groups[groupIndex];
                    GroupState state = AnalyzeGroup(root, group, volume.Manifest.BlockSize);
                    if (state.Affected.Any(value => value))
                    {
                        if (!RepairGroup(parity, groupOffset, root, repairRoot, group, state, volume.Manifest.BlockSize, out int groupRepaired, out string groupError))
                            failures.Add("group " + groupIndex + ": " + groupError);
                        else
                            repaired += groupRepaired;
                    }
                    groupOffset += checked((long)group.MaxBlocks * 2L * volume.Manifest.BlockSize);
                }
            }
            report = "RomVault collection recovery" + Environment.NewLine +
                     "volume=" + ChdDiagnosticFormatter.RedactPath(parityPath) + Environment.NewLine +
                     "output=" + ChdDiagnosticFormatter.RedactPath(repairRoot) + Environment.NewLine +
                     "repairedCandidates=" + repaired + Environment.NewLine +
                     "failedGroups=" + failures.Count +
                     (failures.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, failures));
            return failures.Count == 0 ? 0 : 5;
        }
        catch (Exception ex) { report = "Could not repair CHD collection: " + ex.Message; return 5; }
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-parity-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string source = Path.Combine(root, "source");
            string repaired = Path.Combine(root, "repaired");
            Directory.CreateDirectory(source);
            byte[][] originals = new byte[4][];
            for (int file = 0; file < originals.Length; file++)
            {
                originals[file] = new byte[180000 + file * 17003];
                for (int i = 0; i < originals[file].Length; i++) originals[file][i] = (byte)(i * 17 + file * 53);
                File.WriteAllBytes(Path.Combine(source, "disc" + file + ".chd"), originals[file]);
            }
            string parity = Path.Combine(root, "collection.rvpar");
            if (Create(source, parity, 4, 65536, out error) != 0) return false;
            byte[] damaged0 = (byte[])originals[0].Clone();
            byte[] damaged1 = (byte[])originals[1].Clone();
            damaged0[70000] ^= 0x55;
            damaged1[70001] ^= 0xaa;
            File.WriteAllBytes(Path.Combine(source, "disc0.chd"), damaged0);
            File.WriteAllBytes(Path.Combine(source, "disc1.chd"), damaged1);
            if (Repair(parity, source, repaired, out error) != 0) return false;
            if (!File.ReadAllBytes(Path.Combine(repaired, "disc0.chd")).SequenceEqual(originals[0]) ||
                !File.ReadAllBytes(Path.Combine(repaired, "disc1.chd")).SequenceEqual(originals[1]))
                throw new InvalidDataException("Two-shard collection recovery changed source bytes.");
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    private static ParityManifest BuildManifest(string root, List<string> paths, int groupSize, int blockSize)
    {
        ParityManifest manifest = new ParityManifest { BlockSize = blockSize, GroupSize = groupSize };
        for (int start = 0; start < paths.Count; start += groupSize)
        {
            ParityGroup group = new ParityGroup();
            foreach (string path in paths.Skip(start).Take(groupSize))
            {
                string relative = path.Substring(root.Length).Replace('\\', '/');
                ParityFile file = HashSource(path, relative, blockSize);
                group.Files.Add(file);
                group.MaxBlocks = Math.Max(group.MaxBlocks, file.BlockHashes.Count);
            }
            manifest.Groups.Add(group);
        }
        return manifest;
    }

    private static ParityFile HashSource(string path, string relative, int blockSize)
    {
        ParityFile result = new ParityFile { RelativePath = relative };
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 full = SHA256.Create())
        {
            result.Length = stream.Length;
            byte[] block = new byte[blockSize];
            int read;
            while ((read = ReadUpTo(stream, block, blockSize)) > 0)
            {
                full.TransformBlock(block, 0, read, null, 0);
                result.BlockHashes.Add(Hash(block, 0, read));
            }
            full.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            result.Sha256 = full.Hash;
        }
        return result;
    }

    private static bool RepairGroup(FileStream parity, long parityOffset, string root, string outputRoot, ParityGroup group, GroupState state, int blockSize, out int repaired, out string error)
    {
        repaired = 0;
        error = "";
        byte[] p = new byte[blockSize];
        byte[] q = new byte[blockSize];
        byte[] data = new byte[blockSize];
        List<int>[] missingByBlock = new List<int>[group.MaxBlocks];
        for (int block = 0; block < group.MaxBlocks; block++)
        {
            List<int> missing = new List<int>();
            for (int file = 0; file < group.Files.Count; file++)
                if (block < group.Files[file].BlockHashes.Count && !state.Good[file][block]) missing.Add(file);
            if (missing.Count > 2) { error = "More than two data shards are damaged in block " + block + "."; return false; }
            missingByBlock[block] = missing;
        }

        for (int fileIndex = 0; fileIndex < group.Files.Count; fileIndex++)
        {
            if (!state.Affected[fileIndex]) continue;
            ParityFile file = group.Files[fileIndex];
            string output = ResolveSafe(outputRoot, file.RelativePath);
            if (File.Exists(output)) { error = "Recovery output already exists: " + file.RelativePath; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string temp = output + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    for (int block = 0; block < file.BlockHashes.Count; block++)
                    {
                        List<int> missing = missingByBlock[block];
                        if (!missing.Contains(fileIndex))
                            ReadSourceBlock(root, file, block, data);
                        else if (!RecoverBlock(parity, parityOffset, root, group, block, missing, fileIndex, blockSize, p, q, data, out error))
                            return false;
                        int count = (int)Math.Min(blockSize, file.Length - (long)block * blockSize);
                        if (!Equal(Hash(data, 0, count), file.BlockHashes[block]))
                        { error = "Recovered block checksum mismatch for " + file.RelativePath + "."; return false; }
                        destination.Write(data, 0, count);
                    }
                    destination.Flush(true);
                }
                if (!Equal(HashFile(temp), file.Sha256)) { error = "Recovered file checksum mismatch for " + file.RelativePath + "."; return false; }
                File.Move(temp, output);
                repaired++;
            }
            finally { Delete(temp); }
        }
        return true;
    }

    private static bool RecoverBlock(FileStream parity, long parityOffset, string root, ParityGroup group, int block, List<int> missing, int targetFileIndex, int blockSize, byte[] p, byte[] q, byte[] output, out string error)
    {
        error = "";
        long offset = checked(parityOffset + (long)block * 2L * blockSize);
        ReadAt(parity, offset, p);
        ReadAt(parity, offset + blockSize, q);
        byte[] known = new byte[blockSize];
        for (int fileIndex = 0; fileIndex < group.Files.Count; fileIndex++)
        {
            if (missing.Contains(fileIndex)) continue;
            ReadSourceBlock(root, group.Files[fileIndex], block, known);
            byte coefficient = (byte)(fileIndex + 1);
            for (int pos = 0; pos < blockSize; pos++)
            {
                p[pos] ^= known[pos];
                q[pos] ^= MultiplyTable[coefficient, known[pos]];
            }
        }
        if (missing.Count == 1)
        {
            byte coefficient = (byte)(missing[0] + 1);
            for (int pos = 0; pos < blockSize; pos++)
            {
                output[pos] = p[pos];
                if (q[pos] != MultiplyTable[coefficient, output[pos]])
                { error = "Parity equations disagree for block " + block + "."; return false; }
            }
            return true;
        }
        if (missing.Count != 2) { error = "No recoverable shard was selected."; return false; }
        byte a = (byte)(missing[0] + 1);
        byte b = (byte)(missing[1] + 1);
        byte denominator = (byte)(a ^ b);
        byte inverse = GfInverse(denominator);
        bool first = targetFileIndex == missing[0];
        for (int pos = 0; pos < blockSize; pos++)
        {
            byte second = Multiply((byte)(q[pos] ^ MultiplyTable[a, p[pos]]), inverse);
            byte firstValue = (byte)(p[pos] ^ second);
            output[pos] = first ? firstValue : second;
        }
        return true;
    }

    private static GroupState AnalyzeGroup(string root, ParityGroup group, int blockSize)
    {
        GroupState state = new GroupState
        {
            Good = new bool[group.Files.Count][],
            Affected = new bool[group.Files.Count]
        };
        for (int fileIndex = 0; fileIndex < group.Files.Count; fileIndex++)
        {
            ParityFile file = group.Files[fileIndex];
            state.Good[fileIndex] = new bool[file.BlockHashes.Count];
            string path = ResolveSafe(root, file.RelativePath);
            if (!File.Exists(path)) { state.Affected[fileIndex] = true; continue; }
            FileInfo info = new FileInfo(path);
            if (info.Length != file.Length) state.Affected[fileIndex] = true;
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] block = new byte[blockSize];
                for (int index = 0; index < file.BlockHashes.Count; index++)
                {
                    int read = ReadUpTo(stream, block, blockSize);
                    state.Good[fileIndex][index] = Equal(Hash(block, 0, read), file.BlockHashes[index]);
                    if (!state.Good[fileIndex][index]) state.Affected[fileIndex] = true;
                }
            }
        }
        return state;
    }

    private static void ReadSourceBlock(string root, ParityFile file, int blockIndex, byte[] buffer)
    {
        Array.Clear(buffer, 0, buffer.Length);
        if (blockIndex >= file.BlockHashes.Count) return;
        string path = ResolveSafe(root, file.RelativePath);
        if (!File.Exists(path)) return;
        using (FileStream stream = File.OpenRead(path))
        {
            stream.Position = (long)blockIndex * buffer.Length;
            ReadUpTo(stream, buffer, buffer.Length);
        }
    }

    private static byte[] SerializeManifest(ParityManifest manifest)
    {
        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory, Encoding.UTF8, true))
        {
            writer.Write(manifest.BlockSize);
            writer.Write(manifest.GroupSize);
            writer.Write(manifest.Groups.Count);
            foreach (ParityGroup group in manifest.Groups)
            {
                writer.Write(group.MaxBlocks);
                writer.Write(group.Files.Count);
                foreach (ParityFile file in group.Files)
                {
                    WriteString(writer, file.RelativePath);
                    writer.Write(file.Length);
                    writer.Write(file.Sha256);
                    writer.Write(file.BlockHashes.Count);
                    foreach (byte[] hash in file.BlockHashes) writer.Write(hash);
                }
            }
            writer.Flush();
            if (memory.Length > MaxManifestBytes) throw new InvalidDataException("Parity manifest is too large.");
            return memory.ToArray();
        }
    }

    private static ParityManifest DeserializeManifest(byte[] bytes)
    {
        using (MemoryStream memory = new MemoryStream(bytes, false))
        using (BinaryReader reader = new BinaryReader(memory, Encoding.UTF8, true))
        {
            ParityManifest manifest = new ParityManifest { BlockSize = reader.ReadInt32(), GroupSize = reader.ReadInt32() };
            if (manifest.BlockSize < 65536 || manifest.BlockSize > 16777216 || (manifest.BlockSize & (manifest.BlockSize - 1)) != 0 || manifest.GroupSize < 2 || manifest.GroupSize > 32)
                throw new InvalidDataException("Invalid recovery-volume geometry.");
            int groupCount = reader.ReadInt32();
            if (groupCount < 1 || groupCount > 100000) throw new InvalidDataException("Invalid recovery group count.");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                ParityGroup group = new ParityGroup { MaxBlocks = reader.ReadInt32() };
                int fileCount = reader.ReadInt32();
                if (group.MaxBlocks < 1 || fileCount < 1 || fileCount > manifest.GroupSize) throw new InvalidDataException("Invalid recovery group.");
                for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
                {
                    ParityFile file = new ParityFile { RelativePath = ReadString(reader), Length = reader.ReadInt64(), Sha256 = ReadExact(reader, 32) };
                    ValidateRelative(file.RelativePath);
                    if (!names.Add(file.RelativePath) || file.Length < 0) throw new InvalidDataException("Duplicate or invalid recovery member.");
                    int blocks = reader.ReadInt32();
                    int expectedBlocks = file.Length == 0 ? 0 : checked((int)((file.Length + manifest.BlockSize - 1) / manifest.BlockSize));
                    if (blocks != expectedBlocks || blocks > group.MaxBlocks) throw new InvalidDataException("Invalid recovery block count.");
                    for (int block = 0; block < blocks; block++) file.BlockHashes.Add(ReadExact(reader, 32));
                    group.Files.Add(file);
                }
                manifest.Groups.Add(group);
            }
            if (memory.Position != memory.Length) throw new InvalidDataException("Unexpected trailing parity manifest data.");
            return manifest;
        }
    }

    private static bool TryOpen(string path, out ParityVolume volume, out string error)
    {
        volume = null;
        error = "";
        if (!File.Exists(path)) { error = "Recovery volume was not found."; return false; }
        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (!Equal(reader.ReadBytes(Magic.Length), Magic) || reader.ReadInt32() != Schema) throw new InvalidDataException("Unsupported recovery-volume format.");
                int manifestLength = reader.ReadInt32();
                if (manifestLength < 1 || manifestLength > MaxManifestBytes) throw new InvalidDataException("Invalid parity manifest length.");
                byte[] expected = ReadExact(reader, 32);
                byte[] manifestBytes = ReadExact(reader, manifestLength);
                if (!Equal(expected, Hash(manifestBytes))) throw new InvalidDataException("Parity manifest checksum mismatch.");
                ParityManifest manifest = DeserializeManifest(manifestBytes);
                long parityOffset = stream.Position;
                long parityBytes = manifest.Groups.Sum(group => checked((long)group.MaxBlocks * 2L * manifest.BlockSize));
                if (stream.Length != checked(parityOffset + parityBytes + FooterBytes)) throw new InvalidDataException("Recovery volume is truncated or has trailing data.");
                volume = new ParityVolume { Manifest = manifest, ParityOffset = parityOffset, ParityBytes = parityBytes };
                return true;
            }
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    private static bool VerifyParityDigest(string path, ParityVolume volume, out string error)
    {
        error = "";
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            stream.Position = volume.ParityOffset;
            byte[] buffer = new byte[1024 * 1024];
            long remaining = volume.ParityBytes;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0) { error = "Recovery volume is truncated."; return false; }
                sha.TransformBlock(buffer, 0, read, null, 0);
                remaining -= read;
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            byte[] expected = new byte[FooterBytes];
            if (stream.Read(expected, 0, expected.Length) != expected.Length || !Equal(expected, sha.Hash))
            { error = "Recovery parity checksum mismatch; repair is unsafe."; return false; }
            return true;
        }
    }

    private static void WriteHashed(Stream output, HashAlgorithm hash, byte[] data)
    {
        hash.TransformBlock(data, 0, data.Length, null, 0);
        output.Write(data, 0, data.Length);
    }

    private static int ReadUpTo(Stream stream, byte[] buffer, int count)
    {
        Array.Clear(buffer, 0, buffer.Length);
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }

    private static void ReadAt(FileStream stream, long offset, byte[] buffer)
    {
        stream.Position = offset;
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read <= 0) throw new EndOfStreamException();
            total += read;
        }
    }

    private static byte[] Hash(byte[] data) => Hash(data, 0, data.Length);
    private static byte[] Hash(byte[] data, int offset, int count)
    {
        using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(data, offset, count);
    }
    private static byte[] HashFile(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream);
    }

    private static byte[,] BuildMultiplyTable()
    {
        byte[,] table = new byte[33, 256];
        for (int coefficient = 1; coefficient <= 32; coefficient++)
            for (int value = 0; value < 256; value++) table[coefficient, value] = Multiply((byte)coefficient, (byte)value);
        return table;
    }

    private static byte Multiply(byte left, byte right)
    {
        int a = left;
        int b = right;
        int value = 0;
        while (b != 0)
        {
            if ((b & 1) != 0) value ^= a;
            a <<= 1;
            if ((a & 0x100) != 0) a ^= 0x11d;
            b >>= 1;
        }
        return (byte)value;
    }

    private static byte GfInverse(byte value)
    {
        if (value == 0) throw new InvalidDataException("Invalid Reed-Solomon coefficient.");
        byte result = 1;
        byte factor = value;
        int power = 254;
        while (power > 0)
        {
            if ((power & 1) != 0) result = Multiply(result, factor);
            factor = Multiply(factor, factor);
            power >>= 1;
        }
        return result;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
        return difference == 0;
    }

    private static string FullDirectory(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException();
        return full;
    }
    private static string FullDirectoryCreate(string path)
    {
        Directory.CreateDirectory(path);
        return FullDirectory(path);
    }
    private static string ResolveSafe(string root, string relative)
    {
        ValidateRelative(relative);
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe recovery member path.");
        return path;
    }
    private static void ValidateRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Replace('\\', '/').Split('/').Any(part => part.Length == 0 || part == ".."))
            throw new InvalidDataException("Unsafe recovery member path.");
    }
    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        if (bytes.Length > 32768) throw new InvalidDataException("Recovery member path is too long.");
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 1 || length > 32768) throw new InvalidDataException("Invalid recovery member path length.");
        return Encoding.UTF8.GetString(ReadExact(reader, length));
    }
    private static byte[] ReadExact(BinaryReader reader, int length)
    {
        byte[] data = reader.ReadBytes(length);
        if (data.Length != length) throw new EndOfStreamException();
        return data;
    }
    private static void Delete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed class ParityManifest { public int BlockSize; public int GroupSize; public List<ParityGroup> Groups = new List<ParityGroup>(); }
    private sealed class ParityGroup { public int MaxBlocks; public List<ParityFile> Files = new List<ParityFile>(); }
    private sealed class ParityFile { public string RelativePath; public long Length; public byte[] Sha256; public List<byte[]> BlockHashes = new List<byte[]>(); }
    private sealed class ParityVolume { public ParityManifest Manifest; public long ParityOffset; public long ParityBytes; }
    private sealed class GroupState { public bool[][] Good; public bool[] Affected; }
}
