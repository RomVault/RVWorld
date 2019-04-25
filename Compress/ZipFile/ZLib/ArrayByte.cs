namespace Compress.ZipFile.ZLib
{
   public static class ArrayByte
    {
        
        public static void Copy(byte[] sourceArray, int sourceIndex, byte[] destinationArray, int destinationIndex,int length)
        {
            while (length-- > 0)
                destinationArray[destinationIndex++] = sourceArray[sourceIndex++];
        }
        
        /*
        public static unsafe void Copy(byte[] source, int sourceOffset, byte[] target, int targetOffset, int count)
        {
            // If either array is not instantiated, you cannot complete the copy.
            if ((source == null) || (target == null))
            {
                throw new System.ArgumentException();
            }

            // If either offset, or the number of bytes to copy, is negative, you
            // cannot complete the copy.
            if ((sourceOffset < 0) || (targetOffset < 0) || (count < 0))
            {
                throw new System.ArgumentException();
            }

            // If the number of bytes from the offset to the end of the array is 
            // less than the number of bytes you want to copy, you cannot complete
            // the copy. 
            if ((source.Length - sourceOffset < count) ||
                (target.Length - targetOffset < count))
            {
                throw new System.ArgumentException();
            }

            // The following fixed statement pins the location of the source and
            // target objects in memory so that they will not be moved by garbage
            // collection.
            fixed (byte* pSource = source, pTarget = target)
            {
                // Copy the specified number of bytes from source to target.
                for (int i = 0; i < count; i++)
                {
                    pTarget[targetOffset ++] = pSource[sourceOffset ++];
                }
            }
        }
        */
    }
}
