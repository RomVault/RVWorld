using System;
using System.Collections.Generic;
using DATReader.DatStore;
using DATReader.Utils;
using RVUtils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void DatSetMakeMergeSet(DatDir tDat, bool mergeWithGameName = true)
        {
            // look for merged roms, check if a rom exists in a parent set where the Name,Size and CRC all match.

            DatDir[] sortThis = new DatDir[tDat.Count];
            for (int g = 0; g < tDat.Count; g++)
                sortThis[g] = (DatDir)tDat.ChildSorted(g);


            Array.Sort(sortThis, new IAlphanumComparator());

            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = sortThis[g];

                if (mGame.DGame == null)
                {
                    DatSetMakeMergeSet(mGame, mergeWithGameName);
                    continue;
                }

                // find all parents of this game
                List<DatDir> lstParentGames = new List<DatDir>();
                DatFindParentSets.FindParentSet(mGame, tDat, true, ref lstParentGames);

                // if no parents are found then just set all children as kept
                if (lstParentGames.Count == 0)
                    continue;

                List<DatDir> pGames = new List<DatDir>();
                List<DatDir> pBios = new List<DatDir>();
                foreach (DatDir dd in lstParentGames)
                {
                    if (dd.DGame.IsBios?.ToLower() == "yes")
                        pBios.Add(dd);
                    else
                        pGames.Add(dd);
                }

                DatBase[] mGameTest = mGame.ToArray();
                List<DatBase> mGameKeep = new List<DatBase>();

                foreach (DatBase tGame in mGameTest)
                {
                    DatFile dr0 = (DatFile)tGame;
                    if (dr0.Status == "nodump")
                    {
                        mGameKeep.Add(tGame);
                        continue;
                    }

                    // first remove any file that is in a parent BIOS set
                    bool found = FindRomInParent(dr0, pBios);

                    if (!found)
                        mGameKeep.Add(tGame);
                }

                mGame.ChildrenClear();

                if (pGames.Count == 0)
                {
                    foreach (DatBase tGame in mGameKeep)
                        mGame.ChildAdd(tGame);

                    continue;
                }

                DatDir romOfTopParent = pGames[pGames.Count - 1];

                foreach (DatBase tGame in mGameKeep)
                {
                    if (mergeWithGameName && !((DatFile)tGame).isDisk)
                        tGame.Name = mGame.Name + "/" + tGame.Name;
                    romOfTopParent.ChildAdd(tGame);
                }
            }
        }

        private static bool FindRomInParent(DatFile dr0, List<DatDir> lstParentGames)
        {
            foreach (DatDir romofGame in lstParentGames)
            {
                for (int r1 = 0; r1 < romofGame.Count; r1++)
                {
                    DatFile dr1 = (DatFile)romofGame[r1];
                    // size/checksum compare, so name does not need to match
                    // if (!string.Equals(mGame[r].Name, romofGame[r1].Name, StringComparison.OrdinalIgnoreCase))
                    // {
                    //     continue;
                    // }

                    ulong? size0 = dr0.Size;
                    ulong? size1 = dr1.Size;
                    if ((size0 != null) && (size1 != null) && (size0 != size1))
                    {
                        continue;
                    }

                    byte[] crc0 = dr0.CRC;
                    byte[] crc1 = dr1.CRC;
                    if ((crc0 != null) && (crc1 != null) && !ByteUtils.ByteArrEqualsQuick(crc0, crc1))
                    {
                        continue;
                    }

                    byte[] sha0 = dr0.SHA1;
                    byte[] sha1 = dr1.SHA1;
                    if ((sha0 != null) && (sha1 != null) && !ByteUtils.ByteArrEqualsQuick(sha0, sha1))
                    {
                        continue;
                    }

                    byte[] sha256_0 = dr0.SHA256;
                    byte[] sha256_1 = dr1.SHA256;
                    if ((sha256_0 != null) && (sha256_1 != null) && !ByteUtils.ByteArrEqualsQuick(sha256_0, sha256_1))
                    {
                        continue;
                    }

                    byte[] md50 = dr0.MD5;
                    byte[] md51 = dr1.MD5;
                    if ((md50 != null) && (md51 != null) && !ByteUtils.ByteArrEqualsQuick(md50, md51))
                    {
                        continue;
                    }

                    if (dr0.isDisk != dr1.isDisk)
                    {
                        continue;
                    }

                    // not needed as we are now checking for nodumps at the top of this code
                    // don't merge if only one of the ROM is nodump
                    //if (dr1.Status == "nodump" != (dr0.Status == "nodump"))
                    //{
                    //    continue;
                    //}

                    return true;
                }
            }
            return false;
        }
    }
}
