/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2013                                *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using DATReader.Utils;
using RomVaultCore.Storage.Dat;
using RVUtils;

namespace RomVaultCore.RvDB
{
    public enum EFile
    {
        Keep,
        Delete
    }

    public static class DBHelper
    {
        private static readonly byte[] ZeroByteMD5;
        private static readonly byte[] ZeroByteSHA1;
        private static readonly byte[] ZeroByteCRC;

        static DBHelper()
        {
            ZeroByteMD5 = VarFix.CleanMD5SHA1("d41d8cd98f00b204e9800998ecf8427e", 32);
            ZeroByteSHA1 = VarFix.CleanMD5SHA1("da39a3ee5e6b4b0d3255bfef95601890afd80709", 40);
            ZeroByteCRC = VarFix.CleanMD5SHA1("00000000", 8);
        }

        public static void GetSelectedDirListStart(ref List<RvFile> lstDir, RvFile thisDir)
        {
            if (thisDir == null)
            {
                thisDir = DB.DirRoot;
            }
            else if (thisDir.Tree != null)
            {
                lstDir.Add(thisDir);
            }


            GetSelectedDirList(ref lstDir, thisDir);
        }


        public static void GetSelectedDirList(ref List<RvFile> lstDir, RvFile thisDir)
        {
            for (int i = 0; i < thisDir.ChildCount; i++)
            {
                if (thisDir.DatStatus != DatStatus.InDatCollect)
                {
                    continue;
                }
                RvFile tDir = thisDir.Child(i);
                if (!tDir.IsDirectory)
                {
                    continue;
                }
                if (tDir.Tree == null)
                {
                    continue;
                }
                if (tDir.Tree.Checked != RvTreeRow.TreeSelect.UnSelected)
                {
                    lstDir.Add(tDir);
                }

                GetSelectedDirList(ref lstDir, tDir);
            }
        }



