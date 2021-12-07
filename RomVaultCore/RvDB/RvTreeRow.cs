using System.IO;
using System.Text;

namespace RomVaultCore.RvDB
{
    public class RvTreeRow
    {
        public enum TreeSelect
        {
            UnSelected,
            Selected,
            Locked
        }

        private long _filePointer = -1;
        private bool _pTreeExpanded;
        private TreeSelect _pChecked;

        public object UiObject;

        public RvTreeRow()
        {
            _pTreeExpanded = true;
            _pChecked = TreeSelect.Selected;
        }

        public bool TreeExpanded
        {
            get => _pTreeExpanded;
        }

        public void SetTreeExpanded(bool value, bool CoreActive)
        {
            if (_pTreeExpanded == value) return;
            _pTreeExpanded = value;

            if (!CoreActive)
                CacheUpdate();
        }

        public TreeSelect Checked
        {
            get => _pChecked;
        }

        public void SetChecked(TreeSelect value, bool CoreActive)
        {
            if (_pChecked == value) return;
            _pChecked = value;
            if (!CoreActive)
            CacheUpdate();
        }

        public void Write(BinaryWriter bw)
        {
            _filePointer = bw.BaseStream.Position;
            bw.Write(_pTreeExpanded);
            bw.Write((byte)_pChecked);
        }

        public void Read(BinaryReader br)
        {
            _filePointer = br.BaseStream.Position;
            _pTreeExpanded = br.ReadBoolean();
            _pChecked = (TreeSelect)br.ReadByte();
        }


        private static FileStream fsl;
        private static BinaryWriter bwl;

        public static void OpenStream()
        {
            if (!RVIO.File.Exists(Settings.rvSettings.CacheFile))
            {
                fsl = null;
                bwl = null;
                return;
            }
            fsl = new FileStream(Settings.rvSettings.CacheFile, FileMode.Open, FileAccess.Write);
            bwl = new BinaryWriter(fsl, Encoding.UTF8, true);
        }

        public static void CloseStream()
        {
            bwl?.Flush();
            bwl?.Close();
            bwl?.Dispose();
            fsl?.Close();
            fsl?.Dispose();
            bwl = null;
            fsl = null;
        }

        private void CacheUpdate()
        {
            if (_filePointer < 0)
                return;


            if (fsl != null && bwl != null)
            {
                fsl.Position = _filePointer;
                bwl.Write(_pTreeExpanded);
                bwl.Write((byte)_pChecked);
                return;
            }

            using (FileStream fs = new FileStream(Settings.rvSettings.CacheFile, FileMode.Open, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8, true))
                {
                    fs.Position = _filePointer;
                    bw.Write(_pTreeExpanded);
                    bw.Write((byte)_pChecked);

                    bw.Flush();
                    bw.Close();
                }

                fs.Close();
            }
        }
    }
}