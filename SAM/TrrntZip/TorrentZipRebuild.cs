using System;
using System.Collections.Generic;
using System.IO;
using Compress;
using Compress.SevenZip;
using Compress.StructuredZip;
using Compress.Support.Utils;
using Compress.ZipFile;
using File = RVIO.File;
using Path = RVIO.Path;

namespace TrrntZip
{
    public static class TorrentZipRebuild
    {
        public static TrrntZipStatus ReZipFiles(List<ZippedFile> zippedFiles, ICompress originalZipFile, ZipStructure outputType, byte[] buffer, StatusCallback statusCallBack, LogCallback logCallback, ErrorCallback errorCallback, int threadId, int threadCount, PauseCancel pc, Settings settings)
        {
            InputZipType inputType;

            string inExt = "";
            switch (originalZipFile)
            {
                case Zip _:
                    inputType = InputZipType.Zip;
                    inExt = ".zip";
                    break;
                case SevenZ _:
                    inputType = InputZipType.SevenZip;
                    inExt = ".7z";
                    break;
                case Compress.File.File _:
                    inputType = InputZipType.File;
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }

            int bufferSize = buffer.Length;


            string filename = originalZipFile.ZipFilename;

            // if the source file is a file (not an archive) use the full source name, if the source is an archive remove the original extention
            string fileNameOutputPart = inputType == InputZipType.File ? Path.GetFileName(filename) : Path.GetFileNameWithoutExtension(filename);
            string fileNameOutputDir = Path.GetDirectoryName(filename);

            string tmpFilename = $"{fileNameOutputDir}{Path.DirSeparatorChar}__{Path.GetFileName(filename)}.samtmp";

            string outExt = outputType == ZipStructure.ZipTrrnt || outputType == ZipStructure.ZipZSTD ? ".zip" : ".7z";
            string outfilename = $"{fileNameOutputDir}{Path.DirSeparatorChar}{fileNameOutputPart + outExt}";

            if (inExt != outExt)
            {
                if (File.Exists(outfilename))
                {
                    logCallback?.Invoke(threadId, "Error output " + outExt + " file already exists");
                    return TrrntZipStatus.RepeatFilesFound;
                }
            }

            if (File.Exists(tmpFilename))
            {
                File.Delete(tmpFilename);
            }


            ICompress zipFileOut = null;
            try
            {
                ZipReturn zr;
                if (outputType == ZipStructure.ZipTrrnt || outputType == ZipStructure.ZipZSTD)
                {
                    zipFileOut = new StructuredZip();
                    zr = ((StructuredZip)zipFileOut).ZipFileCreate(tmpFilename, outputType);
                }
                else
                {
                    ulong unCompressedSize = 0;
                    foreach (ZippedFile f in zippedFiles)
                        unCompressedSize += f.Size;

                    zipFileOut = new Structured7Zip();
                    zr = ((Structured7Zip)zipFileOut).ZipFileCreateFromUncompressedSize(tmpFilename, outputType, unCompressedSize);
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
                    if (settings.VerboseLogging)
                    {
                        logCallback?.Invoke(threadId, $"{t.Size,15}  {t.StringCRC}   {t.Name}");
                    }

                    Stream readStream = null;
                    ulong streamSize = 0;

                    ZipReturn zrInput = ZipReturn.ZipUntested;

                    if (t.Size > 0)
                    {
                        switch (inputType)
                        {
                            case InputZipType.Zip:
                                zrInput = ((Zip)originalZipFile).ZipFileOpenReadStream(t.Index, false, out readStream, out streamSize, out ZipCompression _);
                                break;
                            case InputZipType.SevenZip:
                                zrInput = originalZipFile.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                                break;
                            case InputZipType.File:
                                zrInput = originalZipFile.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                                break;
                        }
                    }
                    else
                    {
                        // do nothing for a zero size file, the stream will not be used.
                        zrInput = ZipReturn.ZipGood;
                    }

                    ZipCompression outputcompressionType = StructuredArchive.GetCompressionType(outputType,t.Size);

                    ZipReturn zrOutput = zipFileOut.ZipFileOpenWriteStream(false, t.Name, streamSize, outputcompressionType, out Stream writeStream, threadCount: threadCount);

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
                    writeStream?.Flush();

                    crcCs.Close();
                    if (inputType != InputZipType.SevenZip)
                    {
                        originalZipFile.ZipFileCloseReadStream();
                    }

                    uint crc = (uint)((CrcCalculatorStream)crcCs).Crc;

                    if (t.CRC == null)
                        t.CRC = crc;

                    if (crc != t.CRC)
                    {
                        try
                        {
                            zipFileOut.ZipFileCloseFailed();
                        }
                        catch (Exception e)
                        {
                            errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Closing Temp zipfile With CRC match error {tmpFilename}\n{e.Message}");
                        }
                        try
                        {
                            originalZipFile.ZipFileClose();
                        }
                        catch (Exception e)
                        {
                            errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Closing Source zipfile With CRC match error {filename}\n{e.Message}");
                        }
                        try
                        {
                            File.Delete(tmpFilename);
                        }
                        catch (Exception e)
                        {
                            errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Deleting temp zipfile {tmpFilename}\n{e.Message}");
                        }


                        return TrrntZipStatus.CorruptZip;
                    }

                    zipFileOut.ZipFileCloseWriteStream(t.ByteCRC);
                }
                statusCallBack?.Invoke(threadId, 100);

                TrrntZipStatus result = TrrntZipStatus.Trrntzipped;
                try
                {
                    zipFileOut.ZipFileClose();
                }
                catch (Exception e)
                {
                    errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Closing Temp zipfile {tmpFilename}\n{e.Message}");
                    result = TrrntZipStatus.CatchError;
                }
                try
                {
                    originalZipFile.ZipFileClose();
                }
                catch (Exception e)
                {
                    errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Closing Source zipfile {filename}\n{e.Message}");
                    result = TrrntZipStatus.CatchError;
                }
                try
                {
                    File.Delete(filename);
                }
                catch (Exception e)
                {
                    errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Deleting Source zipfile {filename}\n{e.Message}");
                    result = TrrntZipStatus.CatchError;
                }
                try
                {
                    File.Move(tmpFilename, outfilename);
                }
                catch (Exception e)
                {
                    errorCallback?.Invoke(threadId, $"Error In TorrentZipRebuid\nError Renameing temp file {tmpFilename} to {outfilename}\n{e.Message}");
                    result = TrrntZipStatus.CatchError;
                }

                return result;
            }
            catch (Exception e)
            {
                zipFileOut?.ZipFileCloseFailed();
                originalZipFile?.ZipFileClose();

                lock (settings.lockObj)
                {
                    string content = $"Error In TorrentZipRebuid - {filename}\n";
                    content += $"{e.Message}";
                    if (e.InnerException != null)
                        content += $"\nInnerException: {e.InnerException.Message}";
                    errorCallback?.Invoke(threadId, content);
                }

                return TrrntZipStatus.CatchError;
            }
        }
    }
}