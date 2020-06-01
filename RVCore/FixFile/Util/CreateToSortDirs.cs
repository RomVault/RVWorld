using System.Collections.Generic;
using RVCore.RvDB;
using RVIO;

namespace RVCore.FixFile.Util
{
    public static partial class FixFileUtils
    {
        public static ReturnCode CreateToSortDirs(RvFile inFile, out RvFile outDir, out string filename)
        {
            List<RvFile> dirTree = new List<RvFile>();

            RvFile rFile = inFile;
            while (rFile.Parent != null)
            {
                rFile = rFile.Parent;
                dirTree.Insert(0,rFile);
            }
            dirTree.RemoveAt(0);

            RvFile toSort = DB.RvFileToSort();
            if (!Directory.Exists(toSort.FullName))
            {
                outDir = null;
                filename = null;
                return ReturnCode.ToSortNotFound;
            }
            dirTree[0] = toSort;
            int dirTreeCount = dirTree.Count;

            for (int i = 1; i < dirTreeCount; i++)
            {
                RvFile baseDir = dirTree[i - 1];
                RvFile thisDir = dirTree[i];

                RvFile tDir = new RvFile(FileType.Dir)
                {
                    Name = thisDir.Name,
                    DatStatus = DatStatus.InToSort
                };
                int found=baseDir.ChildNameSearch(tDir, out int index);
                if (found==0)
                {
                    tDir = baseDir.Child(index);
                }
                else
                {
                    baseDir.ChildAdd(tDir,index);
                }

                string fullpath = tDir.FullName;
                if (!Directory.Exists(fullpath))
                {
                    Directory.CreateDirectory(fullpath);
                    DirectoryInfo di =new DirectoryInfo(fullpath);
                    tDir.SetStatus(DatStatus.InToSort,GotStatus.Got);
                    tDir.FileModTimeStamp = di.LastWriteTime;
                }

                dirTree[i] = tDir;
            }

            outDir = dirTree[dirTreeCount - 1];
            filename=inFile.Name;
       
            string toSortFullName = Path.Combine(outDir.FullName, filename);
            int fileC = 0;
            string name=null, ext=null;

            while (File.Exists(toSortFullName))
            {
                if (name == null)
                {
                    int pIndex = inFile.Name.LastIndexOf('.');
                    if (pIndex >= 0)
                    {
                        name = inFile.Name.Substring(0, pIndex);
                        ext = inFile.Name.Substring(pIndex + 1);
                    }
                    else
                    {
                        name = inFile.Name;
                        ext = "";
                    }
                }

                filename = name + "_" + fileC + "." + ext;
                toSortFullName = Path.Combine(outDir.FullName, filename);
                fileC += 1;
            }

            return ReturnCode.Good;
        }
    }
}
