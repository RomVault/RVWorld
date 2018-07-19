using System;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void RemoveDupes(DatDir tDat)
        {
            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir)tDat.Child(g);

                if (mGame.DGame == null)
                {
                    RemoveDupes(mGame);
                }
                else
                {

                    bool found = true;
                    while (found)
                    {
                        found = false;


                        for (int r = 0; r < mGame.ChildCount; r++)
                        {
                            DatFile df0 = (DatFile)mGame.Child(r);
                            for (int t = r + 1; t < mGame.ChildCount; t++)
                            {
                                DatFile df1 = (DatFile)mGame.Child(t);

                                if (!ArrByte.bCompare(df0.CRC, df1.CRC))
                                    continue;

                                found = true;

                                string name0 = df0.Name;
                                string name1 = df1.Name;

                                bool nS0 = name0.Contains("\\");
                                bool ns1 = name1.Contains("\\");

                                if (nS0 && !ns1)
                                {
                                    mGame.ChildRemove(df0);
                                }
                                else if (!nS0 && ns1)
                                {
                                    mGame.ChildRemove(df1);
                                }
                                else if (nS0 && ns1)
                                {
                                    string s0 = name0.Substring(0, name0.IndexOf("\\", StringComparison.Ordinal));
                                    string s1 = name1.Substring(0, name1.IndexOf("\\", StringComparison.Ordinal));
                                    if (s0 != s1)
                                        mGame.ChildRemove(df1);
                                    else
                                    {
                                        int res = AlphanumComparatorFast.Compare(name0, name1);
                                        mGame.ChildRemove(res >= 0 ? df0 : df1);
                                    }
                                }
                                else if (name0 == name1)
                                {
                                    mGame.ChildRemove(df1);
                                }
                                else
                                {
                                    found = false;
                                    continue;
                                }
                                r = mGame.ChildCount;
                                t = mGame.ChildCount;

                            }
                        }
                    }
                }

            }
        }



        public static bool RemoveEmptySets(DatBase inDat)
        {
            if (inDat is DatFile)
            {
                return true;
            }

            DatDir dDir = inDat as DatDir;

            DatBase[] children = dDir?.ToArray();
            if (children == null || children.Length == 0)
                return false;

            dDir.ChildrenClear();

            bool found = false;
            foreach (DatBase child in children)
            {
                bool keep = RemoveEmptySets(child);
                if (keep)
                {
                    found = true;
                    dDir.ChildAdd(child);
                }
            }

            return found;
        }

        public static bool RemoveNotCollected(DatBase inDat)
        {
            if (inDat is DatFile dFile)
            {
                return (dFile.DatStatus == DatFileStatus.InDatCollect || dFile.DatStatus == DatFileStatus.InDatBad);
            }

            DatDir dDir = inDat as DatDir;

            DatBase[] children = dDir?.ToArray();
            if (children == null || children.Length == 0)
                return false;

            dDir.ChildrenClear();

            bool found = false;
            foreach (DatBase child in children)
            {
                bool keep = RemoveNotCollected(child);
                if (keep)
                {
                    found = true;
                    dDir.ChildAdd(child);
                }
            }

            return found;
        }

        public static void RemoveNoDumps(DatDir tDat)
        {
            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir)tDat.Child(g);

                if (mGame.DGame == null)
                {
                    RemoveNoDumps(mGame);
                }
                else
                {
                    DatBase[] tGame = mGame.ToArray();
                    foreach (DatBase t in tGame)
                    {
                        if (((DatFile)t).Status == "nodump")
                        {
                            mGame.ChildRemove(t);
                        }
                    }
                }

            }
        }

        public static void RemoveCHD(DatDir tDat)
        {

            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir) tDat.Child(g);

                if (mGame.DGame == null)
                {
                    RemoveCHD(mGame);
                }
                else
                {
                    for (int r = 0; r < mGame.ChildCount; r++)
                    {
                        DatFile df1 = (DatFile) mGame.Child(r);
                        if (!df1.isDisk)
                            continue;
                        mGame.ChildRemove(df1);
                        r--;
                    }
                }
            }
        }

        public static void RemoveNonCHD(DatDir tDat)
        {

            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir)tDat.Child(g);

                if (mGame.DGame == null)
                {
                    RemoveNonCHD(mGame);
                }
                else
                {
                    for (int r = 0; r < mGame.ChildCount; r++)
                    {
                        DatFile df1 = (DatFile)mGame.Child(r);
                        if (df1.isDisk)
                            continue;
                        mGame.ChildRemove(df1);
                        r--;
                    }
                }
            }
        }


    }
}
