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
                    DatFindParentSets.FindParentSet(mGame, tDat,true, ref lstParentGames);

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
                        for (int r = 0; r < mGame.ChildCount; r++)
                        {
                            if (((DatFile)mGame.Child(r)).Status == "nodump")
                            {
                                RomCheckCollect((DatFile)mGame.Child(r), false);
                                continue;
                            }

                            bool found = false;
                            foreach (DatDir romofGame in lstParentGames)
                            {
                                for (int r1 = 0; r1 < romofGame.ChildCount; r1++)
                                {
                                    // size/checksum compare, so name does not need to match
                                    // if (!string.Equals(mGame.Child(r).Name, romofGame.Child(r1).Name, StringComparison.OrdinalIgnoreCase))
                                    // {
                                    //     continue;
                                    // }

                                    ulong? size0 = ((DatFile)mGame.Child(r)).Size;
                                    ulong? size1 = ((DatFile)romofGame.Child(r1)).Size;
                                    if ((size0 != null) && (size1 != null) && (size0 != size1))
                                    {
                                        continue;
                                    }

                                    byte[] crc0 = ((DatFile)mGame.Child(r)).CRC;
                                    byte[] crc1 = ((DatFile)romofGame.Child(r1)).CRC;
                                    if ((crc0 != null) && (crc1 != null) && !ArrByte.bCompare(crc0, crc1))
                                    {
                                        continue;
                                    }

                                    byte[] sha0 = ((DatFile)mGame.Child(r)).SHA1;
                                    byte[] sha1 = ((DatFile)romofGame.Child(r1)).SHA1;
                                    if ((sha0 != null) && (sha1 != null) && !ArrByte.bCompare(sha0, sha1))
                                    {
                                        continue;
                                    }

                                    byte[] md50 = ((DatFile)mGame.Child(r)).MD5;
                                    byte[] md51 = ((DatFile)romofGame.Child(r1)).MD5;
                                    if ((md50 != null) && (md51 != null) && !ArrByte.bCompare(md50, md51))
                                    {
                                        continue;
                                    }

                                    if (((DatFile)mGame.Child(r)).isDisk != ((DatFile)romofGame.Child(r1)).isDisk)
                                    {
                                        continue;
                                    }

                                    // not needed as we are now checking for nodumps at the top of this code
                                    // don't merge if only one of the ROM is nodump
                                    //if (((DatFile)romofGame.Child(r1)).Status == "nodump" != (((DatFile)mGame.Child(r)).Status == "nodump"))
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

                            RomCheckCollect((DatFile)mGame.Child(r), found);
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
