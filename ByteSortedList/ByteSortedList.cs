using System;
using System.Collections;
using System.Collections.Generic;

namespace ByteSortedList
{
    public class ByteSortedList<tStore, tInput> : IEnumerable
    {
        public delegate byte getByteFunc(tInput val);
        public delegate int compareFunc(tInput val1, tStore val2);
        public delegate tStore newFunc(tInput val);
        public delegate void mergeFunc(tInput val1, tStore val2);
        public delegate bool exactFunc(tInput val1, tStore val2);

        private List<tStore>[] byteArray;
        private getByteFunc _getByteFunc;
        private compareFunc _compareFunc;
        private newFunc _newFunc;
        private mergeFunc _mergeFunc;

        public ByteSortedList(getByteFunc getByteFunc, compareFunc compareFunc, newFunc newFunc, mergeFunc mergeFunc)
        {
            byteArray = new List<tStore>[256];
            for (int i = 0; i < 256; i++)
                byteArray[i] = new List<tStore>();
            _getByteFunc = getByteFunc;
            _compareFunc = compareFunc;
            _newFunc = newFunc;
            _mergeFunc = mergeFunc;
        }

        public tStore Find(tInput value)
        {
            List<tStore> thisList = byteArray[_getByteFunc(value)];
            lock (thisList)
            {
                int found = searchOn(value, thisList, out int index);
                if (found == 0)
                {
                    return thisList[index];
                }
                return default;
            }
        }

        public void AddFind(tInput value)
        {
            List<tStore> thisList = byteArray[_getByteFunc(value)];
            lock (thisList)
            {
                int found = searchOn(value, thisList, out int index);
                if (found == 0)
                {
                    _mergeFunc(value, thisList[index]);
                    return;
                }
                thisList.Insert(index, _newFunc(value));
            }
        }



        public void AddFindWithExact(tInput value, exactFunc exact)
        {
            List<tStore> thisList = byteArray[_getByteFunc(value)];
            lock (thisList)
            {
                int found = searchOn(value, thisList, out int index);
                if (found == 0)
                {
                    int intTop = thisList.Count;
                    while (index < intTop)
                    {
                        tStore thisStore = thisList[index];
                        int intRes = _compareFunc(value, thisStore);
                        if (intRes != 0)
                            break;
                        if (exact(value, thisStore))
                        {
                            _mergeFunc(value, thisStore);
                            return;
                        }
                        index++;
                    }
                }
                thisList.Insert(index, _newFunc(value));
            }
        }

        private int searchOn(tInput searchvalue, List<tStore> searchList, out int index)
        {
            int intBottom = 0;
            int intTop = searchList.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
            {
                intMid = (intBottom + intTop) / 2;

                intRes = _compareFunc(searchvalue, searchList[intMid]);
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
                    intRes1 = _compareFunc(searchvalue, searchList[index - 1]);
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

        public int Count
        {
            get
            {
                int count = 0;
                foreach (List<tStore> v in byteArray)
                    count += v.Count;
                return count;
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (List<tStore> v in byteArray)
                foreach (tStore t in v)
                    yield return t;
        }

        public tStore[] ToArray()
        {
            int CountAll = Count;

            tStore[] outArray = new tStore[CountAll];
            CountAll = 0;
            foreach (List<tStore> v in byteArray)
            {
                tStore[] tStores = v.ToArray();
                Array.Copy(tStores, 0, outArray, CountAll, tStores.Length);
                CountAll += tStores.Length;
            }
            return outArray;
        }
    }
}
