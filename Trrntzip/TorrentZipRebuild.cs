using System;
using System.Collections.Generic;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.Utils;
using Compress.ZipFile;
using Compress.ZipFile.ZLib;
using File = RVIO.File;
using Path = RVIO.Path;

namespace Trrntzip
{
    public static class TorrentZipRebuild
    {
        public static TrrntZipStatus ReZipFiles(List<ZippedFile> zippedFiles, ICompress originalZipFile, byte[] buffer, StatusCallback StatusCallBack, LogCallback LogCallback, int ThreadID)
        {
            zipType inputType;
            if (originalZipFile is ZipFile)
            {
                inputType = zipType.zip;
            }
            else if (originalZipFile is SevenZ)
            {
                inputType = zipType.sevenzip;
            }
            else
            {
                return TrrntZipStatus.Unknown;
            }

            zipType outputType = Program.OutZip == zipType.both ? inputType : Program.OutZip;

            int bufferSize = buffer.Length;

            string filename = originalZipFile.ZipFilename;
            string tmpFilename = Path.GetDirectoryName(filename) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(filename) + ".tmp";

            string outExt = outputType == zipType.zip ? ".zip" : ".7z";
            string outfilename = Path.GetDirectoryName(filename) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(filename) + outExt;

            if (inputType != outputType)
            {
                if (File.Exists(outfilename))
                {
                    LogCallback?.Invoke(ThreadID, "Error output " + outExt + " file already exists");
                    return TrrntZipStatus.RepeatFilesFound;
                }
            }

            if (File.Exists(tmpFilename))
            {
                File.Delete(tmpFilename);
            }

            ICompress zipFileOut = outputType == zipType.zip ? new ZipFile() : (ICompress) new SevenZ();

            try
            {
                zipFileOut.ZipFileCreate(tmpFilename);


                ulong fileSizeTotal = 0;
                ulong fileSizeProgress = 0;
                int filePercentReported = 20;
                foreach (ZippedFile f in zippedFiles)
                {
                    fileSizeTotal += f.Size;
                }

                // by now the zippedFiles have been sorted so just loop over them
                for (int i = 0; i < zippedFiles.Count; i++)
                {
                    ZippedFile t = zippedFiles[i];

                    if (Program.VerboseLogging)
                    {
                        LogCallback?.Invoke(ThreadID, $"{t.Size,15}  {t.StringCRC}   {t.Name}");
                    }

                    Stream readStream = null;
                    ulong streamSize = 0;
                    ushort compMethod;

                    ZipFile z = originalZipFile as ZipFile;
                    ZipReturn zrInput = ZipReturn.ZipUntested;
                    if (z != null)
                    {
                        zrInput = z.ZipFileOpenReadStream(t.Index, false, out readStream, out streamSize, out compMethod);
                    }
                    SevenZ z7 = originalZipFile as SevenZ;
                    if (z7 != null)
                    {
                        zrInput = z7.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                    }

                    Stream writeStream;
                    ZipReturn zrOutput = zipFileOut.ZipFileOpenWriteStream(false, true, t.Name, streamSize, 8, out writeStream);

                    if ((zrInput != ZipReturn.ZipGood) || (zrOutput != ZipReturn.ZipGood))
                    {
                        //Error writing local File.
                        zipFileOut.ZipFileClose();
                        originalZipFile.ZipFileClose();
                        File.Delete(tmpFilename);
                        return TrrntZipStatus.CorruptZip;
                    }

                    Stream crcCs = new CrcCalculatorStream(readStream, true);

                    ulong sizetogo = streamSize;
                    while (sizetogo > 0)
                    {
                        int sizenow = sizetogo > (ulong) bufferSize ? bufferSize : (int) sizetogo;

                        fileSizeProgress += (ulong) sizenow;
                        int filePercent = (int) ((double) fileSizeProgress/fileSizeTotal*20);
                        if (filePercent != filePercentReported)
                        {
                            StatusCallBack?.Invoke(ThreadID, filePercent*5);
                            filePercentReported = filePercent;
                        }

                        crcCs.Read(buffer, 0, sizenow);
                        writeStream.Write(buffer, 0, sizenow);
                        sizetogo = sizetogo - (ulong) sizenow;
                    }
                    writeStream.Flush();

                    crcCs.Close();
                    if (z != null)
                    {
                        originalZipFile.ZipFileCloseReadStream();
                    }

                    uint crc = (uint) ((CrcCalculatorStream) crcCs).Crc;

                    if (crc != t.CRC)
                    {
                        return TrrntZipStatus.CorruptZip;
                    }

                    zipFileOut.ZipFileCloseWriteStream(t.ByteCRC);
                }
                StatusCallBack?.Invoke(ThreadID, 100);

                zipFileOut.ZipFileClose();
                originalZipFile.ZipFileClose();
                File.Delete(filename);
                File.Move(tmpFilename, outfilename);

                return TrrntZipStatus.ValidTrrntzip;
            }
            catch (Exception)
            {
                zipFileOut?.ZipFileCloseFailed();
                originalZipFile?.ZipFileClose();
                return TrrntZipStatus.CorruptZip;
            }
        }
    }
}