using System;
using System.Collections.Generic;
using RomVaultCore.RvDB;

namespace RomVaultCore.FindFix
{
    /// <summary>
    /// Keeps CHD repair atomic: partial source sets remain loose beside the expected CHD
    /// until every required member is available.
    /// </summary>
    internal static class ChdIncompleteSetPolicy
    {
        public static void PreserveLooseSources(RvFile basePath)
        {
            PreserveLooseSources(basePath, true);
        }

        /// <summary>
        /// A CUE/GDI/TOC shown below a CHD is a reconstructed virtual view, not
        /// an independently removable archive entry.  If a DAT rule moves the
        /// descriptor beside the CHD, do not offer to delete that stale virtual
        /// member on the next Find Fixes pass.
        /// </summary>
        public static void IgnoreVirtualDescriptorRemovals(RvFile basePath)
        {
            if (basePath == null)
                return;

            if (basePath.FileType == FileType.FileCHD &&
                basePath.Parent?.FileType == FileType.CHD &&
                basePath.DatStatus == DatStatus.NotInDat &&
                basePath.GotStatus == GotStatus.Got &&
                IsDescriptor(basePath.Name))
            {
                basePath.RepStatus = RepStatus.Ignore;
            }

            if (!basePath.IsDirectory)
                return;

            for (int i = 0; i < basePath.ChildCount; i++)
                IgnoreVirtualDescriptorRemovals(basePath.Child(i));
        }

        private static void PreserveLooseSources(RvFile directory, bool selected)
        {
            if (directory == null || !directory.IsDirectory)
                return;

            bool nextSelected = selected;
            if (directory.Tree != null)
                nextSelected = directory.Tree.Checked == RvTreeRow.TreeSelect.Selected;

            for (int i = 0; i < directory.ChildCount; i++)
            {
                RvFile child = directory.Child(i);
                if (child == null)
                    continue;

                if (child.Game != null)
                {
                    if (nextSelected)
                        PreserveSetSources(child);
                    continue;
                }

                if (child.IsDirectory)
                    PreserveLooseSources(child, nextSelected);
            }
        }

        internal static void PreserveSetSources(RvFile game)
        {
            if (game == null)
                return;

            List<RvFile> incompleteChds = GetIncompleteChds(game);
            if (incompleteChds.Count == 0)
                return;

            RvFile sourceDirectory = game.FileType == FileType.CHD ? game.Parent : game;
            if (sourceDirectory == null || !sourceDirectory.IsDirectory)
                return;

            for (int i = 0; i < sourceDirectory.ChildCount; i++)
            {
                RvFile source = sourceDirectory.Child(i);
                if (!ShouldPreserve(source) || !BelongsToIncompleteChd(source, incompleteChds))
                    continue;

                source.RepStatus = RepStatus.Ignore;
            }
        }

        private static List<RvFile> GetIncompleteChds(RvFile game)
        {
            List<RvFile> incomplete = new List<RvFile>();
            if (game.FileType == FileType.CHD)
            {
                if (IsIncompleteChd(game))
                    incomplete.Add(game);
                return incomplete;
            }

            if (!game.IsDirectory)
                return incomplete;

            for (int i = 0; i < game.ChildCount; i++)
            {
                RvFile child = game.Child(i);
                if (child?.FileType == FileType.CHD && IsIncompleteChd(child))
                    incomplete.Add(child);
            }

            return incomplete;
        }

        private static bool IsIncompleteChd(RvFile chd)
        {
            for (int memberIndex = 0; memberIndex < chd.ChildCount; memberIndex++)
            {
                RepStatus status = chd.Child(memberIndex).RepStatus;
                if (status == RepStatus.Incomplete || status == RepStatus.IncompleteRemove)
                    return true;
            }

            return false;
        }

        private static bool ShouldPreserve(RvFile source)
        {
            if (source == null || source.FileType != FileType.File || source.GotStatus != GotStatus.Got ||
                source.DatStatus != DatStatus.NotInDat)
                return false;

            if (source.RepStatus != RepStatus.Unknown && source.RepStatus != RepStatus.MoveToSort)
                return false;

            switch (System.IO.Path.GetExtension(source.Name ?? "").ToLowerInvariant())
            {
                case ".cue":
                case ".gdi":
                case ".toc":
                case ".iso":
                case ".bin":
                case ".raw":
                case ".img":
                case ".hdd":
                case ".hd":
                case ".avi":
                case ".sbi":
                    return true;
                default:
                    return false;
            }
        }

