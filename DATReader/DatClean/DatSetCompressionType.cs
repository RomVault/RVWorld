using Compress;
using DATReader.DatStore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DATReader.DatClean
{
    /// <summary>
    /// Applies container and compression typing rules to DAT trees (directories, archives, CHDs).
    /// </summary>
    public static class DatSetCompressionType
    {
        public static bool ChdStrictCueGdi = false;
        public static bool ChdKeepCueGdi = false;
        public static bool ChdMultiView = true;
        public static string ChdMediaType = "Auto";

        private static FileType GetFileTypeFromDir(FileType fileType)
        {
            switch (fileType)
            {
                case FileType.Dir:
                    return FileType.File;
                case FileType.Zip:
                    return FileType.FileZip;
                case FileType.SevenZip:
                    return FileType.FileSevenZip;
                case FileType.CHD:
                    return FileType.FileCHD;
                default:
                    return FileType.File;
            }
        }

        public static void SetType(DatBase inDat, FileType fileType, ZipStructure zs, bool fix)
        {
            if (inDat is DatFile dFile)
            {
                dFile.FileType = GetFileTypeFromDir(fileType);
                if (fileType == FileType.CHD && dFile.isDisk == false)
                {
                    string ext = System.IO.Path.GetExtension(dFile.Name)?.ToLowerInvariant() ?? "";
                    if (ext == ".cue" || ext == ".gdi" || ext == ".toc")
                    {
                        if (!ChdStrictCueGdi && !ChdKeepCueGdi)
                            dFile.DatStatus = DatStatus.InDatMerged;
                    }
                }
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            bool chdMediaGraphMode =
                fileType == FileType.CHD &&
                dDir.DGame != null &&
                HasMediaRoots(dDir);

            if (dDir.DGame == null || fileType == FileType.Dir)
            {
                if (dDir.FileType != FileType.CHD)
                    dDir.FileType = FileType.Dir;
            }
            else
            {
                if (dDir.FileType!=FileType.UnSet)
                {
                    if (dDir.FileType == FileType.Dir)
                    {
                        fileType = FileType.Dir;
                        zs = ZipStructure.None;
                    }
                    if (dDir.FileType == FileType.Zip)
                    {
                        fileType = FileType.Zip;
                        zs = ZipStructure.ZipTrrnt;
                    }
                    if (dDir.FileType == FileType.SevenZip)
                    {
                        fileType = FileType.SevenZip;
                        zs = ZipStructure.SevenZipNZSTD;
                    }
                    if (dDir.FileType == FileType.CHD)
                    {
                        fileType = FileType.CHD;
                        zs = ZipStructure.None;
                    }
                }
                dDir.FileType = chdMediaGraphMode ? FileType.Dir : fileType;

                ZipStructure zsChecked = IsTrrntzipDateTimes(dDir, zs) ? ZipStructure.ZipTrrnt : zs;
                dDir.SetDatStruct(zsChecked, fix);
            }


            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            if (chdMediaGraphMode)
            {
                BuildMediaGraphs(dDir, children, fix);
                return;
            }

            if (dDir.FileType == FileType.CHD)
            {
                System.Collections.Generic.List<DatBase> flattened = new System.Collections.Generic.List<DatBase>();
                foreach (DatBase child in children)
                {
                    if (child is DatDir childDir)
                    {
                        FlattenChdChildren(childDir, childDir.Name + "/", flattened);
                        continue;
                    }
                    flattened.Add(child);
                }

                foreach (DatBase child in flattened)
                {
                    SetType(child, fileType, zs, fix);
                    dDir.ChildAdd(child);
                }
                return;
            }

            foreach (DatBase child in children)
            {
                SetType(child, fileType, zs, fix);
                dDir.ChildAdd(child);
            }

        }

        private sealed class MediaGraph
        {
            public DatFile Root;
            public string Extension;
            public string Stem;
            public readonly List<DatBase> Payloads = new List<DatBase>();
        }

        private static bool HasMediaRoots(DatDir dir)
        {
            DatBase[] children = dir?.ToArray();
            if (children == null)
                return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is DatFile file && IsMediaRoot(file))
                    return true;
            }
            return false;
        }

        private static void BuildMediaGraphs(DatDir parent, DatBase[] children, bool fix)
        {
            List<MediaGraph> graphs = new List<MediaGraph>();
            Dictionary<DatBase, MediaGraph> ownership = new Dictionary<DatBase, MediaGraph>();
            for (int i = 0; i < children.Length; i++)
            {
                if (!(children[i] is DatFile root) || !IsMediaRoot(root))
                    continue;
                string extension = Path.GetExtension(root.Name ?? "").ToLowerInvariant();
                MediaGraph graph = new MediaGraph
                {
                    Root = root,
                    Extension = extension,
                    Stem = NormalizeStem(root.Name)
                };
                graphs.Add(graph);
                ownership[root] = graph;
            }

            List<MediaGraph> descriptors = graphs.FindAll(graph => graph.Extension == ".cue" || graph.Extension == ".gdi" || graph.Extension == ".toc");
            for (int i = 0; i < children.Length; i++)
            {
                DatBase child = children[i];
                if (!(child is DatFile file) || ownership.ContainsKey(child) || !IsDescriptorPayload(file.Name))
                    continue;

                MediaGraph owner = string.Equals(Path.GetExtension(file.Name ?? ""), ".sbi", StringComparison.OrdinalIgnoreCase)
                    ? descriptors.Find(graph => string.Equals(graph.Stem, NormalizeStem(file.Name), StringComparison.OrdinalIgnoreCase))
                    : FindPayloadOwner(file.Name, descriptors);
                if (owner == null)
                    continue;
                owner.Payloads.Add(child);
                ownership[child] = owner;
            }

            if (ChdMultiView)
            {
                // A same-stem ISO can be a second exact representation of a
                // single-track CUE.  Group it as a candidate view; creation
                // still proves sector conversion and every DAT hash before it
                // is allowed into the CHD manifest.
                List<MediaGraph> bundled = new List<MediaGraph>();
                for (int i = 0; i < graphs.Count; i++)
                {
                    MediaGraph iso = graphs[i];
                    if (iso.Extension != ".iso")
                        continue;
                    MediaGraph descriptor = descriptors.Find(value => value.Extension == ".cue" && string.Equals(value.Stem, iso.Stem, StringComparison.OrdinalIgnoreCase));
                    if (descriptor == null || descriptor.Payloads.Count == 0)
                        continue;
                    descriptor.Payloads.Add(iso.Root);
                    ownership[iso.Root] = descriptor;
                    bundled.Add(iso);
                }
                for (int i = 0; i < bundled.Count; i++)
                    graphs.Remove(bundled[i]);
            }

            for (int i = 0; i < graphs.Count; i++)
            {
                MediaGraph graph = graphs[i];
                bool descriptor = graph.Extension == ".cue" || graph.Extension == ".gdi" || graph.Extension == ".toc";
                if (descriptor && graph.Payloads.Count == 0)
                    continue;

                DatDir container = new DatDir(graph.Root.Name, FileType.CHD) { DatStatus = parent.DatStatus };
                if (!descriptor || !ChdKeepCueGdi)
                    container.ChildAdd(graph.Root);
                for (int j = 0; j < graph.Payloads.Count; j++)
                    container.ChildAdd(graph.Payloads[j]);
                SetType(container, FileType.CHD, ZipStructure.None, fix);
                parent.ChildAdd(container);

                if (descriptor && ChdKeepCueGdi)
                {
                    SetType(graph.Root, FileType.Dir, ZipStructure.None, fix);
                    parent.ChildAdd(graph.Root);
                }
            }

            for (int i = 0; i < children.Length; i++)
            {
                DatBase child = children[i];
                if (ownership.TryGetValue(child, out MediaGraph graph))
                {
                    bool graphBuilt = (graph.Extension != ".cue" && graph.Extension != ".gdi" && graph.Extension != ".toc") || graph.Payloads.Count > 0;
                    if (graphBuilt)
                        continue;
                }
                SetType(child, FileType.Dir, ZipStructure.None, fix);
                parent.ChildAdd(child);
            }
        }

        private static MediaGraph FindPayloadOwner(string payloadName, List<MediaGraph> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
                return null;
            if (descriptors.Count == 1)
                return descriptors[0];

            string payloadStem = NormalizeStem(payloadName);
            MediaGraph best = null;
            int bestLength = -1;
            bool ambiguous = false;
            for (int i = 0; i < descriptors.Count; i++)
            {
                string rootStem = descriptors[i].Stem;
                if (!IsStemPrefix(payloadStem, rootStem))
                    continue;
                if (rootStem.Length > bestLength)
                {
                    best = descriptors[i];
                    bestLength = rootStem.Length;
                    ambiguous = false;
                }
                else if (rootStem.Length == bestLength)
                {
                    ambiguous = true;
                }
            }
            return ambiguous ? null : best;
        }

        private static bool IsMediaRoot(DatFile file)
        {
            if (file == null || file.isDisk)
                return false;
            string extension = Path.GetExtension(file.Name ?? "").ToLowerInvariant();
            switch (ChdMediaType ?? "Auto")
            {
                case "Raw":
                    return extension == ".raw";
                case "HardDisk":
                    return extension == ".img" || extension == ".hdd" || extension == ".hd" || extension == ".raw";
                case "LaserDisc":
                    return extension == ".avi";
                default:
                    return extension == ".cue" || extension == ".gdi" || extension == ".toc" || extension == ".iso" ||
                           extension == ".img" || extension == ".hdd" || extension == ".hd" || extension == ".avi";
            }
        }

        private static bool IsDescriptorPayload(string name)
        {
            switch (Path.GetExtension(name ?? "").ToLowerInvariant())
            {
                case ".bin":
                case ".raw":
                case ".sbi":
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeStem(string name)
        {
            string stem = Path.GetFileNameWithoutExtension(Path.GetFileName(name ?? "")) ?? "";
            return Regex.Replace(stem.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        private static bool IsStemPrefix(string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(prefix) ||
                !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            if (value.Length == prefix.Length)
                return true;
            char boundary = value[prefix.Length];
            return char.IsWhiteSpace(boundary) || boundary == '(' || boundary == '[' ||
                   boundary == '-' || boundary == '_' || boundary == '.';
        }

        public static bool RunMediaGraphSelfTest(out string error)
        {
            error = "";
            bool oldKeep = ChdKeepCueGdi;
            bool oldStrict = ChdStrictCueGdi;
            bool oldMultiView = ChdMultiView;
            string oldMediaType = ChdMediaType;
            try
            {
                ChdKeepCueGdi = false;
                ChdStrictCueGdi = false;
                ChdMultiView = true;
                ChdMediaType = "Auto";
                DatDir set = new DatDir("Mixed PC set", FileType.UnSet) { DGame = new DatGame() };
                set.ChildAdd(new DatFile("Disc.cue", FileType.UnSet));
                set.ChildAdd(new DatFile("Disc (Track 01).bin", FileType.UnSet));
                set.ChildAdd(new DatFile("Disc.iso", FileType.UnSet));
                set.ChildAdd(new DatFile("Disc.sbi", FileType.UnSet));
                set.ChildAdd(new DatFile("Install.iso", FileType.UnSet));
                set.ChildAdd(new DatFile("Machine.img", FileType.UnSet));
                set.ChildAdd(new DatFile("Side A.avi", FileType.UnSet));
                set.ChildAdd(new DatFile("notes.txt", FileType.UnSet));

                SetType(set, FileType.CHD, ZipStructure.None, true);
                DatClean.SetExt(set, HeaderFileType.Nothing);

                DatDir cue = FindChildDir(set, "Disc.cue.chd");
                DatDir iso = FindChildDir(set, "Install.iso.chd");
                DatDir hdd = FindChildDir(set, "Machine.img.chd");
                DatDir laserDisc = FindChildDir(set, "Side A.avi.chd");
                if (set.FileType != FileType.Dir || cue == null || iso == null || hdd == null || laserDisc == null ||
                    !HasChild(cue, "Disc.cue") || !HasChild(cue, "Disc (Track 01).bin") ||
                    !HasChild(cue, "Disc.iso") || !HasChild(cue, "Disc.sbi") || FindChildDir(set, "Disc.iso.chd") != null ||
                    HasChild(cue, "Install.iso") || !HasChild(iso, "Install.iso") ||
                    !HasChild(hdd, "Machine.img") || !HasChild(laserDisc, "Side A.avi") ||
                    !HasChild(set, "notes.txt"))
                {
                    error = "Mixed CUE/BIN and ISO DATs were not partitioned into independent media graphs.";
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
                ChdKeepCueGdi = oldKeep;
                ChdStrictCueGdi = oldStrict;
                ChdMultiView = oldMultiView;
                ChdMediaType = oldMediaType;
            }
        }

        private static DatDir FindChildDir(DatDir parent, string name)
        {
            DatBase[] children = parent?.ToArray();
            if (children == null)
                return null;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is DatDir dir && string.Equals(dir.Name, name, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
            return null;
        }

        private static bool HasChild(DatDir parent, string name)
        {
            DatBase[] children = parent?.ToArray();
            if (children == null)
                return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (string.Equals(children[i]?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void FlattenChdChildren(DatDir dir, string prefix, System.Collections.Generic.List<DatBase> output)
        {
            DatBase[] children = dir.ToArray();
            if (children == null)
                return;

            foreach (DatBase child in children)
            {
                if (child is DatDir childDir)
                {
                    FlattenChdChildren(childDir, prefix + childDir.Name + "/", output);
                    continue;
                }
                if (child is DatFile childFile)
                {
                    DatFile copy = new DatFile(childFile);
                    copy.Name = prefix + childFile.Name;
                    output.Add(copy);
                }
            }
        }


        private static bool IsTrrntzipDateTimes(DatDir dDir, ZipStructure zs)
        {
            if (dDir.FileType != FileType.Zip || zs != ZipStructure.ZipTDC)
                return false;

            DatBase[] children = dDir.ToArray();
            foreach (DatBase child in children)
            {
                if (child is DatFile)
                {
                    if (child.DateModified != Compress.StructuredZip.StructuredZip.TrrntzipDosDateTime)
                        return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }


    }
}
