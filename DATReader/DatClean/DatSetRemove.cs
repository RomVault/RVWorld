﻿using System;
using Compress;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void RemoveDupes(DatDir tDat, bool testName = true, bool testWithMergeName = false)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                {
                    RemoveDupes(mGame, testName);
                }
                else
                {

                    bool found = true;
                    while (found)
                    {
                        found = false;

                        for (int r = 0; r < mGame.Count; r++)
                        {
                            DatFile df0 = (DatFile)mGame[r];
                            for (int t = r + 1; t < mGame.Count; t++)
                            {
                                DatFile df1 = (DatFile)mGame[t];

                                if (testName && df0.Name != df1.Name)
                                    continue;
                                bool hasCRC = df0.CRC != null && df1.CRC != null;
                                if (hasCRC && !ArrByte.bCompare(df0.CRC, df1.CRC))
                                    continue;
                                bool hasSHA1 = df0.SHA1 != null && df1.SHA1 != null;
                                if (hasSHA1 && !ArrByte.bCompare(df0.SHA1, df1.SHA1))
                                    continue;
                                bool hasSHA256 = df0.SHA256 != null && df1.SHA256 != null;
                                if (hasSHA256 && !ArrByte.bCompare(df0.SHA256, df1.SHA256))
                                    continue;
                                bool hasMD5 = df0.MD5 != null && df1.MD5 != null;
                                if (hasMD5 && !ArrByte.bCompare(df0.MD5, df1.MD5))
                                    continue;
                                if (!hasCRC && !hasSHA1 && !hasMD5)
                                    continue;

                                found = true;

                                string name0 = df0.Name;
                                string name1 = df1.Name;

                                bool nS0 = name0.Contains("/");
                                bool ns1 = name1.Contains("/");

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
                                    string s0 = name0.Substring(0, name0.IndexOf("/", StringComparison.Ordinal));
                                    string s1 = name1.Substring(0, name1.IndexOf("/", StringComparison.Ordinal));
                                    if (s0 != s1)
                                        mGame.ChildRemove(df1);
                                    else
                                    {
                                        int res = AlphanumComparatorFast.Compare(name0, name1);
                                        mGame.ChildRemove(res >= 0 ? df0 : df1);
                                    }
                                }
                                else if ((name0 == name1) || (testWithMergeName && (name0 == df1.Merge)))
                                {
                                    mGame.ChildRemove(df1);
                                }
                                else
                                {
                                    found = false;
                                    continue;
                                }
                                r = mGame.Count;
                                t = mGame.Count;

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
                return (dFile.DatStatus == DatStatus.InDatCollect || dFile.DatStatus == DatStatus.InDatNoDump);
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
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir mGame))
                    continue;

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

            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                {
                    RemoveCHD(mGame);
                }
                else
                {
                    for (int r = 0; r < mGame.Count; r++)
                    {
                        DatFile df1 = (DatFile)mGame[r];
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
            for (int g = 0; g < tDat.Count; g++)
            {
                DatDir mGame = (DatDir)tDat[g];

                if (mGame.DGame == null)
                {
                    RemoveNonCHD(mGame);
                }
                else
                {
                    for (int r = 0; r < mGame.Count; r++)
                    {
                        DatFile df1 = (DatFile)mGame[r];
                        if (df1.isDisk)
                            continue;
                        mGame.ChildRemove(df1);
                        r--;
                    }
                }
            }
        }

        public static void RemoveAllDateTime(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                tDat[g].DateModified = null;
                if (tDat[g] is DatDir mGame)
                {
                    if (mGame.DatStruct == ZipStructure.ZipTDC)
                        continue;
                    RemoveAllDateTime(mGame);
                }
            }
        }

        public static void RemoveUnNeededDirectories(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir mGame))
                    continue;

                if (mGame.FileType == FileType.Dir || (mGame.FileType == FileType.UnSet && mGame.DGame == null))
                    RemoveUnNeededDirectories(mGame);
                else
                {
                    if (mGame.DatStruct == ZipStructure.ZipTDC)
                        continue;
                    RemoveUnNeededDirectoriesFromZip(mGame);
                }
            }
        }

        public static void RemoveUnNeededDirectoriesFromZip(DatDir mGame)
        {
            for (int r = 0; r < mGame.Count; r++)
            {
                DatFile df1 = (DatFile)mGame[r];
                if (df1.Size != 0 || df1.Name.Length == 0 || df1.Name.Substring(df1.Name.Length - 1) != "/")
                    continue;
                bool found = false;
                for (int r1 = 0; r1 < mGame.Count; r1++)
                {
                    if (r == r1)
                        continue;
                    string compName = mGame[r1].Name;
                    if (compName.Length <= df1.Name.Length)
                        continue;

                    if (compName.Substring(0, df1.Name.Length) == df1.Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    continue;

                mGame.ChildRemove(df1);
                r--;
            }
        }

        public static void RemoveFilesNotInGames(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (tDat[g] is DatFile datFile)
                {
                    tDat.ChildRemove(datFile);
                    g--;
                    continue;
                }

                if (!(tDat[g] is DatDir datDir))
                    continue;

                if (datDir.DGame == null)
                    RemoveFilesNotInGames(datDir);
            }
        }

        public static void RemoveEmptyDirectories(DatDir tDat)
        {
            for (int g = 0; g < tDat.Count; g++)
            {
                if (!(tDat[g] is DatDir datDir))
                    continue;

                if (datDir.DGame == null)
                {
                    RemoveEmptyDirectories(datDir);
                }
                else
                {
                    if (datDir.Count == 0)
                    {
                        tDat.ChildRemove(datDir);
                        g--;
                    }
                }
            }
        }
    }
}
