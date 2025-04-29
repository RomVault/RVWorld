using DATReader.DatStore;
using RomVaultCore.RvDB;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RomVaultCore.FindFix
{
    public static class ClearPartial
    {
        public static void ResetCorrupt(RvFile basePath)
        {

            for (int i = 0; i < basePath.ChildCount; i++)
            {
                RvFile child = basePath.Child(i);
                if (child.Game != null)
                {
                    if (child.FileType == FileType.Zip && child.GotStatus == GotStatus.Corrupt)
                        child.FileModTimeStamp = long.MinValue;
                }
                else
                    ResetCorrupt(child);
            }
        }

        public static Dictionary<FileGroup, FileGroup> checkGroups = new Dictionary<FileGroup, FileGroup>();

        public static void CheckRemovePartial(RvFile basePath)
        {
            bool nextSelect = false;
            if (basePath.Tree != null)
                nextSelect = basePath.Tree.Checked == RvTreeRow.TreeSelect.Selected;

            DatRule datRule = null;
            if (nextSelect)
                datRule = ReadDat.DatReader.FindDatRule(basePath.DatTreeFullName + "\\");

            for (int i = 0; i < basePath.ChildCount; i++)
            {
                RvFile child = basePath.Child(i);
                if (child.Game != null)
                {
                    if (nextSelect && datRule != null && datRule.CompleteOnly)
                        RemovePartialSets(child);
                }
                else
                    CheckRemovePartial(child);
            }
        }

        private static void StatusCheck(RvFile f, ref bool foundMissing, ref bool foundGotOrFixable)
        {
            RepStatus gs = f.RepStatus;
            switch (gs)
            {
                case RepStatus.Missing:
                case RepStatus.MissingMIA:
                    foundMissing = true;
                    break;
                case RepStatus.Correct:
                case RepStatus.CorrectMIA:
                    foundGotOrFixable = true;
                    break;
                case RepStatus.CanBeFixed:
                case RepStatus.CanBeFixedMIA:
                    foundGotOrFixable = true;
                    break;
                case RepStatus.MoveToSort:
                case RepStatus.NotCollected:
                    break;
                default:
                    Debug.WriteLine("Unknown");
                    break;
            }
        }


        private static void StatusSet(RvFile f)
        {
            RepStatus gs = f.RepStatus;
            switch (gs)
            {
                case RepStatus.Missing:
                case RepStatus.MissingMIA:
                case RepStatus.NotCollected:
                    break;

                case RepStatus.CanBeFixed:
                    f.RepStatus = RepStatus.Incomplete;
                    if (!checkGroups.ContainsKey(f.FileGroup))
                        checkGroups.Add(f.FileGroup, f.FileGroup);
                    break;
                case RepStatus.CanBeFixedMIA:
                    f.RepStatus = RepStatus.Incomplete;
                    if (!checkGroups.ContainsKey(f.FileGroup))
                        checkGroups.Add(f.FileGroup, f.FileGroup);
                    break;

                case RepStatus.Correct:
                case RepStatus.CorrectMIA:
                    f.RepStatus = RepStatus.IncompleteRemove;
                    if (!checkGroups.ContainsKey(f.FileGroup))
                        checkGroups.Add(f.FileGroup, f.FileGroup);
                    break;

                case RepStatus.MoveToSort:
                    break;

                default:
                    Debug.WriteLine("Unknown");
                    break;
            }
        }

        private static void StatusCheckDir(RvFile f, ref bool foundMissing, ref bool foundGotOrFixable)
        {
            int cCount = f.ChildCount;
            for (int i = 0; i < cCount; i++)
            {
                RvFile fChild = f.Child(i);

                if (fChild.IsDirectory)
                    StatusCheckDir(fChild, ref foundMissing, ref foundGotOrFixable);
                else
                    StatusCheck(fChild, ref foundMissing, ref foundGotOrFixable);
            }
        }
        private static void StatusSetDir(RvFile f)
        {
            int cCount = f.ChildCount;
            for (int i = 0; i < cCount; i++)
            {
                RvFile fChild = f.Child(i);

                if (fChild.IsDirectory)
                    StatusSetDir(fChild);
                else
                    StatusSet(fChild);
            }
        }

        private static void RemovePartialSets(RvFile dbDir)
        {
            if (dbDir.Game == null)
                return;
            // do check
            //Debug.WriteLine($"Check file {dbDir.FileName}");
            // check if complete and exit
            bool foundMissing = false;
            bool foundGotOrFixable = false;

            StatusCheckDir(dbDir, ref foundMissing, ref foundGotOrFixable);
            if (!foundMissing || !foundGotOrFixable)
                return;

            // set remove status
            StatusSetDir(dbDir);
        }

        public static void checkAllGroups()
        {
            Parallel.ForEach(checkGroups, fg => RecheckFileGroup(fg.Value));
        }

        private static void RecheckFileGroup(FileGroup fGroup)
        {
            foreach (RvFile f in fGroup.Files)
            {
                if (f.RepStatus == RepStatus.Incomplete || f.RepStatus == RepStatus.IncompleteRemove)
                    continue;
                f.RepStatusReset();
            }

            FindFixesListCheck.ListCheck(fGroup);
        }
    }
}
