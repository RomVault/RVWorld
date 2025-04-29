/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using Compress;
using DATReader.DatStore;
using RomVaultCore.Utils;
using System.Collections.Generic;

namespace RomVaultCore.RvDB
{
    public partial class RvFile
    {

        private readonly List<RvDat> _dirDats; // DAT's stored in this dir in DatRoot


        public int CountDats()
        {
            if (_dirDats == null)
                return 0;
            return _dirDats.Count;
        }

        public RvDat DirDat(int index)
        {
            return _dirDats[index];
        }

        public void DirDatAdd(RvDat dat)
        {
            BinarySearch.ListSearch(_dirDats, dat, DBHelper.DatCompare, out int index);
            _dirDats.Insert(index, dat);
        }
        public int DirDatRemove(int index)
        {
            _dirDats.RemoveAt(index);
            return _dirDats.Count;
        }

        public RvFile DatAddDirectory(string dirName, int index)
        {
            RvFile tDir = new RvFile(FileType.Dir)
            {
                Name = dirName,
                Tree = new RvTreeRow(),
                DatStatus = DatStatus.InDatCollect
            };
            ChildAdd(tDir, index);
            return tDir;
        }

        public RvFile DatAdd(DatBase datBase, RvDat rvDat, int index)
        {
            RvFile retFile = new RvFile(datBase, rvDat, Tree != null);
            ChildAdd(retFile, index);
            return retFile;
        }

        private RvFile(DatBase datBase, RvDat rvDat, bool parentHasTree) : this(datBase.FileType)
        {
            switch (datBase)
            {
                case DatFile datFile:
                    Name = datFile.Name;
                    Size = datFile.Size;
                    CRC = datFile.CRC;
                    SHA1 = datFile.SHA1;
                    MD5 = datFile.MD5;
                    Merge = datFile.Merge;
                    Status = datFile.Status;
                    Dat = rvDat;
                    DatStatus = datFile.DatStatus;
                    HeaderFileTypeSet = datFile.HeaderFileType; // this could have the Required flag set on it

                    if (datFile.DateModified != null)
                    {
                        FileModTimeStamp = (long)datFile.DateModified;
                        FileStatusSet(FileStatus.DateFromDAT);
                    }

                    if (HeaderFileType != HeaderFileType.Nothing) FileStatusSet(FileStatus.HeaderFileTypeFromDAT);
                    if (Size != null) FileStatusSet(FileStatus.SizeFromDAT);
                    if (CRC != null) FileStatusSet(FileStatus.CRCFromDAT);
                    if (SHA1 != null) FileStatusSet(FileStatus.SHA1FromDAT);
                    if (MD5 != null) FileStatusSet(FileStatus.MD5FromDAT);
                    return;

                case DatDir datDir:
                    // else convert as a dir
                    Name = datDir.Name;
                    Dat = rvDat;
                    DatStatus = datDir.DatStatus;
                    SetZipDatStruct(datDir.DatStruct,datDir.DatStructFix);

                    if (datDir.DGame != null)
                        Game = new RvGame(datDir.DGame);
                    if (parentHasTree && Game == null && Tree == null)
                        Tree = new RvTreeRow();

                    DatBase[] datB = datDir.ToArray();
                    if (datB == null)
                        return;
                    foreach (DatBase b in datB)
                        ChildAdd(new RvFile(b, rvDat, Tree != null));
                    return;

                default:
                    return;
            }
        }