        private static bool BelongsToIncompleteChd(RvFile source, List<RvFile> incompleteChds)
        {
            string sourceName = System.IO.Path.GetFileName(source?.Name ?? "");
            if (string.IsNullOrWhiteSpace(sourceName))
                return false;

            for (int i = 0; i < incompleteChds.Count; i++)
            {
                RvFile chd = incompleteChds[i];
                for (int memberIndex = 0; memberIndex < chd.ChildCount; memberIndex++)
                {
                    string memberName = System.IO.Path.GetFileName(chd.Child(memberIndex)?.Name ?? "");
                    if (string.Equals(sourceName, memberName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                string encodedRoot = System.IO.Path.GetFileNameWithoutExtension(chd.Name ?? "");
                if (string.Equals(sourceName, encodedRoot, StringComparison.OrdinalIgnoreCase))
                    return true;

                string mediaBase = System.IO.Path.GetFileNameWithoutExtension(encodedRoot);
                string sourceBase = System.IO.Path.GetFileNameWithoutExtension(sourceName);
                if (string.Equals(sourceBase, mediaBase, StringComparison.OrdinalIgnoreCase) && IsPrimaryDiscSource(sourceName))
                    return true;
            }

            return false;
        }

        private static bool IsPrimaryDiscSource(string name)
        {
            switch (System.IO.Path.GetExtension(name ?? "").ToLowerInvariant())
            {
                case ".cue":
                case ".gdi":
                case ".toc":
                case ".iso":
                case ".raw":
                case ".img":
                case ".hdd":
                case ".hd":
                case ".avi":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDescriptor(string name)
        {
            string extension = System.IO.Path.GetExtension(name ?? "").ToLowerInvariant();
            return extension == ".cue" || extension == ".gdi" || extension == ".toc";
        }

        internal static bool RunSelfTest(out string error)
        {
            error = "";
            try
            {
                RepairStatus.InitStatusCheck();

                RvFile set = new RvFile(FileType.Dir) { Name = "Incomplete game" };
                RvFile chd = new RvFile(FileType.CHD) { Name = "disc.cue.chd" };
                RvFile existingTrack = new RvFile(FileType.FileCHD) { Name = "disc (Track 1).bin" };
                existingTrack.SetDatGotStatus(DatStatus.InDatCollect, GotStatus.Got);
                existingTrack.RepStatus = RepStatus.IncompleteRemove;
                RvFile missingTrack = new RvFile(FileType.FileCHD) { Name = "disc (Track 2).bin" };
                missingTrack.SetDatGotStatus(DatStatus.InDatCollect, GotStatus.NotGot);
                missingTrack.RepStatus = RepStatus.Incomplete;
                chd.ChildAdd(existingTrack);
                chd.ChildAdd(missingTrack);
                set.ChildAdd(chd);

                RvFile cue = MakeLooseSource("disc.cue");
                RvFile bin = MakeLooseSource("disc (Track 1).bin");
                RvFile otherBin = MakeLooseSource("other (Track 1).bin");
                RvFile unrelated = MakeLooseSource("notes.txt");
                set.ChildAdd(cue);
                set.ChildAdd(bin);
                set.ChildAdd(otherBin);
                set.ChildAdd(unrelated);

                PreserveSetSources(set);

                if (cue.RepStatus != RepStatus.Ignore || bin.RepStatus != RepStatus.Ignore)
                    throw new InvalidOperationException("Incomplete CHD source files were not preserved in place.");
                if (otherBin.RepStatus != RepStatus.MoveToSort)
                    throw new InvalidOperationException("A track belonging to another CHD was preserved.");
                if (unrelated.RepStatus != RepStatus.MoveToSort)
                    throw new InvalidOperationException("An unrelated loose file was preserved by the CHD policy.");

                RvFile category = new RvFile(FileType.Dir) { Name = "Category" };
                RvFile directChd = new RvFile(FileType.CHD) { Name = "direct.gdi.chd" };
                RvFile directMissing = new RvFile(FileType.FileCHD) { Name = "direct (Track 1).bin" };
                directMissing.SetDatGotStatus(DatStatus.InDatCollect, GotStatus.NotGot);
                directMissing.RepStatus = RepStatus.Incomplete;
                directChd.ChildAdd(directMissing);
                category.ChildAdd(directChd);
                RvFile gdi = MakeLooseSource("direct.gdi");
                RvFile track = MakeLooseSource("direct (Track 1).bin");
                category.ChildAdd(gdi);
                category.ChildAdd(track);

                PreserveSetSources(directChd);
                if (gdi.RepStatus != RepStatus.Ignore || track.RepStatus != RepStatus.Ignore)
                    throw new InvalidOperationException("Sources beside a direct CHD game node were not preserved.");

                RvFile virtualCue = new RvFile(FileType.FileCHD) { Name = "direct.gdi.cue" };
                virtualCue.SetDatGotStatus(DatStatus.NotInDat, GotStatus.Got);
                virtualCue.RepStatus = RepStatus.Delete;
                directChd.ChildAdd(virtualCue);
                IgnoreVirtualDescriptorRemovals(category);
                if (virtualCue.RepStatus != RepStatus.Ignore)
                    throw new InvalidOperationException("A virtual CHD descriptor was still offered for removal.");

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static RvFile MakeLooseSource(string name)
        {
            RvFile file = new RvFile(FileType.File) { Name = name };
            file.SetDatGotStatus(DatStatus.NotInDat, GotStatus.Got);
            file.RepStatus = RepStatus.MoveToSort;
            return file;
        }
    }
}
