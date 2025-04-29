public enum DatStatus
{
    InDatCollect,
    InDatMerged,
    InDatNoDump,
    NotInDat,       // Any item not in a dat and not in ToSort should have this status
    InToSort,       // All items in any ToSort directory should have this status
    InDatMIA
}