using RomVaultCore.FindFix;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using File = RVIO.File;
using FileInfo = RVIO.FileInfo;


namespace RomVaultCore.FixFile.Utils
{


    public static partial class FixFileUtils
    {
        /// <summary>
        /// Moves a source file into its destination location and updates DB state.
        /// </summary>
        /// <remarks>
        /// This is used by the fix pipeline for rename/move operations, including CHD parity moves.
        ///
        /// When <paramref name="forceMove"/> is true, the move is attempted even if the usual
        /// <see cref="TestFileMove(RvFile,RvFile)"/> heuristic would skip it.
        ///
        /// When <paramref name="skipDatValidation"/> is true, destination checksum validation against DAT values
        /// is bypassed and the output file inherits the input file's verified hashes. This is used when we have
        /// already proven equivalence via an alternate mechanism (e.g., CHD track parity) and container hashes
        /// are not comparable or not intended to match.
        /// </remarks>
        public static ReturnCode MoveFile(RvFile fileIn, RvFile fileOut, string outFilename, out bool fileMoved, out string error, bool forceMove = false, bool skipDatValidation = false)
        {
            error = "";
            fileMoved = false;

            bool fileMove = forceMove || TestFileMove(fileIn, fileOut);

            if (!fileMove)
            {
                return ReturnCode.Good;
            }

            byte[] bCRC = null;
            byte[] bMD5 = null;
            byte[] bSHA1 = null;

            string fileNameIn = fileIn.FullNameCase;
            if (!File.Exists(fileNameIn))
            {
                error = "Rescan needed, File Not Found :" + fileNameIn;
                return ReturnCode.RescanNeeded;
            }
            FileInfo fileInInfo = new FileInfo(fileNameIn);
            if (fileInInfo.LastWriteTime != fileIn.FileModTimeStamp)
            {
                error = "Rescan needed, File Changed :" + fileNameIn;
                return ReturnCode.RescanNeeded;
            }

            string fileNameOut = outFilename ?? fileOut.FullName;
            try
            {
                string outDir = System.IO.Path.GetDirectoryName(fileNameOut);
                if (!string.IsNullOrWhiteSpace(outDir) && !System.IO.Directory.Exists(outDir))
                    System.IO.Directory.CreateDirectory(outDir);

                if (!string.Equals(fileNameIn, fileNameOut, System.StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(fileNameIn, fileNameOut);
                }
            }
            catch
            {
                error = "Could not move file :" + fileNameIn;
                return ReturnCode.CannotMove;
            }


            if (fileIn.CRC != null)
                bCRC = fileIn.CRC.Copy();
            if (fileIn.FileStatusIs(FileStatus.MD5Verified))
            {
                if (fileIn.MD5 != null)
                    bMD5 = fileIn.MD5.Copy();
            }

            if (fileIn.FileStatusIs(FileStatus.SHA1Verified))
            {
                if (fileIn.SHA1 != null)
                    bSHA1 = fileIn.SHA1.Copy();
            }

            FileInfo fi = new FileInfo(fileNameOut);
            fileOut.FileModTimeStamp = fi.LastWriteTime;

            ReturnCode retC = skipDatValidation
                ? ValidateFileOutSkipDatCheck(fileIn, fileOut, true, bCRC, bSHA1, bMD5, out error)
                : ValidateFileOut(fileIn, fileOut, true, bCRC, bSHA1, bMD5, out error);
            if (retC != ReturnCode.Good)
            {
                return retC;
            }

            CheckDeleteFile(fileIn);

            fileMoved = true;
            return ReturnCode.Good;
        }

        /// <summary>
        /// Determines whether the current DB state indicates a file should be moved/renamed.
        /// </summary>
        public static bool TestFileMove(RvFile fileIn, RvFile fileOut)
        {
            if (fileIn == null)
                return false;

            if (fileIn.FileType != FileType.File && fileIn.FileType != FileType.CHD)
                return false;
            if (fileOut.FileType != FileType.File && fileOut.FileType != FileType.CHD)
                return false;

            if (RvFile.treeType(fileIn) == RvTreeRow.TreeSelect.Locked)
                return false;

            if (fileIn.RepStatus == RepStatus.NeededForFix || fileIn.RepStatus == RepStatus.MoveToSort || fileIn.RepStatus == RepStatus.Rename || fileIn.RepStatus == RepStatus.MoveToCorrupt)
                return true;

            return false;
        }

    }
}
