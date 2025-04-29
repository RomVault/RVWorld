using Compress;
using SortMethods;
using System;
using System.Collections.Generic;

namespace DATReader.DatStore
{
    // if DatFileType is UnSet then we store the children files in List<DatBase> unsorted. (The order they are added from the DAT.)
    // problem with this is that searching for a child by name would have to be a linear search. (Which gets very slow.)
    // So for this reason UnSet file lists also use the _childrenNameIndex to store a sorted index.
    // So the List<DatBase> _children is still the original DAT order, but the List<int> _childrenNameIndex gives a quick way to search for the items.

    public class DatDir : DatBase
    {

        private byte _datStruct;
        public ZipStructure DatStruct { get { return (ZipStructure)(_datStruct & 0x7f); } } // Structure of Zip Found in Dat
        public bool DatStructFix { get { return (_datStruct & 0x80) == 0x80; } }
        public void SetDatStruct(ZipStructure zipStruncture, bool fix)
        {
            _datStruct = (byte)zipStruncture;
            if (fix)
                _datStruct |= 0x80;
        }

        public DatGame DGame;
        private readonly List<DatBase> _children = new List<DatBase>();
        private readonly List<int> _childrenNameIndex = new List<int>();

        public DatDir(string name, FileType type) : base(name, type) { }

        public DatDir(DatDir dd) : base(dd)
        {
            DGame = dd.DGame != null ? new DatGame(dd.DGame) : null;
            foreach (DatBase child in dd._children)
            {
                if (child is DatDir ddChild) _children.Add(new DatDir(ddChild));
                if (child is DatFile ddFile) _children.Add(new DatFile(ddFile));
            }
            foreach (int childIndex in dd._childrenNameIndex)
                _childrenNameIndex.Add(childIndex);
        }

        public int Count => _children.Count;

        public DatBase this[int index] => _children[index];

        public DatBase ChildSorted(int index)
        {
            return FileType == FileType.UnSet ?
                _children[_childrenNameIndex[index]] :
                _children[index];
        }

        public DatBase[] ToArray()
        {
            return _children.ToArray();
        }

        public int ChildAdd(DatBase datItem)
        {
            int index;
            if (FileType == FileType.UnSet)
            {
                ChildNameBinarySearch(datItem, true, false, out int indexSearch);
                _children.Add(datItem);
                index = _children.Count - 1;
                _childrenNameIndex.Insert(indexSearch, index);
                return index;
            }

            ChildNameBinarySearch(datItem, false, false, out index);
            _children.Insert(index, datItem);
            return index;
        }

        public void ChildRemove(DatBase datItem)
        {
            int count = _children.Count;
            for (int i = 0; i < count; i++)
            {
                if (_children[i] != datItem)
                    continue;

                _children.RemoveAt(i);

                if (FileType != FileType.UnSet)
                    return;

                for (int j = 0; j < count; j++)
                {
                    if (_childrenNameIndex[j] == i)
                    {
                        _childrenNameIndex.RemoveAt(j);
                        count--;
                        j--;
                    }
                    else if (_childrenNameIndex[j] > i)
                        _childrenNameIndex[j]--;
                }
                return;
            }
        }

        public void ChildrenClear()
        {
            _children.Clear();
            _childrenNameIndex.Clear();
        }

        public int ChildNameSearch(DatBase lName, out int index)
        {
            if (FileType != FileType.UnSet)
                return ChildNameBinarySearch(lName, false, true, out index);

            int retval = ChildNameBinarySearch(lName, true, true, out index);
            index = retval == 0 ? _childrenNameIndex[index] : -1;
            return retval;
        }

        public void ChildNameSearchAdd(ref DatDir dirFind)
        {
            if (ChildNameSearch(dirFind, out int index) != 0)
                ChildAdd(dirFind);
            else
                dirFind = (DatDir)_children[index];
        }

        private int ChildNameBinarySearch(DatBase lName, bool useIndex, bool findFirst, out int index)
        {
            int intBottom = 0;
            int intTop = _children.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop) / 2;

                intRes = SortCompareName(lName, _children[useIndex ? _childrenNameIndex[intMid] : intMid]);
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

            if (intRes == 0)
            {
                if (findFirst)
                {
                    // if match found check up the list to the first match
                    int intRes1 = 0;
                    while ((index > 0) && (intRes1 == 0))
                    {
                        intRes1 = SortCompareName(lName, _children[useIndex ? _childrenNameIndex[index - 1] : index - 1]);
                        if (intRes1 == 0)
                        {
                            index--;
                        }
                    }
                }
                else
                {
                    // if match was not found check down the list to point past the last match
                    // this is used so that if a duplicate is found, we return the next record as the index
                    // where the duplicate item should be added.
                    int intRes1 = 0;
                    while ((index < _children.Count - 1) && (intRes1 == 0))
                    {
                        intRes1 = SortCompareName(lName, _children[useIndex ? _childrenNameIndex[index + 1] : index + 1]);
                        if (intRes1 == 0)
                        {
                            index++;
                        }
                    }
                    // if intRes1 is still 0, then we are at the very end of the list.
                    // increase index to point past the end so that the item is added on the end of the list.
                    if (intRes1 == 0)
                    {
                        index++;
                    }
                }

            }
            // if the search is greater than the closest match move one down the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;

        }

        private int SortCompareName(DatBase lName, DatBase dBase)
        {

            switch (FileType)
            {
                case FileType.UnSet:
                    {
                        int res = Sorters.StringCompare(lName.Name, dBase.Name);
                        if (res != 0)
                            return res;
                        break;
                    }
                case FileType.Dir:
                    {
                        int res = Sorters.DirectoryNameCompareCase(lName.Name, dBase.Name);
                        if (res != 0)
                            return res;
                        break;
                    }
                case FileType.Zip:
                    {
                        int res = Sorters.TrrntZipStringCompareCase(lName.Name, dBase.Name);
                        if (res != 0)
                            return res;
                        break;
                    }
                case FileType.SevenZip:
                    {
                        int res = Sorters.Trrnt7ZipStringCompare(lName.Name, dBase.Name);
                        if (res != 0)
                            return res;
                        break;
                    }
                default:

                    throw new InvalidOperationException("Invalid directory compare type " + FileType);

            }
            return lName.FileType.CompareTo(dBase.FileType);
        }
    }
}
