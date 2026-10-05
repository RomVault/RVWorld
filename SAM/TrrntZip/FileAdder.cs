using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RVIO;

namespace TrrntZip
{
    public delegate void UpdateFileCount(int fileCount);

    public class FileAdder
    {
        private readonly string[] _file;
        private readonly UpdateFileCount _updateFileCount;
        private readonly BlockingCollection<cFile> _fileCollection;
        private readonly ProcessFileEndCallback _processFileEndCallBack;
        private readonly Settings _settings;
        private readonly PauseCancel _pc;


        public FileAdder(BlockingCollection<cFile> fileCollectionIn, string[] file, UpdateFileCount updateFileCount, ProcessFileEndCallback ProcessFileEndCallBack, Settings settings, PauseCancel pc)
        {
            _fileCollection = fileCollectionIn;
            _file = file;
            _updateFileCount = updateFileCount;
            _processFileEndCallBack = ProcessFileEndCallBack;
            _settings = settings;
            _pc = pc;
        }

        public void ProcFiles()
        {
            //fileCount = 0;

            // Flag the queue as busy while this scan runs, so the UI does not go
            // idle between finding files.
            MainQueue.BeginProducing();
            try
            {
                foreach (string t in _file)
                {
                    if (File.Exists(t) && AddFile(t))
                    {
                        cFile cf = new cFile() { fileId = MainQueue.fileCount++, filename = t, settings = _settings };
                        _fileCollection.Add(cf);
                    }
                }
                _updateFileCount?.Invoke(MainQueue.fileCount);

                foreach (string t in _file)
                {
                    if (Directory.Exists(t))
                    {
                        if (_settings.InZip == InputZipType.Directory)
                        {

                            cFile cf = new cFile() { fileId = MainQueue.fileCount++, filename = t, isDir = true, settings = _settings };
                            _fileCollection.Add(cf);
                        }
                        else
                            AddDirectory(t);
                    }
                }
                _processFileEndCallBack?.Invoke(-1, 0, TrrntZipStatus.Unknown, Compress.StructuredZip.ZipStructure.None);
            }
            finally
            {
                MainQueue.EndProducing();
            }
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
                if (_settings.InZip == InputZipType.Zip || _settings.InZip == InputZipType.Archive || _settings.InZip == InputZipType.All)
                {
                    return true;
                }
            }

            if (extn == ".7z")
            {
                if (_settings.InZip == InputZipType.SevenZip || _settings.InZip == InputZipType.Archive || _settings.InZip == InputZipType.All)
                {
                    return true;
                }
            }

            if (_settings.InZip == InputZipType.File || _settings.InZip == InputZipType.All)
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
                if (_pc != null)
                {
                    _pc.WaitOne();
                    if (_pc.Cancelled)
                        return;
                }

                if (AddFile(t.FullName))
                {
                    cFile cf = new cFile() { fileId = MainQueue.fileCount++, filename = t.FullName, settings = _settings };
                    _fileCollection.Add(cf);
                }
            }
            _updateFileCount?.Invoke(MainQueue.fileCount);

            List<DirectoryInfo> diChild = di.GetDirectories().ToList();
            diChild.Sort((x, y) => string.Compare(x.FullName, y.FullName, StringComparison.Ordinal));
            foreach (DirectoryInfo t in diChild)
            {
                if (_pc != null)
                {
                    _pc.WaitOne();
                    if (_pc.Cancelled)
                        return;
                }

                AddDirectory(t.FullName);
            }
        }
    }
}
