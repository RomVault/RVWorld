using Compress;
using Compress.SevenZip;
using Compress.StructuredZip;
using RomVaultCore.FixFile.Utils;
using RomVaultCore.RvDB;
using RVIO;

namespace RomVaultCore.FixFile
{
    /// <summary>
    /// Helper functions used by the archive fix pipeline.
    /// </summary>
    /// <remarks>
    /// These helpers are used when rebuilding archives (ZIP/7z) as part of fixing:
    /// - opening output archives with the right structure
    /// - estimating uncompressed size for solid compression planning
    /// - moving unrecoverable archives to the Corrupt area in ToSort
    /// </remarks>
    public static class FixAZipFunctions
    {

        /// <summary>
        /// Opens an output archive for writing, using the correct target structure and compression settings.
        /// </summary>
        /// <param name="fixZip">The source archive being fixed.</param>
        /// <param name="UncompressedSize">Estimated uncompressed size of the output archive.</param>
        /// <param name="outputZipFilename">Temporary output filename to create.</param>
        /// <param name="outputFixZip">Opened archive writer.</param>
        /// <param name="errorMessage">Error message on failure.</param>
        /// <returns>A <see cref="ReturnCode"/>.</returns>
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

        /// <summary>
        /// Returns the total uncompressed size of members that will be present in the rebuilt archive.
        /// </summary>
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

        /// <summary>
        /// Returns the total uncompressed size of members that are being moved to ToSort.
        /// </summary>
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

        /// <summary>
        /// Moves a corrupt archive to the ToSort\\Corrupt folder and updates the DB tree accordingly.
        /// </summary>
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
