using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Compress;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;

namespace RomVaultCore.FixFile.FixAZipCore
{
    public static class FindSourceFile
    {

        public enum SourceFileFixTypes
        {
            ZipTrrnt_V,
            ZipTrrnt_U,
            ZipTDC_V,
            ZipTDC_U,
            ZipZSTD_V,
            ZipZSTD_U,
            Zip_V,
            Zip_U,
            SevenZipSLZMA_V_MF,
            SevenZipSLZMA_U_MF,
            SevenZipSLZMA_V_SF,
            SevenZipSLZMA_U_SF,
            SevenZipNLZMA_V,
            SevenZipNLZMA_U,
            SevenZipSZSTD_V_MF,
            SevenZipSZSTD_U_MF,
            SevenZipSZSTD_V_SF,
            SevenZipSZSTD_U_SF,
            SevenZipNZSTD_V,
            SevenZipNZSTD_U,
            SevenZipTrrnt_V_MF,
            SevenZipTrrnt_U_MF,
            SevenZipTrrnt_V_SF,
            SevenZipTrrnt_U_SF,
            SevenZip_V_MF,
            SevenZip_U_MF,
            SevenZip_V_SF,
            SevenZip_U_SF,
            File_V
        }
        public enum DestinationFileFixTypes
        {
            ZipTrrnt,
            ZipTDC,
            ZipZSTD,
            Zip,
            SevenZipNLZMA,
            SevenZipSLZMA_SF,
            SevenZipSLZMA_MF,
            SevenZipNZSTD,
            SevenZipSZSTD_SF,
            SevenZipSZSTD_MF,
            //SevenZipTrrnt,
            //SevenZip,
            File
        }

        public enum FixStyle
        {
            Zero,
            RawCopy,
            RawCopyOnceVerified,
            Compress,
            DecompressRecompress,
            ExtractToCache
        }

        public class FixPriorityStyle
        {
            public SourceFileFixTypes SourceType;
            public FixStyle FixStyle;

            public FixPriorityStyle(SourceFileFixTypes sourceType, FixStyle fixStyle)
            {
                SourceType = sourceType;
                FixStyle = fixStyle;
            }
        }

        public class PriorityOrder
        {
            public DestinationFileFixTypes DestinationType;
            public FixPriorityStyle[] SourceType;

            public PriorityOrder(DestinationFileFixTypes destinationType, FixPriorityStyle[] sourceType)
            {
                DestinationType = destinationType;
                SourceType = sourceType;
            }
        }

        public static int[,] FixOrderPriority = null;
        public static FixStyle[,] FixOrderStyle = null;


        public static List<RvFile> GetFixFileList(RvFile fixFile)
        {
            return fixFile.FileGroup.Files.FindAll(file => file.GotStatus == GotStatus.Got && DBHelper.CheckIfMissingFileCanBeFixedByGotFile(fixFile, file));
        }


        public static void SetFixOrderSettings()
        {


            Dictionary<DestinationFileFixTypes, FixPriorityStyle[]> MasterPriorityList = new Dictionary<DestinationFileFixTypes, FixPriorityStyle[]>
            {
                { DestinationFileFixTypes.ZipTrrnt, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* needs decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                }
                },
                { DestinationFileFixTypes.ZipTDC, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* needs decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                }
                },
                { DestinationFileFixTypes.ZipZSTD, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                }
                },

