using System.Collections.Generic;

namespace CHDSharpLib.Utils;

internal class ArrayPool
{
    private uint _arraySize;
    private List<byte[]> _array;
    private int _count;
    private int _issuedArraysTotal;

    internal ArrayPool(uint arraySize)
    {
        _array = new List<byte[]>();
        _arraySize = arraySize;
        _count = 0;
        _issuedArraysTotal = 0;
    }

    internal byte[] Rent()
    {
        byte[] ret;
        lock (_array)
        {
            if (_count == 0)
            {
                ret = new byte[_arraySize];
                _issuedArraysTotal++;
            }
            else
            {
                _count--;
                ret = _array[_count];
                _array.RemoveAt(_count);
            }
        }
        return ret;

    }

    internal void Return(byte[] ret)
    {
        lock (_array)
        {
            _array.Add(ret);
            _count++;
        }
    }

    internal void ReadStats(out int issuedArraysTotal, out int returnedArraysTotal)
    {
        issuedArraysTotal = _issuedArraysTotal;
        returnedArraysTotal = _count;
    }
}
