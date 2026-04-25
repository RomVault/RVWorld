using RVIO;

namespace Compress.ZipFile
{
    public partial class Zip
    {

        internal void zipFileCloseRead()
        {
            if (_zipFs != null)
            {
                _zipFs.Close();
                _zipFs.Dispose();
                _zipFs = null;
            }
            ZipOpen = ZipOpenType.Closed;
        }

        internal void zipFileCloseWrite()
        {
            if (_zipFs != null)
            {
                _zipFs.SetLength(_zipFs.Position);
                _zipFs.Flush();
                _zipFs.Close();
                _zipFs.Dispose();
                _zipFs = null;
            }
            _zipFileInfo = _zipFileInfo == null ? null : new FileInfo(_zipFileInfo.FullName);

            ZipOpen = ZipOpenType.Closed;
        }




        public void ZipFileCloseFailed()
        {
            try
            {
                switch (ZipOpen)
                {
                    case ZipOpenType.Closed:
                        return;
                    case ZipOpenType.OpenRead:
                        if (_zipFs != null)
                        {
                            try { _zipFs?.Close(); } catch { }
                            try { _zipFs?.Dispose(); } catch { }
                        }
                        break;
                    case ZipOpenType.OpenWrite:
                        if (_zipFs != null)
                        {
                            try { _zipFs?.Flush(); } catch { }
                            try { _zipFs?.Close(); } catch { }
                            try { _zipFs?.Dispose(); } catch { }
                        }
                        if (_zipFileInfo != null)
                        {
                            try { RVIO.File.Delete(_zipFileInfo.FullName); } catch { }
                            _zipFileInfo = null;
                        }
                        break;
                }
            }
            finally
            {
                _zipFs = null;
                ZipOpen = ZipOpenType.Closed;
            }
        }

    }
}
