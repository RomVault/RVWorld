using System.Collections.Generic;

namespace CHDSharpLib.Utils;

/// <summary>
/// Simple fixed-size byte[] pool used to reduce allocations during CHD decoding.
/// </summary>
internal class ArrayPool
{
    /// <summary>
    /// Size of arrays managed by this pool.
    /// </summary>
    private uint _arraySize;

    /// <summary>
    /// Backing store of returned arrays.
    /// </summary>
    private List<byte[]> _array;

    /// <summary>
    /// Number of arrays currently available in <see cref="_array"/>.
    /// </summary>
    private int _count;

    /// <summary>
    /// Total number of arrays created by this pool.
    /// </summary>
    private int _issuedArraysTotal;

    /// <summary>
    /// Creates an array pool for fixed-size buffers.
    /// </summary>
    /// <param name="arraySize">Size of each buffer.</param>
    internal ArrayPool(uint arraySize)
    {
        _array = new List<byte[]>();
        _arraySize = arraySize;
        _count = 0;
        _issuedArraysTotal = 0;
    }

    /// <summary>
    /// Rents a buffer from the pool or allocates a new one if none are available.
    /// </summary>
    /// <returns>Buffer of size <see cref="_arraySize"/>.</returns>
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

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="ret">Buffer previously obtained from <see cref="Rent"/>.</param>
    internal void Return(byte[] ret)
    {
        lock (_array)
        {
            _array.Add(ret);
            _count++;
        }
    }

    /// <summary>
    /// Reads pool statistics useful for diagnostics.
    /// </summary>
    /// <param name="issuedArraysTotal">Total number of allocated arrays.</param>
    /// <param name="returnedArraysTotal">Current number of arrays available in the pool.</param>
    internal void ReadStats(out int issuedArraysTotal, out int returnedArraysTotal)
    {
        issuedArraysTotal = _issuedArraysTotal;
        returnedArraysTotal = _count;
    }
}
