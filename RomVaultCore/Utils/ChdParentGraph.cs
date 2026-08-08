using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CHDSharpLib;

namespace RomVaultCore.Utils;

public static class ChdParentGraph
{
    public static int Verify(string childPath, string parentPath, out string report)
    {
        return VerifyWithTool(childPath, parentPath, ChdmanProcessTracker.FindExecutable(), out report);
    }

    private static int VerifyWithTool(string childPath, string parentPath, string tool, out string report)
    {
        report = "";
        if (!TryValidatePair(childPath, parentPath, out ChdContainerInfo child, out _, out report)) return 2;
        ChdmanRunResult result = ChdmanService.Run(tool,
            "verify -i " + ChdmanService.Quote(childPath) + " -ip " + ChdmanService.Quote(parentPath),
            Path.GetDirectoryName(childPath) ?? Environment.CurrentDirectory,
            600000);
        report = result.Success ? "Parent graph verified: " + Hex(child.ParentSha1) : "Parent graph verification failed: " + result.Output;
        return result.Success ? 0 : 5;
    }

    public static int CreateChild(string standalonePath, string parentPath, string outputPath, out string report)
    {
        return CreateChildWithTool(standalonePath, parentPath, outputPath, ChdmanProcessTracker.FindExecutable(), out report);
    }

    private static int CreateChildWithTool(string standalonePath, string parentPath, string outputPath, string tool, out string report)
    {
        report = "";
        if (!File.Exists(standalonePath) || !File.Exists(parentPath)) { report = "Standalone source or parent CHD was not found."; return 2; }
        if (File.Exists(outputPath)) { report = "Parented output already exists; refusing to overwrite it."; return 2; }
        if (!ChdMetadata.TryReadContainerInfo(standalonePath, out ChdContainerInfo source, out string sourceError) || source.RequiresParent)
        { report = "Source must be a readable standalone CHD: " + sourceError; return 2; }
        if (!ChdMetadata.TryReadContainerInfo(parentPath, out ChdContainerInfo parent, out string parentError) || parent.RequiresParent)
        { report = "Parent must be a readable standalone CHD: " + parentError; return 2; }
        if (Path.GetFullPath(standalonePath).Equals(Path.GetFullPath(parentPath), StringComparison.OrdinalIgnoreCase))
        { report = "A CHD cannot be its own parent."; return 2; }

        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        string token = Guid.NewGuid().ToString("N");
        string childTemp = outputPath + ".__rvchild." + token + ".chd";
        string materialized = outputPath + ".__rvstandalone." + token + ".chd";
        try
        {
            ChdmanRunResult create = ChdmanService.Run(tool,
                "copy -i " + ChdmanService.Quote(standalonePath) + " -o " + ChdmanService.Quote(childTemp) + " -op " + ChdmanService.Quote(parentPath) + " -f",
                directory,
                600000);
            if (!create.Success) { report = "Could not create parented CHD: " + create.Output; return 5; }
            if (VerifyWithTool(childTemp, parentPath, tool, out string verifyReport) != 0) { report = verifyReport; return 5; }
            int standaloneCode = MaterializeStandaloneInternal(childTemp, parentPath, materialized, tool, out string standaloneReport);
            if (standaloneCode != 0) { report = standaloneReport; return standaloneCode; }
            if (!ChdMetadata.TryReadContainerInfo(materialized, out ChdContainerInfo roundTrip, out _) ||
                !HashEqual(source.RawSha1 ?? source.Sha1, roundTrip.RawSha1 ?? roundTrip.Sha1) ||
                !string.Equals(LogicalSha256(standalonePath), LogicalSha256(materialized), StringComparison.OrdinalIgnoreCase))
            {
                report = "Parented CHD failed standalone materialization parity.";
                return 5;
            }
            File.Move(childTemp, outputPath);
            report = "Created reversible parented CHD; parentSha1=" + Hex(parent.RawSha1 ?? parent.Sha1);
            return 0;
        }
        finally
        {
            Delete(childTemp);
            Delete(materialized);
        }
    }

    public static int MaterializeStandalone(string childPath, string parentPath, string outputPath, out string report)
    {
        if (File.Exists(outputPath)) { report = "Standalone output already exists; refusing to overwrite it."; return 2; }
        string tool = ChdmanProcessTracker.FindExecutable();
        string temp = outputPath + ".__rvstandalone." + Guid.NewGuid().ToString("N") + ".chd";
        try
        {
            int code = MaterializeStandaloneInternal(childPath, parentPath, temp, tool, out report);
            if (code == 0) File.Move(temp, outputPath);
            return code;
        }
        finally { Delete(temp); }
    }