                // non of this should get used as the logic is all wrong for this.
                { DestinationFileFixTypes.Zip, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* needs decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),


                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                }
                },

                { DestinationFileFixTypes.SevenZipNLZMA, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),

                 }
                },

                { DestinationFileFixTypes.SevenZipSLZMA_SF, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                 }
                },

                { DestinationFileFixTypes.SevenZipSLZMA_MF, new[] {
                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                 }
                },


                { DestinationFileFixTypes.SevenZipNZSTD, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.RawCopy), 

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                 }
                },


                { DestinationFileFixTypes.SevenZipSZSTD_SF, new[] {
                    /* raw copy */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.RawCopy),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.RawCopy),

                    /* raw copy once verified */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.RawCopyOnceVerified),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.RawCopyOnceVerified),

                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                 }
                },

                { DestinationFileFixTypes.SevenZipSZSTD_MF, new[] {
                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                }
                },

                { DestinationFileFixTypes.File, new[] {
                    /* needs compressed */
                    new FixPriorityStyle(SourceFileFixTypes.File_V,FixStyle.Compress),

                    /* all need decompressed / recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNZSTD_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.ZipTrrnt_U,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.ZipTDC_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.Zip_V,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.Zip_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_V,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipNLZMA_U,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_SF,FixStyle.DecompressRecompress),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_SF,FixStyle.DecompressRecompress),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_SF,FixStyle.DecompressRecompress),

                    /* needs decompressed to cache and then cache files recompressed */
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSZSTD_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipSLZMA_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZipTrrnt_U_MF,FixStyle.ExtractToCache),

                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_V_MF,FixStyle.ExtractToCache),
                    new FixPriorityStyle(SourceFileFixTypes.SevenZip_U_MF,FixStyle.ExtractToCache),
                 }
                }
            };

            FixOrderPriority = new int[Enum.GetValues(typeof(DestinationFileFixTypes)).Length, Enum.GetValues(typeof(SourceFileFixTypes)).Length];
            FixOrderStyle = new FixStyle[Enum.GetValues(typeof(DestinationFileFixTypes)).Length, Enum.GetValues(typeof(SourceFileFixTypes)).Length];

            for (int i = 0; i < FixOrderPriority.GetLength(0); i++)
            {
                for (int j = 0; j < FixOrderPriority.GetLength(1); j++)
                {
                    FixOrderPriority[i, j] = 99;
                }
            }

            foreach (KeyValuePair<DestinationFileFixTypes, FixPriorityStyle[]> p in MasterPriorityList)
            {
                for (int i = 0; i < p.Value.Length; i++)
                {
                    FixPriorityStyle v = p.Value[i];
                    FixOrderPriority[(int)p.Key, (int)v.SourceType] = i;
                    FixOrderStyle[(int)p.Key, (int)v.SourceType] = v.FixStyle;
                }
            }

            for (int i = 0; i < FixOrderPriority.GetLength(0); i++)
            {
                for (int j = 0; j < FixOrderPriority.GetLength(1); j++)
                {
                    if (FixOrderPriority[i, j] == 99)
                        Debug.WriteLine($"Missing value for {(DestinationFileFixTypes)i} , {(SourceFileFixTypes)j}");
                }
            }

        }

        public static RvFile[] FindSourceToUseForFix(RvFile FixZip, RvFile fixZippedFile, List<RvFile> fixFiles, out FixStyle fixStyle)
        {
            if (FixOrderPriority == null)
                SetFixOrderSettings();

            if (DBHelper.IsZeroLengthFile(fixZippedFile))
            {
                fixStyle = FixStyle.Zero;
                return null;
            }

            DestinationFileFixTypes destType = GetDestinationFileType(FixZip, fixZippedFile);

            int best = 99;
            List<RvFile> fixWith = new List<RvFile>();
            fixStyle = FixStyle.DecompressRecompress;
            foreach (RvFile fFile in fixFiles)
            {
                SourceFileFixTypes fsft = GetSourceFileType(fFile);

                int thisPriority = FixOrderPriority[(int)destType, (int)fsft];
                if (thisPriority < best)
                {
                    best = thisPriority;
                    fixStyle = FixOrderStyle[(int)destType, (int)fsft];
                    fixWith.Clear();
                    fixWith.Add(fFile);
                }
                else if (thisPriority == best)
                {
                    fixWith.Add(fFile);
                }
            }

            if (Settings.rvSettings.FixLevel == EFixLevel.Level1)
            {
                // FixStyle.RawCopy is ok, even if not verified
                if (fixStyle == FixStyle.RawCopyOnceVerified)
                    fixStyle = FixStyle.RawCopy;
            }
            //if (Settings.rvSettings.FixLevel==EFixLevel.Level2) nothing to do

            if (Settings.rvSettings.FixLevel == EFixLevel.Level3)
            {
                // never raw copy
                if (fixStyle == FixStyle.RawCopy)
                    fixStyle = FixStyle.DecompressRecompress;
            }


            if (fixStyle == FixStyle.RawCopyOnceVerified)    // RawCopyOnceVerifed does not yet exist , so use DecompressRecompress
                fixStyle = FixStyle.DecompressRecompress;
            if (fixStyle == FixStyle.Compress)             // Compress does not really exist, so use DecompressRecompress
                fixStyle = FixStyle.DecompressRecompress;


            return fixWith.ToArray();
        }




        private static DestinationFileFixTypes GetDestinationFileType(RvFile parent, RvFile file)
        {
            if (file.FileType == FileType.File)
                return DestinationFileFixTypes.File;

            if (file.FileType == FileType.FileZip || file.FileType == FileType.FileSevenZip)
            {
                ZipStructure destFileStruct = parent.newZipStruct;

                if (parent.FileType == FileType.SevenZip && (destFileStruct == ZipStructure.None || destFileStruct == ZipStructure.SevenZipTrrnt))
                    destFileStruct = Settings.rvSettings.getDefault7ZStruct;

                switch (destFileStruct)
                {
                    case ZipStructure.None: // none for 7z has been converted to SevenZipNZSTD
                        return DestinationFileFixTypes.Zip;
                    case ZipStructure.ZipTrrnt:
                        return DestinationFileFixTypes.ZipTrrnt;
                    case ZipStructure.ZipTDC:
                        return DestinationFileFixTypes.ZipTDC;
                    case ZipStructure.ZipZSTD:
                        return DestinationFileFixTypes.ZipZSTD;
                    case ZipStructure.SevenZipSLZMA:
                        return DestinationFileFixTypes.SevenZipSLZMA_MF;
                    case ZipStructure.SevenZipNLZMA:
                        return DestinationFileFixTypes.SevenZipNLZMA;
                    case ZipStructure.SevenZipSZSTD:
                        return DestinationFileFixTypes.SevenZipSZSTD_MF;
                    case ZipStructure.SevenZipNZSTD:
                        return DestinationFileFixTypes.SevenZipNZSTD;
                    default:
                        throw new InvalidOperationException($"Cannot figure out fix file type {parent.ZipDatStruct}");
                }

            }
            throw new InvalidOperationException($"Cannot figure out fix file type {file.FileType}");

        }

        private static bool IsSingleFile(RvFile file)
        {
            int childCount = 0;
            for (int i = 0; i < file.ChildCount; i++)
            {
                GotStatus gStatus = file.Child(i).GotStatus;
                if (gStatus != GotStatus.Got && gStatus != GotStatus.Corrupt)
                    continue;
                childCount++;
                if (childCount > 1)
                    return false;
            }
            return childCount == 1;
        }

        private static SourceFileFixTypes GetSourceFileType(RvFile file)
        {
            bool isVerified = file.IsDeepScanned;
            switch (file.FileType)
            {
                case FileType.File:
                    return SourceFileFixTypes.File_V;

                case FileType.FileZip:
                    {
                        if (file.Parent.ZipStruct == ZipStructure.ZipTrrnt)
                        {
                            return isVerified ? SourceFileFixTypes.ZipTrrnt_V : SourceFileFixTypes.ZipTrrnt_U;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.ZipTDC)
                        {
                            return isVerified ? SourceFileFixTypes.ZipTDC_V : SourceFileFixTypes.ZipTDC_U;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.ZipZSTD)
                        {
                            return isVerified ? SourceFileFixTypes.ZipZSTD_V : SourceFileFixTypes.ZipZSTD_U;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.None)
                        {
                            return isVerified ? SourceFileFixTypes.Zip_U : SourceFileFixTypes.Zip_V;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cannot figure out fix file type {file.Parent.ZipStruct}");
                        }
                    }
                case FileType.FileSevenZip:
                    {
                        bool SingleFileInArchive = IsSingleFile(file.Parent);
                        if (file.Parent.ZipStruct == ZipStructure.SevenZipSLZMA)
                        {
                            return SingleFileInArchive
                                ? isVerified ? SourceFileFixTypes.SevenZipSLZMA_V_SF : SourceFileFixTypes.SevenZipSLZMA_U_SF
                                : isVerified ? SourceFileFixTypes.SevenZipSLZMA_V_MF : SourceFileFixTypes.SevenZipSLZMA_U_MF;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.SevenZipNLZMA)
                        {
                            return isVerified ? SourceFileFixTypes.SevenZipNLZMA_V : SourceFileFixTypes.SevenZipNLZMA_U;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.SevenZipSZSTD)
                        {
                            return SingleFileInArchive
                                ? isVerified ? SourceFileFixTypes.SevenZipSZSTD_V_SF : SourceFileFixTypes.SevenZipSZSTD_U_SF
                                : isVerified ? SourceFileFixTypes.SevenZipSZSTD_V_MF : SourceFileFixTypes.SevenZipSZSTD_U_MF;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.SevenZipNZSTD)
                        {
                            return isVerified ? SourceFileFixTypes.SevenZipNZSTD_V : SourceFileFixTypes.SevenZipNZSTD_U;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.SevenZipTrrnt)
                        {
                            return SingleFileInArchive
                                ? isVerified ? SourceFileFixTypes.SevenZipTrrnt_V_SF : SourceFileFixTypes.SevenZipTrrnt_U_SF
                                : isVerified ? SourceFileFixTypes.SevenZipTrrnt_V_MF : SourceFileFixTypes.SevenZipTrrnt_U_MF;
                        }
                        else if (file.Parent.ZipStruct == ZipStructure.None)
                        {
                            return SingleFileInArchive
                                ? isVerified ? SourceFileFixTypes.SevenZip_V_SF : SourceFileFixTypes.SevenZip_U_SF
                                : isVerified ? SourceFileFixTypes.SevenZip_V_MF : SourceFileFixTypes.SevenZip_U_MF;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cannot figure out fix file type {file.Parent.ZipStruct}");
                        }
                    }
                default:
                    throw new InvalidOperationException($"Cannot figure out fix file type {file.FileType}");
            }
        }

    }
}
