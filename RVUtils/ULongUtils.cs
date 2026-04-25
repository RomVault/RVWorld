
namespace RVUtils;

public static class ULongUtils
{
    public static int ULongCompareNull(ulong? v0, ulong? v1)
    {
        if (v0 == null && v1 == null) return 0;
        if (v0 != null && v1 == null) return 1;
        if (v0 == null && v1 != null) return -1;
        return ((ulong)v0).CompareTo((ulong)v1);
    }
}

