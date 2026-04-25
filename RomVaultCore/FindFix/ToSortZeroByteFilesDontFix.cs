using CHDSharpLib;
using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RomVaultCore.FindFix
{
    public static class ToSortZeroByteFilesDontFix
    {
        public static void Clear(FileGroup zeroGroup)
        {
            if (zeroGroup.Size != 0)
                throw new Exception("This is not a zero byte file group");
            if (zeroGroup.CRC[0] != 0 || zeroGroup.CRC[1] != 0 || zeroGroup.CRC[2] != 0 || zeroGroup.CRC[3] != 0)
                throw new Exception("This is not a zero byte file group");

            Dictionary<string, RvFile> toSortFilesWithZeroBytes = new Dictionary<string, RvFile>();
            foreach (RvFile file in zeroGroup.Files)
            {
                if (!file.IsInToSort)
                    continue;
                if (file.FileType != FileType.FileSevenZip && file.FileType != FileType.FileZip)
                    continue;
                Debug.Write(file.Name);
                string parentFullname = file.Parent.TreeFullName;
                if (!toSortFilesWithZeroBytes.ContainsKey(parentFullname))
                    toSortFilesWithZeroBytes.Add(parentFullname, file.Parent);
            }

            foreach (KeyValuePair<string, RvFile> file in toSortFilesWithZeroBytes)
            {
                bool foundFixableFile = false;
                bool foundANonZeroFile = false;
                for (int i = 0; i < file.Value.ChildCount; i++)
                {
                    RvFile child = file.Value.Child(i);
                    if (child.Size != 0)
                        foundANonZeroFile = true;
                    if (child.Size == 0)
                        continue;
                    RepStatus fixStatus = child.RepStatus;
                    if (fixStatus != RepStatus.InToSort && fixStatus != RepStatus.Corrupt)
                    {
                        foundFixableFile = true;
                        break;
                    }
                }
                if (foundFixableFile || !foundANonZeroFile)
                    continue;

                for (int i = 0; i < file.Value.ChildCount; i++)
                {
                    RvFile child = file.Value.Child(i);
                    if (child.Size != 0)
                        continue;
                    child.RepStatus = RepStatus.InToSort;
                }
            }
        }
    }
}
