using System.Collections.Concurrent;
using System.Collections.Generic;
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
                    if (TrrntZip.Program.InZip == zipType.dir)
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
                if (TrrntZip.Program.InZip == zipType.zip || TrrntZip.Program.InZip == zipType.archive || TrrntZip.Program.InZip == zipType.all)
                {
                    return true;
                }
            }

            if (extn == ".7z")
            {
                if (TrrntZip.Program.InZip == zipType.sevenzip || TrrntZip.Program.InZip == zipType.archive || TrrntZip.Program.InZip == zipType.all)
                {
                    return true;
                }
            }

            if (TrrntZip.Program.InZip == zipType.file || TrrntZip.Program.InZip == zipType.all)
            {
                return true;
            }
            return false;
        }

        private void AddDirectory(string directory)
        {
            DirectoryInfo di = new DirectoryInfo(directory);

            List<string> lstFile = new List<string>();
            FileInfo[] fi = di.GetFiles();

            foreach (FileInfo t in fi)
            {
                if (AddFile(t.FullName))
                {
                    cFile cf = new cFile() { fileId = fileCount++, filename = t.FullName };
                    _fileCollection.Add(cf);
                }
            }
            _updateFileCount?.Invoke(fileCount);

            DirectoryInfo[] diChild = di.GetDirectories();
            foreach (DirectoryInfo t in diChild)
            {
                AddDirectory(t.FullName);
            }
        }

    }
}
