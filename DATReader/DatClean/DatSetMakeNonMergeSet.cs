using System.Collections.Generic;
using DATReader.DatStore;
using DATReader.Utils;

namespace DATReader.DatClean
{
    public static partial class DatClean
    {
        public static void DatSetMakeNonMergeSet(DatDir tDat)
        {
            // look for merged roms, check if a rom exists in a parent set where the Name,Size and CRC all match.

            for (int g = 0; g < tDat.ChildCount; g++)
            {
                DatDir mGame = (DatDir)tDat.Child(g);

                if (mGame.DGame == null)
                {
                    DatSetMakeNonMergeSet(mGame);
                }
                else
                {

                    DatGame dGame = mGame.DGame;

                    if (dGame?.device_ref == null)
                        continue;

                    List<DatDir> devices = new List<DatDir> {mGame};

                    foreach (string device in dGame.device_ref)
                    {
                        AddDevice(device, devices, tDat);
                    }
                    devices.RemoveAt(0);


                    foreach (DatDir device in devices)
                    {
                        for (int i = 0; i < device.ChildCount; i++)
                        {
                            DatFile df0 = (DatFile)device.Child(i);
                            bool crcFound = false;
                            for (int j = 0; j < mGame.ChildCount; j++)
                            {
                                DatFile df1 = (DatFile)mGame.Child(j);
                                if (ArrByte.bCompare(df0.SHA1, df1.SHA1) && df0.Name==df1.Name)
                                {
                                    crcFound = true;
                                    break;
                                }

                            }
                            if (!crcFound)
                                mGame.ChildAdd(device.Child(i));
                        }
                    }
                }
            }
        }

        private static void AddDevice(string device, List<DatDir> devices, DatDir tDat)
        {
            if (tDat.ChildNameSearch(new DatDir(tDat.DatFileType) { Name = device }, out int index) != 0)
                return;
            DatDir devChild = (DatDir)tDat.Child(index);
            if (devChild == null)
                return;

            if (devices.Contains(devChild))
                return;

            devices.Add(devChild);

            List<string> childDev = devChild.DGame?.device_ref;
            if (childDev == null)
                return;

            foreach (string deviceChild in childDev)
            {
                AddDevice(deviceChild, devices, tDat);
            }


        }

        public static void RemoveDevices(DatDir tDat)
        {
            DatBase[] children = tDat.ToArray();

            tDat.ChildrenClear();
            foreach (DatBase child in children)
            {
                DatDir mGame = (DatDir)child;

                if (mGame.DGame == null)
                {
                    RemoveDevices(mGame);
                    tDat.ChildAdd(mGame);
                }
                else
                {
                    DatGame dGame = mGame.DGame;

                    if (dGame != null && dGame.IsDevice == "yes" && dGame.Runnable == "no")
                        continue;

                    tDat.ChildAdd(mGame);
                }
            }
        }
    }

}