    public static int ValidateGraph(IEnumerable<string> paths, out string report)
    {
        Dictionary<string, Node> byId = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        List<Node> nodes = new List<Node>();
        foreach (string path in (paths ?? Array.Empty<string>()).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ChdMetadata.TryReadContainerInfo(path, out ChdContainerInfo info, out string error))
            { report = ChdDiagnosticFormatter.RedactPath(path) + ": " + error; return 5; }
            Node node = new Node { Path = path, Info = info };
            nodes.Add(node);
            AddId(byId, Hex(info.Sha1), node);
            AddId(byId, Hex(info.RawSha1), node);
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!nodes[i].Info.RequiresParent) continue;
            string parentId = Hex(nodes[i].Info.ParentSha1);
            if (!byId.TryGetValue(parentId, out Node parent))
            { report = ChdDiagnosticFormatter.RedactPath(nodes[i].Path) + ": missing parent " + parentId; return 5; }
            nodes[i].Parent = parent;
        }
        HashSet<Node> complete = new HashSet<Node>();
        for (int i = 0; i < nodes.Count; i++)
        {
            HashSet<Node> active = new HashSet<Node>();
            Node current = nodes[i];
            while (current != null && !complete.Contains(current))
            {
                if (!active.Add(current))
                { report = "Parent graph contains a cycle at " + ChdDiagnosticFormatter.RedactPath(current.Path); return 5; }
                current = current.Parent;
            }
            foreach (Node node in active) complete.Add(node);
        }
        report = "Parent graph valid; nodes=" + nodes.Count + "; parented=" + nodes.Count(node => node.Info.RequiresParent);
        return 0;
    }

    internal static bool RunFixture(string executable, out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "rv-chd-parent-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string parentRaw = Path.Combine(root, "parent.raw");
            string childRaw = Path.Combine(root, "child.raw");
            byte[] parentBytes = new byte[64 * 1024];
            byte[] childBytes = new byte[parentBytes.Length];
            for (int i = 0; i < parentBytes.Length; i++) { parentBytes[i] = (byte)i; childBytes[i] = (byte)i; }
            for (int i = 0; i < 128; i++) childBytes[4096 + i] ^= 0x5a;
            File.WriteAllBytes(parentRaw, parentBytes); File.WriteAllBytes(childRaw, childBytes);
            string parent = Path.Combine(root, "parent.chd"); string source = Path.Combine(root, "source.chd"); string child = Path.Combine(root, "child.chd"); string standalone = Path.Combine(root, "standalone.chd");
            ChdmanRunResult a = ChdmanService.Run(executable, "createraw -i " + ChdmanService.Quote(parentRaw) + " -o " + ChdmanService.Quote(parent) + " -us 1 -hs 4096 -c zlib -f", root, 180000);
            ChdmanRunResult b = ChdmanService.Run(executable, "createraw -i " + ChdmanService.Quote(childRaw) + " -o " + ChdmanService.Quote(source) + " -us 1 -hs 4096 -c zlib -f", root, 180000);
            if (!a.Success || !b.Success) throw new InvalidDataException((a.Success ? b.Output : a.Output));
            if (CreateChildWithTool(source, parent, child, executable, out string createReport) != 0) throw new InvalidDataException(createReport);
            if (MaterializeStandaloneInternal(child, parent, standalone, executable, out string materializeReport) != 0) throw new InvalidDataException(materializeReport);
            if (ValidateGraph(new[] { parent, child }, out string graphReport) != 0) throw new InvalidDataException(graphReport);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    private static int MaterializeStandaloneInternal(string childPath, string parentPath, string outputPath, string tool, out string report)
    {
        report = "";
        if (!TryValidatePair(childPath, parentPath, out ChdContainerInfo child, out _, out report)) return 2;
        string work = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        ChdmanRunResult copy = ChdmanService.Run(tool,
            "copy -i " + ChdmanService.Quote(childPath) + " -ip " + ChdmanService.Quote(parentPath) + " -o " + ChdmanService.Quote(outputPath) + " -f",
            work,
            600000);
        if (!copy.Success) { report = "Standalone conversion failed: " + copy.Output; return 5; }
        ChdmanRunResult verify = ChdmanService.Run(tool, "verify -i " + ChdmanService.Quote(outputPath), work, 600000);
        if (!verify.Success) { report = "Standalone verification failed: " + verify.Output; return 5; }
        if (!ChdMetadata.TryReadContainerInfo(outputPath, out ChdContainerInfo standalone, out string error) || standalone.RequiresParent ||
            !HashEqual(child.RawSha1 ?? child.Sha1, standalone.RawSha1 ?? standalone.Sha1))
        { report = "Standalone header parity failed: " + error; return 5; }
        report = "Materialized verified standalone CHD.";
        return 0;
    }

    private static bool TryValidatePair(string childPath, string parentPath, out ChdContainerInfo child, out ChdContainerInfo parent, out string error)
    {
        child = null; parent = null; error = "";
        if (!File.Exists(childPath) || !File.Exists(parentPath)) { error = "Child or parent CHD was not found."; return false; }
        if (!ChdMetadata.TryReadContainerInfo(childPath, out child, out error) || !child.RequiresParent) { error = "Child is not a readable parented CHD. " + error; return false; }
        if (!ChdMetadata.TryReadContainerInfo(parentPath, out parent, out error)) return false;
        if (!HashEqual(child.ParentSha1, parent.Sha1) && !HashEqual(child.ParentSha1, parent.RawSha1)) { error = "The supplied parent hash does not match the child header."; return false; }
        return true;
    }

    private static string LogicalSha256(string path)
    {
        using (Stream stream = ChdLogicalStream.OpenRead(path)) return ChdScrubber.HashSha256(stream);
    }

    private static bool HashEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0) return false;
        int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0;
    }

    private static string Hex(byte[] value)
    {
        return value == null ? "" : string.Concat(value.Select(item => item.ToString("x2")));
    }

    private static void AddId(Dictionary<string, Node> map, string id, Node node)
    {
        if (!string.IsNullOrWhiteSpace(id) && !map.ContainsKey(id)) map.Add(id, node);
    }

    private static void Delete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed class Node { public string Path; public ChdContainerInfo Info; public Node Parent; }
}
