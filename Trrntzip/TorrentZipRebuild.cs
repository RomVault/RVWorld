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
    public static class TorrentZipRebuild
    {
        public static TrrntZipStatus ReZipFiles(List<ZippedFile> zippedFiles, ICompress originalZipFile, byte[] buffer, StatusCallback statusCallBack, LogCallback logCallback, int threadId, PauseCancel pc)
        {
            zipType inputType;
            switch (originalZipFile)
            {
                case Zip _:
                    inputType = zipType.zip;
                    break;
                case SevenZ _:
                    inputType = zipType.sevenzip;
                    break;
                case Compress.File.File _:
                    inputType = zipType.file;
                    break;
                default:
                    return TrrntZipStatus.Unknown;
            }

            zipType outputType = Program.OutZip == zipType.archive ? inputType : Program.OutZip;

            int bufferSize = buffer.Length;

            string filename = originalZipFile.ZipFilename;
            string tmpFilename = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + ".tmp");

            string outExt = outputType == zipType.zip ? ".zip" : ".7z";
            string outfilename = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + outExt);

            if (inputType != outputType)
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

            ICompress zipFileOut = outputType == zipType.zip ? new Zip() : (ICompress)new SevenZ();

            try
            {
                if (outputType == zipType.zip)
                    ((Zip)zipFileOut).ZipFileCreate(tmpFilename, OutputZipType.TrrntZip);
                else
                {
                    zipFileOut.ZipFileCreate(tmpFilename);
                }

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
                        switch (originalZipFile)
                        {
                            case Zip z:
                                zrInput = z.ZipFileOpenReadStream(t.Index, false, out readStream, out streamSize, out ushort _);
                                break;
                            case SevenZ z7:
                                zrInput = z7.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                                break;
                            case Compress.File.File zf:
                                zrInput = zf.ZipFileOpenReadStream(t.Index, out readStream, out streamSize);
                                break;
                        }
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
                    if (inputType != zipType.sevenzip)
                    {
                        originalZipFile.ZipFileCloseReadStream();
                    }

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
                originalZipFile.ZipFileClose();
                File.Delete(filename);
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