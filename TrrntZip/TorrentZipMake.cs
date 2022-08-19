using System;
using System.Collections.Generic;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.Support.Utils;
using Compress.ZipFile;
using File = RVIO.File;
using Path = RVIO.Path;

namespace TrrntZip
{
    public static class TorrentZipMake
    {
        public static TrrntZipStatus ZipFiles(List<ZippedFile> zippedFiles, string filename, byte[] buffer, StatusCallback statusCallBack, LogCallback logCallback, int threadId, PauseCancel pc)
        {

            int bufferSize = buffer.Length;
            Compress.File.File originalZipFile = null;

            zipType outputType = Program.OutZip == zipType.archive ? zipType.zip : Program.OutZip;

            // if the source file is a file (not an archive) use the full source name, if the source is an archive remove the original extention
            string fileNameOutputPart = Path.GetFileName(filename);
            string fileNameOutputDir = Path.GetDirectoryName(filename);

            string tmpFilename = Path.Combine(fileNameOutputDir, "__" + Path.GetFileName(filename) + ".tztmp");

            string outExt = outputType == zipType.zip ? ".zip" : ".7z";
            string outfilename = Path.Combine(fileNameOutputDir, fileNameOutputPart + outExt);

            if (File.Exists(outfilename))
            {
                logCallback?.Invoke(threadId, "Error output " + outExt + " file already exists");
                return TrrntZipStatus.RepeatFilesFound;
            }

            if (File.Exists(tmpFilename))
            {
                File.Delete(tmpFilename);
            }

            ICompress zipFileOut = outputType == zipType.zip ? new Zip() : (ICompress)new SevenZ();
            try
            {
                ZipReturn zr;
                if (outputType == zipType.zip)
                {
                    zr = ((Zip)zipFileOut).ZipFileCreate(tmpFilename, OutputZipType.TrrntZip);
                }
                else
                {
                    zr = zipFileOut.ZipFileCreate(tmpFilename);
                }
                if (zr != ZipReturn.ZipGood)
                    return TrrntZipStatus.ErrorOutputFile;

                ulong fileSizeTotal = 0;
                ulong fileSizeProgress = 0;
                int filePercentReported = 20;
                foreach (ZippedFile f in zippedFiles)
                {
                    fileSizeTotal += f.Size;
                }

                // by now the zippedFiles have been sorted so just loop over them
                foreach (ZippedFile t in zippedFiles)
                {
                    if (Program.VerboseLogging)
                    {
                        logCallback?.Invoke(threadId, $"{t.Size,15}  {t.StringCRC}   {t.Name}");
                    }

                    Stream readStream = null;
                    ulong streamSize = 0;

                    ZipReturn zrInput = ZipReturn.ZipUntested;

                    if (t.Size > 0)
                    {
                        originalZipFile = new Compress.File.File();
                        originalZipFile.ZipFileOpen(Path.Combine(filename, t.Name.Replace("/","\\")), -1, false);
                        zrInput = originalZipFile.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                    }
                    else
                    {
                        // do nothing for a zero size file, the stream will not be used.
                        zrInput = ZipReturn.ZipGood;
                    }

                    ZipReturn zrOutput = zipFileOut.ZipFileOpenWriteStream(false, true, t.Name, streamSize, 8, out Stream writeStream);

                    if ((zrInput != ZipReturn.ZipGood) || (zrOutput != ZipReturn.ZipGood))
                    {
                        //Error writing local File.
                        zipFileOut.ZipFileCloseFailed();
                        originalZipFile.ZipFileClose();
                        File.Delete(tmpFilename);
                        return TrrntZipStatus.CorruptZip;
                    }

                    Stream crcCs = new CrcCalculatorStream(readStream, true);

                    ulong sizetogo = streamSize;
                    while (sizetogo > 0)
                    {
                        if (pc != null)
                        {
                            pc.WaitOne();
                            if (pc.Cancelled)
                            {
                                zipFileOut.ZipFileCloseFailed();
                                originalZipFile.ZipFileClose();
                                File.Delete(tmpFilename);
                                return TrrntZipStatus.Cancel;
                            }
                        }

                        int sizenow = sizetogo > (ulong)bufferSize ? bufferSize : (int)sizetogo;

                        fileSizeProgress += (ulong)sizenow;
                        int filePercent = (int)((double)fileSizeProgress / fileSizeTotal * 20);
                        if (filePercent != filePercentReported)
                        {
                            statusCallBack?.Invoke(threadId, filePercent * 5);
                            filePercentReported = filePercent;
                        }

                        crcCs.Read(buffer, 0, sizenow);
                        writeStream.Write(buffer, 0, sizenow);
                        sizetogo = sizetogo - (ulong)sizenow;
                    }
                    writeStream.Flush();

                    crcCs.Close();
                    originalZipFile.ZipFileClose();
                    originalZipFile = null;

                    uint crc = (uint)((CrcCalculatorStream)crcCs).Crc;

                    if (t.CRC == null)
                        t.CRC = crc;

                    if (crc != t.CRC)
                    {
                        zipFileOut.ZipFileCloseFailed();
                        originalZipFile.ZipFileClose();
                        File.Delete(tmpFilename);
                        return TrrntZipStatus.CorruptZip;
                    }

                    zipFileOut.ZipFileCloseWriteStream(t.ByteCRC);
                }
                statusCallBack?.Invoke(threadId, 100);

                zipFileOut.ZipFileClose();
                Directory.Delete(filename,true);
                File.Move(tmpFilename, outfilename);

                return TrrntZipStatus.Trrntzipped;
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