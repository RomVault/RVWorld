using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace StorageList
{
    /// <summary>
    /// Parallel merge-sort helper for arrays and lists, with optional pre-filtering.
    /// </summary>
    public class FastArraySort
    {
        public delegate bool FindOn<T>(T fileGroup);
        public delegate int SortOn<T>(T fileGroup1, T fileGroup2);

        /// <summary>
        /// Filters <paramref name="arrToSort"/> using <paramref name="find"/> and sorts the remaining items.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="arrToSort">Source array.</param>
        /// <param name="find">Predicate selecting which elements to include.</param>
        /// <param name="sort">Comparison function.</param>
        /// <param name="outArray">Filtered and sorted output array.</param>
        public static void SortWithFilter<T>(T[] arrToSort, FindOn<T> find, SortOn<T> sort, out T[] outArray)
        {
            List<T> outList = new List<T>();
            foreach (T fm in arrToSort)
            {
                if (find(fm))
                    outList.Add(fm);
            }

            outArray = outList.ToArray();

            SortArray(0, outArray.Length, outArray, sort, 0);
        }

        /// <summary>
        /// Returns a sorted copy of the provided array.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="arrToSort">Source array.</param>
        /// <param name="sortFunction">Comparison function.</param>
        /// <returns>Sorted copy.</returns>
        public static T[] SortArray<T>(T[] arrToSort, SortOn<T> sortFunction)
        {
            T[] sortedCRC = new T[arrToSort.Length];
            arrToSort.CopyTo(sortedCRC, 0);
            SortArray(0, sortedCRC.Length, sortedCRC, sortFunction, 0);
            return sortedCRC;
        }

        /// <summary>
        /// Returns a sorted copy of the provided list.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="arrToSort">Source list.</param>
        /// <param name="sortFunction">Comparison function.</param>
        /// <returns>Sorted list.</returns>
        public static List<T> SortList<T>(List<T> arrToSort, SortOn<T> sortFunction)
        {
            T[] sortedCRC = arrToSort.ToArray();
            arrToSort.CopyTo(sortedCRC, 0);
            SortArray(0, sortedCRC.Length, sortedCRC, sortFunction, 0);
            return sortedCRC.ToList();
        }


        private static void SortArray<T>(int intBase, int intTop, T[] arrToSort, SortOn<T> sortFunction, int depth)
        {
            int sortSize = intTop - intBase;
            if (sortSize <= 1) return;

            // if just 2 tests 
            if (sortSize == 2)
            {
                // compare the 2 files
                T t0 = arrToSort[intBase];
                T t1 = arrToSort[intBase + 1];
                if (sortFunction(t0, t1) < 1)
                    return;
                // swap them
                arrToSort[intBase] = t1;
                arrToSort[intBase + 1] = t0;
                return;
            }

            int intMiddle = (intTop + intBase) / 2;

            if (depth < 2)
            {
                Thread t0 = new Thread(() => SortArray(intBase, intMiddle, arrToSort, sortFunction, depth + 1));
                Thread t1 = new Thread(() => SortArray(intMiddle, intTop, arrToSort, sortFunction, depth + 1));
                t0.Start();
                t1.Start();
                t0.Join();
                t1.Join();
            }
            else
            {
                SortArray(intBase, intMiddle, arrToSort, sortFunction, depth + 1);
                SortArray(intMiddle, intTop, arrToSort, sortFunction, depth + 1);
            }

            int intBottomSize = intMiddle - intBase;
            int intTopSize = intTop - intMiddle;

            T[] arrBottom = new T[intBottomSize];
            T[] arrTop = new T[intTopSize];

            if (depth == 0)
            {
                Thread t0 = new Thread(() => Array.Copy(arrToSort, intBase, arrBottom, 0, intBottomSize));
                Thread t1 = new Thread(() => Array.Copy(arrToSort, intMiddle, arrTop, 0, intTopSize));
                t0.Start();
                t1.Start();
                t0.Join();
                t1.Join();
            }
            else
            {
                Array.Copy(arrToSort, intBase, arrBottom, 0, intBottomSize);
                Array.Copy(arrToSort, intMiddle, arrTop, 0, intTopSize);
            }

            int intBottomCount = 0;
            int intTopCount = 0;
            int intCount = intBase;

            while (intBottomCount < intBottomSize && intTopCount < intTopSize)
            {
                if (sortFunction(arrBottom[intBottomCount], arrTop[intTopCount]) < 1)
                {
                    arrToSort[intCount++] = arrBottom[intBottomCount++];
                }
                else
                {
                    arrToSort[intCount++] = arrTop[intTopCount++];
                }
            }

            while (intBottomCount < intBottomSize)
            {
                arrToSort[intCount++] = arrBottom[intBottomCount++];
            }

            while (intTopCount < intTopSize)
            {
                arrToSort[intCount++] = arrTop[intTopCount++];
            }
        }
    }
}
