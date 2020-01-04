using System.Collections.Generic;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void DatSetMakeSplitSet(DatDir tDat)
        {
            // look for merged roms, check if a rom exists in a parent set where the Name,Size and CRC all match.

            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir)tDat.Child(g);

                if (mGame.DGame == null)
                {
                    DatSetMakeSplitSet(mGame);
                }
                else
                {
                    // find all parents of this game
                    List<DatDir> lstParentGames = new List<DatDir>();
                    DatFindParentSets.FindParentSet(mGame, tDat, true, ref lstParentGames);

                    // if no parents are found then just set all children as kept
                    if (lstParentGames.Count == 0)
                    {
                        for (int r = 0; r < mGame.ChildCount; r++)
                        {
                            if (mGame.Child(r) is DatFile dfGame)
                                RomCheckCollect(dfGame, false);
                        }
                    }
                    else
                    {
                        for (int r0 = 0; r0 < mGame.ChildCount; r0++)
                        {
                            DatFile dr0 = (DatFile)mGame.Child(r0);
                            if (dr0.Status == "nodump")
                            {
                                RomCheckCollect(dr0, false);
                                continue;
                            }

                            bool found = false;
                            foreach (DatDir romofGame in lstParentGames)
                            {
                                for (int r1 = 0; r1 < romofGame.ChildCount; r1++)
                                {
                                    DatFile dr1 = (DatFile)romofGame.Child(r1);
                                    // size/checksum compare, so name does not need to match
                                    // if (!string.Equals(mGame.Child(r).Name, romofGame.Child(r1).Name, StringComparison.OrdinalIgnoreCase))
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
                                    if ((crc0 != null) && (crc1 != null) && !ArrByte.bCompare(crc0, crc1))
                                    {
                                        continue;
                                    }

                                    byte[] sha0 = dr0.SHA1;
                                    byte[] sha1 = dr1.SHA1;
                                    if ((sha0 != null) && (sha1 != null) && !ArrByte.bCompare(sha0, sha1))
                                    {
                                        continue;
                                    }

                                    byte[] md50 = dr0.MD5;
                                    byte[] md51 = dr1.MD5;
                                    if ((md50 != null) && (md51 != null) && !ArrByte.bCompare(md50, md51))
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

                                    found = true;
                                    break;
                                }
                                if (found)
                                {
                                    break;
                                }
                            }

                            RomCheckCollect((DatFile)mGame.Child(r0), found);
                        }
                    }
                }
            }
        }


        /*
         * In the mame Dat:
         * status="nodump" has a size but no CRC
         * status="baddump" has a size and crc
         */


        private static void RomCheckCollect(DatFile tRom, bool merge)
        {
            if (merge)
            {
                if (string.IsNullOrEmpty(tRom.Merge))
                {
                    tRom.Merge = "(Auto Merged)";
                }
                tRom.DatStatus = DatFileStatus.InDatMerged;
                return;
            }

            if (!string.IsNullOrEmpty(tRom.Merge))
            {
                tRom.Merge = "(No-Merge) " + tRom.Merge;
            }

            if (tRom.Status == "nodump")
            {
                tRom.DatStatus = DatFileStatus.InDatBad;
                return;
            }

            if (ArrByte.bCompare(tRom.CRC, new byte[] { 0, 0, 0, 0 }) && (tRom.Size == 0))
            {
                tRom.DatStatus = DatFileStatus.InDatCollect;
                return;
            }

            tRom.DatStatus = DatFileStatus.InDatCollect;
        }


    }
}
