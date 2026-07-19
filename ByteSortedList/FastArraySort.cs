using System;
using System.Collections.Generic;

namespace StorageList
{
    public delegate bool FindOn<T>(T fileGroup);
    public delegate int SortOn<T>(T fileGroup1, T fileGroup2);

    public class FastArraySort
    {
        public static void SortWithFilter<T>(T[] arrToSort, FindOn<T> find, SortOn<T> sort, out T[] outArray)
        {
            bool[] matches = new bool[arrToSort.Length];
            int matchCount = 0;
            for (int i = 0; i < arrToSort.Length; i++)
            {
                bool match = find(arrToSort[i]);
                matches[i] = match;
                if (match)
                    matchCount++;
            }

            outArray = new T[matchCount];
            int outputIndex = 0;
            for (int i = 0; i < arrToSort.Length; i++)
            {
                if (matches[i])
                    outArray[outputIndex++] = arrToSort[i];
            }

            SortArrayInPlace(outArray, sort);
        }
        public static T[] SortArray<T>(T[] arrToSort, SortOn<T> sortFunction)
        {
            T[] sortedCRC = new T[arrToSort.Length];
            arrToSort.CopyTo(sortedCRC, 0);
            SortArrayInPlace(sortedCRC, sortFunction);
            return sortedCRC;
        }


        public static List<T> SortList<T>(List<T> arrToSort, SortOn<T> sortFunction)
        {
            T[] sortedCRC = new T[arrToSort.Count];
            arrToSort.CopyTo(sortedCRC, 0);
            SortArrayInPlace(sortedCRC, sortFunction);
            return new List<T>(sortedCRC);
        }


        private static void SortArrayInPlace<T>(T[] arrToSort, SortOn<T> sortFunction)
        {
            if (arrToSort.Length <= 1)
                return;

            T[] scratch = new T[arrToSort.Length];
            SortArray(0, arrToSort.Length, arrToSort, scratch, sortFunction);
        }

        private static void SortArray<T>(int intBase, int intTop, T[] arrToSort, T[] scratch, SortOn<T> sortFunction)
        {
            int sortSize = intTop - intBase;
            if (sortSize <= 1) return;

            int intMiddle = (intTop + intBase) / 2;
            SortArray(intBase, intMiddle, arrToSort, scratch, sortFunction);
            SortArray(intMiddle, intTop, arrToSort, scratch, sortFunction);

            if (sortFunction(arrToSort[intMiddle - 1], arrToSort[intMiddle]) <= 0)
                return;

            Array.Copy(arrToSort, intBase, scratch, intBase, sortSize);
            int intBottomCount = intBase;
            int intTopCount = intMiddle;
            int intCount = intBase;

            while (intBottomCount < intMiddle && intTopCount < intTop)
            {
                if (sortFunction(scratch[intBottomCount], scratch[intTopCount]) <= 0)
                {
                    arrToSort[intCount++] = scratch[intBottomCount++];
                }
                else
                {
                    arrToSort[intCount++] = scratch[intTopCount++];
                }
            }

            while (intBottomCount < intMiddle)
            {
                arrToSort[intCount++] = scratch[intBottomCount++];
            }

            while (intTopCount < intTop)
            {
                arrToSort[intCount++] = scratch[intTopCount++];
            }
        }
    }
}
