using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RVIO;
using TrrntZip;

namespace TrrntZipUI
{
    public delegate void UpdateFileCount(int fileCount);

    public class FileAdder
    {
        private readonly string[] _file;
        private readonly UpdateFileCount _updateFileCount;
        private readonly BlockingCollection<cFile> _fileCollection;
        private readonly ProcessFileEndCallback _processFileEndCallBack;

        private int fileCount;

        public FileAdder(BlockingCollection<cFile> fileCollectionIn, string[] file, UpdateFileCount updateFileCount, ProcessFileEndCallback ProcessFileEndCallBack)
        {
            _fileCollection = fileCollectionIn;
            _file = file;
            _updateFileCount = updateFileCount;
            _processFileEndCallBack = ProcessFileEndCallBack;
        }

        public void ProcFiles()
        {
            fileCount = 0;

            foreach (string t in _file)
            {
                if (File.Exists(t) && AddFile(t))
                {
                    cFile cf = new cFile() { fileId = fileCount++, filename = t };
                    _fileCollection.Add(cf);
                }
            }
            _updateFileCount?.Invoke(fileCount);

            foreach (string t in _file)
            {
                if (Directory.Exists(t))
                {
                    if (Program.InZip == zipType.dir)
                    {

                        cFile cf = new cFile() { fileId = fileCount++, filename = t, isDir = true };
                        _fileCollection.Add(cf);
                    }
                    else
                        AddDirectory(t);
                }
            }
            _processFileEndCallBack?.Invoke(-1, 0, TrrntZipStatus.Unknown);
        }

        private bool AddFile(string filename)
        {
            string extn = Path.GetExtension(filename);
            extn = extn.ToLower();

            if (extn == ".tztmp" && Path.GetFileName(filename).StartsWith("__"))
            {
                File.Delete(filename);
                return false;
            }

            if (extn == ".zip")
            {
                if (Program.InZip == zipType.zip || Program.InZip == zipType.archive || Program.InZip == zipType.all)
                {
                    return true;
                }
            }

            if (extn == ".7z")
            {
                if (Program.InZip == zipType.sevenzip || Program.InZip == zipType.archive || Program.InZip == zipType.all)
                {
                    return true;
                }
            }

            if (Program.InZip == zipType.file || Program.InZip == zipType.all)
            {
                return true;
            }
            return false;
        }

        private void AddDirectory(string directory)
        {
            DirectoryInfo di = new DirectoryInfo(directory);

            List<string> lstFile = new List<string>();
            List<FileInfo> fi = di.GetFiles().ToList();
            fi.Sort((x, y) => string.Compare(x.FullName, y.FullName, StringComparison.Ordinal));

            foreach (FileInfo t in fi)
            {
                if (AddFile(t.FullName))
                {
                    cFile cf = new cFile() { fileId = fileCount++, filename = t.FullName };
                    _fileCollection.Add(cf);
                }
            }
            _updateFileCount?.Invoke(fileCount);

            List<DirectoryInfo> diChild = di.GetDirectories().ToList();
            diChild.Sort((x, y) => string.Compare(x.FullName, y.FullName, StringComparison.Ordinal));
            foreach (DirectoryInfo t in diChild)
            {
                AddDirectory(t.FullName);
            }
        }

    }
}
