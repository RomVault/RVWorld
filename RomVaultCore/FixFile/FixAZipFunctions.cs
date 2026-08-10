using Compress;
using Compress.SevenZip;
using Compress.StructuredZip;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    public static class FixAZipFunctions
    {

        public static ReturnCode OpenOutputZip(RvFile fixZip, ulong UncompressedSize, string outputZipFilename, out ICompress outputFixZip, out string errorMessage)
        {
            outputFixZip = null;


            if (File.Exists(outputZipFilename))
            {
                errorMessage = "Rescan needed, Unkown existing file found :" + outputZipFilename;
                return ReturnCode.RescanNeeded;
            }

            ZipStructure newFileStruct = (fixZip.DatStatus == DatStatus.NotInDat || fixZip.DatStatus == DatStatus.InToSort)
                                          ? fixZip.ZipStruct
                                          : fixZip.ZipDatStruct;

            if ((newFileStruct == ZipStructure.None && fixZip.FileType == FileType.SevenZip) || newFileStruct == ZipStructure.SevenZipTrrnt)
                newFileStruct = Settings.rvSettings.getDefault7ZStruct;

            ZipReturn zrf;
            if (fixZip.FileType == FileType.Zip)
            {
                outputFixZip = new StructuredZip();
                zrf = ((StructuredZip)outputFixZip).ZipFileCreate(outputZipFilename, newFileStruct);
            }
            else
            {
                outputFixZip = new SevenZ();
                zrf = ((SevenZ)outputFixZip).ZipFileCreateFromUncompressedSize(outputZipFilename, newFileStruct, UncompressedSize);
            }

            if (zrf != ZipReturn.ZipGood)
            {
                errorMessage = $"Error Opening Write Stream {zrf}\nMessage: {Compress.Error.ErrorMessage}\nCode: {Compress.Error.ErrorCode}";
                return ReturnCode.FileSystemError;
            }

            errorMessage = "";
            return ReturnCode.Good;
        }


        public static ulong GetUncompressedSize(RvFile fixZip)
        {
            ulong uncompressedSize = 0;

            for (int i = 0; i < fixZip.ChildCount; i++)
            {
                RvFile sevenZippedFile = fixZip.Child(i);
                switch (sevenZippedFile.RepStatus)
                {
                    case RepStatus.InToSort:
                    case RepStatus.Correct:
                    case RepStatus.CorrectMIA:
                    case RepStatus.CanBeFixed:
                    case RepStatus.CanBeFixedMIA:
                    case RepStatus.CorruptCanBeFixed:
                        uncompressedSize += sevenZippedFile.FileGroup.Size ?? 0;
                        break;
                    default:
                        break;
                }
            }

            return uncompressedSize;
        }

        public static ulong GetMoveToSortUncompressedSize(RvFile fixZip)
        {
            ulong uncompressedSize = 0;

            for (int i = 0; i < fixZip.ChildCount; i++)
            {
                RvFile sevenZippedFile = fixZip.Child(i);
                switch (sevenZippedFile.RepStatus)
                {
                    case RepStatus.MoveToSort:
                        uncompressedSize += sevenZippedFile.FileGroup.Size ?? 0;
                        break;
                    default:
                        break;
                }
            }

            return uncompressedSize;
        }


        public static ReturnCode MoveZipToCorrupt(RvFile fixZip, out string errorMessage)
        {
            errorMessage = "";

            string fixZipFullName = fixZip.FullName;
            if (!File.Exists(fixZipFullName))
            {
                errorMessage = "File for move to corrupt not found " + fixZip.FullName;
                return ReturnCode.RescanNeeded;
            }
            FileInfo fi = new FileInfo(fixZipFullName);
            if (fi.LastWriteTime != fixZip.FileModTimeStamp)
            {
                errorMessage = "File for move to corrupt timestamp not correct " + fixZip.FullName;
                return ReturnCode.RescanNeeded;
            }

            string corruptDir = Path.Combine(DB.GetToSortPrimary().Name, "Corrupt");
            if (!Directory.Exists(corruptDir))
            {
                Directory.CreateDirectory(corruptDir);
            }

            RvFile toSort = DB.GetToSortPrimary();
            RvFile corruptDirNew = new RvFile(FileType.Dir) { Name = "Corrupt", DatStatus = DatStatus.InToSort };
            int found = toSort.ChildNameSearch(corruptDirNew, out int indexcorrupt);
            if (found != 0)
            {
                corruptDirNew.GotStatus = GotStatus.Got;
                indexcorrupt = toSort.ChildAdd(corruptDirNew);
            }

            string toSortFullName = Path.Combine(corruptDir, fixZip.Name);
            string toSortFileName = fixZip.Name;
            int fileC = 0;
            while (File.Exists(toSortFullName))
            {
                fileC++;

                string fName = Path.GetFileNameWithoutExtension(fixZip.Name);
                string fExt = Path.GetExtension(fixZip.Name);
                toSortFullName = Path.Combine(corruptDir, fName + fileC + fExt);
                toSortFileName = fixZip.Name + fileC;
            }

            if (!File.SetAttributes(fixZipFullName, FileAttributes.Normal))
            {
                Report.ReportProgress(new bgwShowError(fixZipFullName, "Error Setting File Attributes to Normal. Before Moving To Corrupt. Code " + RVIO.Error.ErrorMessage));
            }


            File.Move(fixZipFullName, toSortFullName);
            FileInfo toSortCorruptFile = new FileInfo(toSortFullName);

            RvFile toSortCorruptGame = new RvFile(FileType.Zip)
            {
                Name = toSortFileName,
                DatStatus = DatStatus.InToSort,
                FileModTimeStamp = toSortCorruptFile.LastWriteTime,
                GotStatus = GotStatus.Corrupt
            };
            toSort.Child(indexcorrupt).ChildAdd(toSortCorruptGame);

            FixFileUtils.CheckDeleteFile(fixZip);

            return ReturnCode.Good;
        }

    }
}
