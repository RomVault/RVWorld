using Compress;
using DATReader.DatStore;

namespace DATReader.DatClean
{
    /// <summary>
    /// Applies container and compression typing rules to DAT trees (directories, archives, CHDs).
    /// </summary>
    public static class DatSetCompressionType
    {
        public static bool ChdStrictCueGdi = false;
        public static bool ChdKeepCueGdi = false;

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
                    if (ext == ".cue" || ext == ".gdi")
                    {
                        if (!ChdStrictCueGdi && !ChdKeepCueGdi)
                            dFile.DatStatus = DatStatus.InDatMerged;
                    }
                }
                return;
            }

            if (!(inDat is DatDir dDir))
                return;

            bool chdSidecarMode =
                fileType == FileType.CHD &&
                ChdKeepCueGdi &&
                dDir.DGame != null;

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
                dDir.FileType = chdSidecarMode ? FileType.Dir : fileType;

                ZipStructure zsChecked = IsTrrntzipDateTimes(dDir, zs) ? ZipStructure.ZipTrrnt : zs;
                dDir.SetDatStruct(zsChecked, fix);
            }


            DatBase[] children = dDir.ToArray();
            if (children == null)
                return;

            dDir.ChildrenClear();

            if (chdSidecarMode)
            {
                string baseName = dDir.Name ?? "";
                if (baseName.EndsWith(".chd", System.StringComparison.OrdinalIgnoreCase))
                    baseName = System.IO.Path.GetFileNameWithoutExtension(baseName);
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = dDir.Name ?? "";

                dDir.Name = baseName;

                DatDir chdContainer = new DatDir(baseName, FileType.CHD) { DatStatus = dDir.DatStatus };
                for (int i = 0; i < children.Length; i++)
                {
                    DatBase child = children[i];
                    if (child is DatFile df)
                    {
                        string ext = System.IO.Path.GetExtension(df.Name)?.ToLowerInvariant() ?? "";
                        if (ext == ".cue" || ext == ".gdi")
                        {
                            SetType(df, FileType.Dir, ZipStructure.None, fix);
                            dDir.ChildAdd(df);
                            continue;
                        }
                    }
                    chdContainer.ChildAdd(child);
                }

                SetType(chdContainer, FileType.CHD, ZipStructure.None, fix);
                dDir.ChildAdd(chdContainer);
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
