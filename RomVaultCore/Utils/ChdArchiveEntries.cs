using System;
using System.Collections.Generic;
using System.IO;

namespace RomVaultCore.Utils
{
    internal static class ChdArchiveEntries
    {
        internal static string NormalizeEntryName(string name)
        {
            string normalized = (name ?? "").Replace('\\', '/').Trim().Trim('"');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            return normalized.TrimStart('/');
        }

        internal static bool TryFind(Dictionary<string, int> entries, string requestedName, out string actualName, out int index)
        {
            actualName = null;
            index = -1;
            if (entries == null || string.IsNullOrWhiteSpace(requestedName))
                return false;

            string requested = NormalizeEntryName(requestedName);
            if (entries.TryGetValue(requested, out index))
            {
                actualName = requested;
                return true;
            }

            string requestedBase = Path.GetFileName(requested);
            if (string.IsNullOrWhiteSpace(requestedBase))
                return false;

            string foundName = null;
            int foundIndex = -1;
            foreach (KeyValuePair<string, int> entry in entries)
            {
                if (!string.Equals(Path.GetFileName(entry.Key), requestedBase, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (foundName != null)
                    return false;
                foundName = entry.Key;
                foundIndex = entry.Value;
            }

            if (foundName == null)
                return false;

            actualName = foundName;
            index = foundIndex;
            return true;
        }

        internal static string ResolveReference(string descriptorEntry, string reference)
        {
            string descriptor = NormalizeEntryName(descriptorEntry);
            string requested = NormalizeEntryName(reference);
            if (string.IsNullOrWhiteSpace(requested))
                return null;

            int separator = descriptor.LastIndexOf('/');
            string combined = separator >= 0 ? descriptor.Substring(0, separator + 1) + requested : requested;
            string[] segments = combined.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> resolved = new List<string>(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == ".")
                    continue;
                if (segments[i] == "..")
                {
                    if (resolved.Count == 0)
                        return null;
                    resolved.RemoveAt(resolved.Count - 1);
                    continue;
                }
                resolved.Add(segments[i]);
            }
            return resolved.Count == 0 ? null : string.Join("/", resolved);
        }

        internal static bool RunSelfTest(out string error)
        {
            error = "";
            try
            {
                Dictionary<string, int> entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Disc 1/09 Chairs (Japan).cue"] = 3,
                    ["Disc 1/09 Chairs (Japan) (Track 01).bin"] = 4,
                    ["Disc 2/09 Chairs (Japan) (Track 01).bin"] = 7,
                    ["common/unique.bin"] = 8
                };

                if (!TryFind(entries, "09 Chairs (Japan).cue", out string descriptor, out int descriptorIndex) ||
                    descriptorIndex != 3 || descriptor != "Disc 1/09 Chairs (Japan).cue")
                    throw new InvalidOperationException("The actual descriptor archive path was not retained.");

                string track = ResolveReference(descriptor, "09 Chairs (Japan) (Track 01).bin");
                if (track != "Disc 1/09 Chairs (Japan) (Track 01).bin" ||
                    !TryFind(entries, track, out string actualTrack, out int trackIndex) || trackIndex != 4 || actualTrack != track)
                    throw new InvalidOperationException("A descriptor-relative track resolved to the wrong archive folder.");

                if (TryFind(entries, "09 Chairs (Japan) (Track 01).bin", out _, out _))
                    throw new InvalidOperationException("An ambiguous basename was accepted without descriptor context.");
                if (!TryFind(entries, "unique.bin", out _, out int uniqueIndex) || uniqueIndex != 8)
                    throw new InvalidOperationException("A unique archive basename was not resolved.");
                if (ResolveReference("Disc 1/disc.cue", "../../outside.bin") != null)
                    throw new InvalidOperationException("An archive reference escaped above its root.");

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
