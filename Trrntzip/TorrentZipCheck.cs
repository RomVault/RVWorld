using Compress.StructuredZip;
using SortMethods;
using System;
using System.Collections.Generic;

/***************************************************************************************************************

    Rules to be checked in a torrentzip file

    1)  Directory separator should be a '/'   
        a '\' is invalid and should be replaced with '/'
    
    2)  All Files in a torrentzip should be sorted with a lowercase file compare.

    3)  Directory Marker files are only needed if they are empty directories.

    4)  Also check for repeating files. (This is just a bad thing, which should never happen in a zip file)

***************************************************************************************************************/

namespace TrrntZip
{
    public static class TorrentZipCheck
    {
        public static TrrntZipStatus CheckZipFiles(ref List<ZippedFile> zippedFiles, int threadId, LogCallback statusLogCallBack)
        {
            TrrntZipStatus tzStatus = TrrntZipStatus.Unknown;


            // ***************************** RULE 1 *************************************
            // Directory separator should be a '/' a '\' is invalid and should be replaced with '/'
            //
            // check if any '\' = 92 need converted to '/' = 47
            // this needs done before the sort, so that the sort is correct.
            // return BadDirectorySeparator if errors found.
            bool error1 = false;
            foreach (ZippedFile t in zippedFiles)
            {
                char[] bytes = t.Name.ToCharArray();
                bool fixDir = false;
                for (int j = 0; j < bytes.Length; j++)
                {
                    if (bytes[j] != 92)
                        continue;

                    fixDir = true;
                    bytes[j] = (char)47;
                    tzStatus |= TrrntZipStatus.BadDirectorySeparator;
                    if (!error1 && Program.VerboseLogging)
                    {
                        error1 = true;
                        statusLogCallBack?.Invoke(threadId, "Incorrect directory separator found");
                    }
                }
                if (fixDir)
                    t.Name = new string(bytes);
            }


            // ***************************** RULE 2 *************************************
            // All Files in a torrentzip should be sorted with a lower case file compare.
            //
            // if needed sort the files correctly, and return Unsorted if errors found.
            bool error2 = false;

            for (int i = 0; i < zippedFiles.Count - 1; i++)
            {
                int c = TrrntZipStringCompare(zippedFiles[i], zippedFiles[i + 1]);
                if (c > 0)
                {
                    tzStatus |= TrrntZipStatus.Unsorted;
                    error2 = true;
                    if (Program.VerboseLogging)
                        statusLogCallBack?.Invoke(threadId, "Incorrect file order found");

                    break;
                }
            }
            if (error2) // we need to sort the list so sort it.
                zippedFiles = StorageList.FastArraySort.SortList(zippedFiles, TrrntZipStringCompare);


            // ***************************** RULE 3 *************************************
            // Directory marker files are only needed if they are empty directories.
            //
            // now that the files are sorted correctly, we can see if there are unneeded
            // directory files, by first finding directory files (these end in a '\' character ascii 92)
            // and then checking if the next file is a file in that found directory.
            // If we find this 2 entry pattern (directory followed by file in that directory)
            // then the directory entry should not be present and the torrentzip is incorrect.
            // return ExtraDirectoryEnteries if error is found. 
            bool error3 = false;
            for (int i = 0; i < zippedFiles.Count - 1; i++)
            {
                // check if this is a directory entry
                if (zippedFiles[i].Name[zippedFiles[i].Name.Length - 1] != 47)
                    continue;

                // check if the next filename is shorter or equal to this filename.
                // if it is shorter or equal it cannot be a file in the directory.
                if (zippedFiles[i + 1].Name.Length <= zippedFiles[i].Name.Length)
                    continue;

                // check if the directory part of the two file enteries match
                // if they do we found an incorrect directory entry.
                bool delete = true;
                for (int j = 0; j < zippedFiles[i].Name.Length; j++)
                {
                    if (zippedFiles[i].Name[j] != zippedFiles[i + 1].Name[j])
                    {
                        delete = false;
                        break;
                    }
                }

                // we found an incorrect directory so remove it.
                if (delete)
                {
                    zippedFiles.RemoveAt(i);
                    tzStatus |= TrrntZipStatus.ExtraDirectoryEnteries;
                    if (!error3 && Program.VerboseLogging)
                    {
                        error3 = true;
                        statusLogCallBack?.Invoke(threadId, "Un-needed directory records found");
                    }

                    i--;
                }
            }


            // check for repeat files
            bool error4 = false;
            for (int i = 0; i < zippedFiles.Count - 1; i++)
            {
                if (zippedFiles[i].Name == zippedFiles[i + 1].Name)
                {
                    tzStatus |= TrrntZipStatus.RepeatFilesFound;
                    if (!error4 && Program.VerboseLogging)
                    {
                        error4 = true;
                        statusLogCallBack?.Invoke(threadId, "Duplcate file enteries found");
                    }
                }
            }

            return tzStatus;
        }