        public static int DatCompare(RvDat var1, RvDat var2)
        {
            //TODO check this
            int retv = RVSorters.CompareDatName(var1, var2);
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.TimeStamp.CompareTo(var2.TimeStamp));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.MultiDatsInDirectory).CompareTo(var2.Flag(DatFlags.MultiDatsInDirectory)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.MultiDatOverride).CompareTo(var2.Flag(DatFlags.MultiDatOverride)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.UseDescriptionAsDirName).CompareTo(var2.Flag(DatFlags.UseDescriptionAsDirName)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.SingleArchive).CompareTo(var2.Flag(DatFlags.SingleArchive)));
            if (retv != 0)
            {
                return retv;
            }
            retv = Math.Sign(var1.SubDirType.CompareTo(var2.SubDirType));
            if (retv != 0)
            {
                return retv;
            }

            return 0;
        }


        public static int DatCompare(RvDat var1, DatImportDat var2)
        {
            //TODO check this
            int retv = RVSorters.CompareDatName(var1, var2);
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.TimeStamp.CompareTo(var2.TimeStamp));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.MultiDatsInDirectory).CompareTo(var2.Flag(DatFlags.MultiDatsInDirectory)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.MultiDatOverride).CompareTo(var2.Flag(DatFlags.MultiDatOverride)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.UseDescriptionAsDirName).CompareTo(var2.Flag(DatFlags.UseDescriptionAsDirName)));
            if (retv != 0)
            {
                return retv;
            }

            retv = Math.Sign(var1.Flag(DatFlags.SingleArchive).CompareTo(var2.Flag(DatFlags.SingleArchive)));
            if (retv != 0)
            {
                return retv;
            }
            retv = Math.Sign(var1.SubDirType.CompareTo(var2.SubDirType));
            if (retv != 0)
            {
                return retv;
            }

            return 0;
        }


        // find fix files, if the gotFile has been fully scanned check the SHA1/MD5, if not then just return true as the CRC/Size is all we have to go on.
        // this means that if the gotfile has not been fully scanned this will return true even with the source and destination SHA1/MD5 possibly different.
        public static bool CheckIfMissingFileCanBeFixedByGotFile(RvFile missingFile, RvFile gotFile)
        {
            if (IsChdContainerMoveMatch(missingFile, gotFile) || IsDiscChdNameMatch(missingFile, gotFile))
                return true;

            // should probably be checking that the header type also match
            if (missingFile.HeaderFileType != HeaderFileType.Nothing && gotFile.HeaderFileType != HeaderFileType.Nothing)
            {
                if (missingFile.HeaderFileType != gotFile.HeaderFileType)
                    return false;
            }
            if (missingFile.HeaderFileTypeRequired && (gotFile.HeaderFileType == HeaderFileType.Nothing || !gotFile.FileStatusIs(FileStatus.HeaderFileTypeFromHeader)))
                return false;


            if (missingFile.FileStatusIs(FileStatus.SHA1FromDAT) && gotFile.FileStatusIs(FileStatus.SHA1Verified) && !ByteUtils.ByteArrEquals(missingFile.SHA1, gotFile.SHA1))
            {
                if (missingFile.FileStatusIs(FileStatus.SHA1FromDAT) && gotFile.FileStatusIs(FileStatus.AltSHA1Verified) && !ByteUtils.ByteArrEquals(missingFile.SHA1, gotFile.AltSHA1))
                    return false;
            }

            if (missingFile.FileStatusIs(FileStatus.MD5FromDAT) && gotFile.FileStatusIs(FileStatus.MD5Verified) && !ByteUtils.ByteArrEquals(missingFile.MD5, gotFile.MD5))
            {
                if (missingFile.FileStatusIs(FileStatus.MD5FromDAT) && gotFile.FileStatusIs(FileStatus.AltMD5Verified) && !ByteUtils.ByteArrEquals(missingFile.MD5, gotFile.AltMD5))
                    return false;
            }

            return true;
        }

        private static bool IsChdContainerMoveMatch(RvFile missingFile, RvFile gotFile)
        {
            if (missingFile == null || gotFile == null || gotFile.GotStatus != GotStatus.Got)
                return false;

            bool missingIsChd = missingFile.FileType == FileType.CHD &&
                                missingFile.Name?.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) == true;
            bool gotIsChd = (gotFile.FileType == FileType.CHD || gotFile.IsFile) &&
                            gotFile.Name?.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) == true;

            if (!missingIsChd || !gotIsChd)
                return false;
            if (string.Equals(missingFile.Name, gotFile.Name, StringComparison.OrdinalIgnoreCase))
                return true;
            return HasExactChdPayloadIdentity(missingFile, gotFile);
        }

        private static bool HasExactChdPayloadIdentity(RvFile expectedChd, RvFile candidateChd)
        {
            if (expectedChd.ChildCount == 0 || candidateChd.ChildCount == 0)
                return false;
            string expectedFamily = InferChdMemberFamily(expectedChd);
            string candidateFamily = InferChdMemberFamily(candidateChd);
            if (string.IsNullOrWhiteSpace(expectedFamily) ||
                !string.Equals(expectedFamily, candidateFamily, StringComparison.OrdinalIgnoreCase))
                return false;

            List<RvFile> expectedPayloads = GetChdPayloadMembers(expectedChd, descriptors: false);
            List<RvFile> candidatePayloads = GetChdPayloadMembers(candidateChd, descriptors: false);
            if (!ExactChdPayloadListsMatch(expectedPayloads, candidatePayloads))
                return false;

            List<RvFile> expectedDescriptors = GetChdPayloadMembers(expectedChd, descriptors: true);
            for (int i = 0; i < expectedDescriptors.Count; i++)
            {
                RvFile descriptor = expectedDescriptors[i];
                if (!HasStrongHash(descriptor))
                    continue;
                List<RvFile> candidates = GetChdPayloadMembers(candidateChd, descriptors: true);
                int matches = 0;
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (HasStrongHash(candidates[j]) && StrongPayloadMatch(descriptor, candidates[j]))
                        matches++;
                }
                if (matches != 1)
                    return false;
            }
            return true;
        }

        private static bool ExactChdPayloadListsMatch(List<RvFile> expectedPayloads, List<RvFile> candidatePayloads)
        {
            if (expectedPayloads == null || candidatePayloads == null || expectedPayloads.Count == 0 || expectedPayloads.Count != candidatePayloads.Count)
                return false;
            bool[] used = new bool[candidatePayloads.Count];
            for (int i = 0; i < expectedPayloads.Count; i++)
            {
                RvFile expected = expectedPayloads[i];
                if (!HasStrongHash(expected))
                    return false;
                int match = -1;
                for (int j = 0; j < candidatePayloads.Count; j++)
                {
                    if (used[j] || !HasStrongHash(candidatePayloads[j]) || !StrongPayloadMatch(expected, candidatePayloads[j]))
                        continue;
                    if (match >= 0)
                        return false;
                    match = j;
                }
                if (match < 0)
                    return false;
                used[match] = true;
            }
            return true;
        }

        private static List<RvFile> GetChdPayloadMembers(RvFile chd, bool descriptors)
        {
            List<RvFile> members = new List<RvFile>();
            for (int i = 0; i < chd.ChildCount; i++)
            {
                RvFile child = chd.Child(i);
                if (child == null || !child.IsFile)
                    continue;
                string extension = Path.GetExtension(child.Name ?? "").ToLowerInvariant();
                bool isDescriptor = extension == ".cue" || extension == ".gdi" || extension == ".toc";
                if (isDescriptor == descriptors)
                    members.Add(child);
            }
            return members;
        }

        private static bool HasStrongHash(RvFile file) =>
            (file?.SHA1 != null && file.SHA1.Length > 0) || (file?.MD5 != null && file.MD5.Length > 0);

        private static bool StrongPayloadMatch(RvFile expected, RvFile candidate)
        {
            if (expected.Size.HasValue && candidate.Size.HasValue && expected.Size.Value != candidate.Size.Value)
                return false;
            if (expected.SHA1 != null && expected.SHA1.Length > 0)
                return candidate.SHA1 != null && ByteUtils.ByteArrEquals(expected.SHA1, candidate.SHA1);
            return expected.MD5 != null && expected.MD5.Length > 0 &&
                   candidate.MD5 != null && ByteUtils.ByteArrEquals(expected.MD5, candidate.MD5);
        }

        private static string InferChdMemberFamily(RvFile chd)
        {
            bool hasCue = false;
            bool hasGdi = false;
            int payloadCount = 0;
            string singleExtension = "";
            for (int i = 0; i < chd.ChildCount; i++)
            {
                RvFile child = chd.Child(i);
                if (child == null || !child.IsFile)
                    continue;
                string extension = Path.GetExtension(child.Name ?? "").ToLowerInvariant();
                if (extension == ".cue") { hasCue = true; continue; }
                if (extension == ".gdi") { hasGdi = true; continue; }
                if (extension == ".toc") continue;
                payloadCount++;
                singleExtension = extension;
            }
            if (hasGdi) return "gdi";
            if (hasCue) return "cd";
            if (payloadCount != 1) return null;
            if (singleExtension == ".iso") return "dvd";
            if (singleExtension == ".avi") return "laserdisc";
            if (singleExtension == ".img" || singleExtension == ".hdd" || singleExtension == ".hd") return "hdd";
            if (singleExtension == ".raw") return "raw";
            return null;
        }

        public static bool RunChdContentIdentitySelfTest(out string error)
        {
            error = "";
            try
            {
                byte[] hash = new byte[20];
                for (int i = 0; i < hash.Length; i++) hash[i] = (byte)(i * 7 + 3);
                List<RvFile> expected = new List<RvFile> { new RvFile(FileType.FileCHD) { Name = "expected.iso", Size = 4096, SHA1 = (byte[])hash.Clone() } };
                List<RvFile> candidate = new List<RvFile> { new RvFile(FileType.FileCHD) { Name = "image.iso", Size = 4096, SHA1 = (byte[])hash.Clone() } };
                if (!ExactChdPayloadListsMatch(expected, candidate))
                {
                    error = "Complete content-identical CHDs did not match across filenames.";
                    return false;
                }
                candidate[0].SHA1[0] ^= 0xff;
                if (ExactChdPayloadListsMatch(expected, candidate))
                {
                    error = "A CHD payload hash mismatch was accepted.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsChdCreationAllowedForSet(RvFile missingChdFile)
        {
            return IsChdCreationAllowedForSet(missingChdFile, out _);
        }

        public static bool IsChdCreationAllowedForSet(RvFile missingChdFile, out string reason)
        {
            reason = "";
            if (missingChdFile == null)
            {
                reason = "Missing CHD file is not valid.";
                return false;
            }

            bool isChdFile = missingChdFile.IsFile &&
                             missingChdFile.Name?.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) == true;
            bool isChdContainer = missingChdFile.FileType == FileType.CHD &&
                                  missingChdFile.Name?.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) == true;
            if (!isChdFile && !isChdContainer)
            {
                reason = "Missing CHD file is not valid.";
                return false;
            }

            RvFile setRoot = isChdFile ? missingChdFile.Parent : missingChdFile;
            if (setRoot == null)
            {
                reason = "Missing CHD file has no parent directory.";
                return false;
            }

            for (int i = 0; i < setRoot.ChildCount; i++)
            {
                RvFile child = setRoot.Child(i);
                if (child == null || child == missingChdFile)
                    continue;

                if (child.MIAStatus != MIAStatus.None)
                {
                    reason = $"Set contains MIA entry: {child.Name}";
                    return false;
                }
            }

            return true;
        }

        private static bool IsDiscChdNameMatch(RvFile missingFile, RvFile gotFile)
        {
            if (missingFile == null || gotFile == null || !missingFile.IsFile || !gotFile.IsFile)
                return false;
            if (missingFile.Name?.EndsWith(".chd", StringComparison.OrdinalIgnoreCase) != true ||
                gotFile.GotStatus != GotStatus.Got)
                return false;

            string extension = Path.GetExtension(gotFile.Name)?.ToLowerInvariant();
            if (extension != ".cue" && extension != ".gdi" && extension != ".iso" &&
                extension != ".raw" && extension != ".img" && extension != ".hdd" &&
                extension != ".hd" && extension != ".avi")
                return false;

            string missingKey = Path.GetFileNameWithoutExtension(missingFile.Name);
            string gotKey = Path.GetFileNameWithoutExtension(gotFile.Name);
            return !string.IsNullOrWhiteSpace(missingKey) &&
                   (string.Equals(missingKey, gotKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(missingKey, Path.GetFileName(gotFile.Name), StringComparison.OrdinalIgnoreCase)) &&
                   IsChdCreationAllowedForSet(missingFile, out _);
        }


        public static bool CheckIfGotfileAndMatchingFileAreFullMatches(RvFile gotFile, RvFile matchingFile)
        {
            if (gotFile.FileStatusIs(FileStatus.SHA1Verified) && matchingFile.FileStatusIs(FileStatus.SHA1Verified) && !ByteUtils.ByteArrEquals(gotFile.SHA1, matchingFile.SHA1))
                return false;
            if (gotFile.FileStatusIs(FileStatus.MD5Verified) && matchingFile.FileStatusIs(FileStatus.MD5Verified) && !ByteUtils.ByteArrEquals(gotFile.MD5, matchingFile.MD5))
                return false;

            return true;
        }




        public static bool IsZeroLengthFile(RvFile tFile)
        {
            bool foundOneMatching = false;
            if (tFile.MD5 != null)
            {
                if (!ByteUtils.ByteArrEquals(tFile.MD5, ZeroByteMD5))
                {
                    return false;
                }
                foundOneMatching = true;
            }

            if (tFile.SHA1 != null)
            {
                if (!ByteUtils.ByteArrEquals(tFile.SHA1, ZeroByteSHA1))
                {
                    return false;
                }
                foundOneMatching = true;
            }

            if (tFile.CRC != null)
            {
                if (!ByteUtils.ByteArrEquals(tFile.CRC, ZeroByteCRC))
                {
                    return false;
                }
                foundOneMatching = true;
            }

            if (tFile.Size != null)
            {
                if (tFile.Size != 0)
                {
                    return false;
                }
                foundOneMatching = true;
            }

            // if at least one hash,size matched. & nothing failed to match.
            if (foundOneMatching)
                return true;

            // hashes and size are all null
            // see if we have a directory
            return (tFile.Name.Length > 1 && tFile.Name.Substring(tFile.Name.Length - 1, 1) == "/");
        }

        public static bool RomFromSameGame(RvFile a, RvFile b)
        {
            if (a.Parent == null)
            {
                return false;
            }
            if (b.Parent == null)
            {
                return false;
            }

            return a.Parent == b.Parent;
        }



    }
}
