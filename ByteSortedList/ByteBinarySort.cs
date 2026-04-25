using System.Collections.Generic;

namespace StorageList
{
    public class ByteBinarySort<T>
    {

        public delegate byte getByte(T fileGound);

        private T[][] baseArray;

        private getByte bgFunc;
        private SortOn<T> sOn;

        public ByteBinarySort(getByte bg, SortOn<T> s, List<T> arrToSort)
        {
            bgFunc = bg;
            sOn = s;
            List<T> tmpOut = FastArraySort.SortList(arrToSort, sOn);

            List<T>[] tmpList = new List<T>[256];
            for (int i = 0; i < 256; i++)
                tmpList[i] = new List<T>();

            foreach (T toSort in tmpOut)
                tmpList[bgFunc(toSort)].Add(toSort);

            baseArray = new T[256][];
            for (int i = 0; i < 256; i++)
            {
                if (tmpList[i] == null)
                    continue;
                baseArray[i] = tmpList[i].ToArray();
            }


        }

        public int ArrSearch(T var, out T[] arrB, out int index)
        {
            index = 0;
            byte bArray = bgFunc(var);
            arrB = baseArray[bArray];
            if (arrB == null)
                return -1;

            return BinarySearch.ArraySearch(baseArray[bArray], var, sOn, out index);
        }
    }
}