        public void DatMergeIn(DatBase datBase, RvDat rvDat, bool altFile)
        {
            switch (datBase)
            {
                case DatFile datFile:
                    if (HeaderFileType == HeaderFileType.Nothing && datFile.HeaderFileType != HeaderFileType.Nothing) HeaderFileTypeSet = datFile.HeaderFileType;

                    // need to fix this still
                    //if (b.HeaderFileTypeRequired) HeaderFileTypeSet = b._headerFileType;

                    if (datFile.HeaderFileType != HeaderFileType.Nothing) FileStatusSet(FileStatus.HeaderFileTypeFromDAT);
                    if (altFile)
                    {
                        if (datFile.Size != null) { FileStatusSet(FileStatus.AltSizeFromDAT); if (AltSize == null) AltSize = datFile.Size; }
                        if (datFile.CRC != null) { FileStatusSet(FileStatus.AltCRCFromDAT); if (AltCRC == null) AltCRC = datFile.CRC; }
                        if (datFile.SHA1 != null) { FileStatusSet(FileStatus.AltSHA1FromDAT); if (AltSHA1 == null) AltSHA1 = datFile.SHA1; }
                        if (datFile.MD5 != null) { FileStatusSet(FileStatus.AltMD5FromDAT); if (AltMD5 == null) AltMD5 = datFile.MD5; }
                    }
                    else
                    {
                        if (datFile.Size != null) { FileStatusSet(FileStatus.SizeFromDAT); if (Size == null) Size = datFile.Size; }
                        if (datFile.CRC != null) { FileStatusSet(FileStatus.CRCFromDAT); if (CRC == null) CRC = datFile.CRC; }
                        if (datFile.SHA1 != null) { FileStatusSet(FileStatus.SHA1FromDAT); if (SHA1 == null) SHA1 = datFile.SHA1; }
                        if (datFile.MD5 != null) { FileStatusSet(FileStatus.MD5FromDAT); if (MD5 == null) MD5 = datFile.MD5; }
                    }

                    Merge = datFile.Merge;
                    Status = datFile.Status;


                    if (datFile.DateModified != null)
                    {
                        // need to check this does not change the value of FileModTimeStamp
                        FileModTimeStamp = (long)datFile.DateModified;
                        FileStatusSet(FileStatus.DateFromDAT);
                    }
                    break;

                case DatDir datDir:
                    SetZipDatStruct(datDir.DatStruct,datDir.DatStructFix);

                    if (datDir.DGame != null)
                        Game = new RvGame(datDir.DGame);
                    if (Parent?.Tree != null && Game == null && Tree == null)
                        Tree = new RvTreeRow();

                    if (_dirDats.Count > 0)
                        ReportError.SendAndShow("Setting Dir with a dat list");

                    break;
            }

            // Parent , TimeStamp Should already be correct.

            if (GotStatus == GotStatus.NotGot)
                ReportError.SendAndShow("Error Adding DAT to NotGot File " + GotStatus);

            SetDatGotStatus(datBase.DatStatus, GotStatus);

            if (Name == datBase.Name) // case match so all is good
            {
                FileName = null;
            }
            else
            {
                FileName = Name;
                Name = datBase.Name;
            }

            Dat = rvDat;
        }

        public EFile DatRemove()
        {
            /************* RvDir ************/

            Tree = null;
            Game = null;
            SetZipDatStruct(ZipStructure.None,false);
            _dirDats?.Clear();

            /************* RvFile ************/
            HeaderFileTypeSet = HeaderFileType; // this removes the required flag. (as the DAT is being removed.)
            if (!FileStatusIs(FileStatus.HeaderFileTypeFromHeader) && FileStatusIs(FileStatus.HeaderFileTypeFromDAT)) HeaderFileTypeSet = HeaderFileType.Nothing;

            if (!FileStatusIs(FileStatus.SizeFromHeader) && !FileStatusIs(FileStatus.SizeVerified)) Size = null;
            if (!FileStatusIs(FileStatus.CRCFromHeader) && !FileStatusIs(FileStatus.CRCVerified)) CRC = null;
            if (!FileStatusIs(FileStatus.SHA1FromHeader) && !FileStatusIs(FileStatus.SHA1Verified)) SHA1 = null;
            if (!FileStatusIs(FileStatus.MD5FromHeader) && !FileStatusIs(FileStatus.MD5Verified)) MD5 = null;
            //Why not do this?
            //if (!FileStatusIs(FileStatus.AltCRCFromHeader) && !FileStatusIs(FileStatus.AltCRCVerified)) AltCRC = null;
            if (!FileStatusIs(FileStatus.AltSHA1FromHeader) && !FileStatusIs(FileStatus.AltSHA1Verified)) AltSHA1 = null;
            if (!FileStatusIs(FileStatus.AltMD5FromHeader) && !FileStatusIs(FileStatus.AltMD5Verified)) AltMD5 = null;
            if (FileStatusIs(FileStatus.DateFromDAT) && IsFile && !(GotStatus == GotStatus.Got || GotStatus == GotStatus.Corrupt)) FileModTimeStamp = long.MinValue;

            FileStatusClear(FileStatus.DatFlags);

            Merge = "";
            Status = "";

            /************* RvFile ************/

            Dat = null;
            if (GotStatus == GotStatus.NotGot)
            {
                return EFile.Delete;
            }

            if (!string.IsNullOrEmpty(FileName))
            {
                Name = FileName;
                FileName = null;
            }
            DatStatus = DatStatus.NotInDat;
#if dt
            DatModTimeStamp = null;
#endif
            return EFile.Keep;
        }
    }
}