        public static TrrntZipStatus CheckSevenZipFiles(ref List<ZippedFile> zippedFiles, int threadId, LogCallback statusLogCallBack)
        {
            TrrntZipStatus tzStatus = TrrntZipStatus.Unknown;

            // ***************************** RULE 1 *************************************
            // Directory separator should be a '/' a '\' is invalid and should be replaced with '/'
            //
            // check if any '\' = 92 need converted to '/' = 47
            // this needs done before the sort, so that the sort is correct.
            // return BadDirectorySeparator if errors found.
            bool error1 = false;
            foreach (ZippedFile t in zippedFiles)
            {
                char[] bytes = t.Name.ToCharArray();
                bool fixDir = false;
                for (int j = 0; j < bytes.Length; j++)
                {
                    if (bytes[j] != 92)
                        continue;

                    fixDir = true;
                    bytes[j] = (char)47;
                    tzStatus |= TrrntZipStatus.BadDirectorySeparator;
                    if (!error1 && Program.VerboseLogging)
                    {
                        error1 = true;
                        statusLogCallBack?.Invoke(threadId, "Incorrect directory separator found");
                    }
                }
                if (fixDir)
                    t.Name = new string(bytes);
            }




            // ***************************** RULE 3 *************************************
            // Directory marker files are only needed if they are empty directories.
            //
            // now that the files are sorted correctly, we can see if there are unneeded
            // directory files, by first finding directory files (these end in a '\' character ascii 92)
            // and then checking if the next file is a file in that found directory.
            // If we find this 2 entry pattern (directory followed by file in that directory)
            // then the directory entry should not be present and the torrentzip is incorrect.
            // return ExtraDirectoryEnteries if error is found. 
            List<ZippedFile> dirSortTest = StorageList.FastArraySort.SortList(zippedFiles, NameSort);

            bool error3 = false;
            for (int i = 0; i < dirSortTest.Count - 1; i++)
            {
                // check if this is a directory entry
                if (dirSortTest[i].Name[dirSortTest[i].Name.Length - 1] != 47)
                    continue;

                // check if the next filename is shorter or equal to this filename.
                // if it is shorter or equal it cannot be a file in the directory.
                if (dirSortTest[i + 1].Name.Length <= dirSortTest[i].Name.Length)
                    continue;

                // check if the directory part of the two file enteries match
                // if they do we found an incorrect directory entry.
                bool delete = true;
                for (int j = 0; j < dirSortTest[i].Name.Length; j++)
                {
                    if (dirSortTest[i].Name[j] != dirSortTest[i + 1].Name[j])
                    {
                        delete = false;
                        break;
                    }
                }

                // we found an incorrect directory so remove it.
                if (delete)
                {
                    for (int k = 0; k < zippedFiles.Count; k++)
                    {
                        if (zippedFiles[k] == dirSortTest[i])
                        {
                            zippedFiles.RemoveAt(k);
                            k--;
                        }
                    }
                    dirSortTest.RemoveAt(i);
                    tzStatus |= TrrntZipStatus.ExtraDirectoryEnteries;
                    if (!error3 && Program.VerboseLogging)
                    {
                        error3 = true;
                        statusLogCallBack?.Invoke(threadId, "Un-needed directory records found");
                    }

                    i--;
                }
            }



            // ***************************** RULE 2 *************************************
            // All Files in a torrentzip should be sorted by extention
            //
            // if needed sort the files correctly, and return Unsorted if errors found.
            bool error2 = false;
            for (int i = 0; i < zippedFiles.Count - 1; i++)
            {
                int c = Trrnt7ZipStringCompare(zippedFiles[i], zippedFiles[i + 1]);
                if (c > 0)
                {

                    tzStatus |= TrrntZipStatus.Unsorted;
                    error2 = true;
                    if (Program.VerboseLogging)
                        statusLogCallBack?.Invoke(threadId, "Incorrect file order found");

                    break;
                }

            }
            if (error2) // we need to sort the list so sort it.
                zippedFiles = StorageList.FastArraySort.SortList(zippedFiles, Trrnt7ZipStringCompare);


            // check for repeat files
            bool error4 = false;
            for (int i = 0; i < zippedFiles.Count - 1; i++)
            {
                if (zippedFiles[i].Name == zippedFiles[i + 1].Name)
                {
                    tzStatus |= TrrntZipStatus.RepeatFilesFound;
                    if (!error4 && Program.VerboseLogging)
                    {
                        error4 = true;
                        statusLogCallBack?.Invoke(threadId, "Duplcate file enteries found");
                    }
                }
            }


            return tzStatus;
        }

        public static int NameSort(ZippedFile z0, ZippedFile z1)
        {
            return Sorters.StringCompare(z0.Name, z1.Name);
        }
        private static int TrrntZipStringCompare(ZippedFile z1, ZippedFile z2)
        {
            return Sorters.TrrntZipStringCompare(z1.Name, z2.Name);
        }

        public static int Trrnt7ZipStringCompare(ZippedFile z0, ZippedFile z1)
        {
            return Sorters.Trrnt7ZipStringCompare(z0.Name, z1.Name);
        }
    }
}