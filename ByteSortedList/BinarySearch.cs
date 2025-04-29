using System.Collections.Generic;

namespace RomVaultCore.Utils
{
    public delegate int compareFunc<T>(T val1, T val2);

    public static class BinarySearch
    {
        public static int ListSearch<T>(List<T> list, T lName, compareFunc<T> CompareName, out int index)
        {
            int intBottom = 0;
            int intTop = list.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
            {
                intMid = (intBottom + intTop) / 2;

                intRes = CompareName(lName, list[intMid]);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while (index > 0 && intRes1 == 0)
                {
                    intRes1 = CompareName(lName, list[index - 1]);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;
        }


    }
}
