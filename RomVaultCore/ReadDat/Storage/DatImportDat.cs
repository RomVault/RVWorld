using DATReader.DatClean;
using DATReader.DatStore;
using RomVaultCore.RvDB;

namespace RomVaultCore.Storage.Dat
{
    public class DatImportDat
    {
        public string DatFullName;
        public long TimeStamp;
        public RemoveSubType SubDirType;
        public HeaderType headerType;
        public DatHeader datHeader;


        private DatFlags datFlags;
        public bool Flag(DatFlags datFlag)
        {
            return (datFlags & datFlag) != 0;
        }
        public void SetFlag(DatFlags datFlag, bool value)
        {
            datFlags = datFlags & ~datFlag;
            if (value) datFlags |= datFlag;
        }
        public DatFlags Flags { get { return datFlags; } }
    }
}
