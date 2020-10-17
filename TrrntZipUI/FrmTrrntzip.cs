using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using RVIO;
using Trrntzip;

namespace TrrntZipUI
{

    public partial class FrmTrrntzip : Form
    {
        private delegate void StatusInvoker(int fileId, int processId, string filename);

        private readonly FileList _fileList;
        private int _fileIndex;

        private int _threadCount;

        private readonly List<Label> _threadLabel;
        private readonly List<ProgressBar> _threadProgress;

        private bool _working;
        private int _threadsBusyCount;

        private bool UiUpdate = false;

        private bool Cancel = false;
        private bool Pause = false;

        public FrmTrrntzip()
        {

            UiUpdate = true;
            InitializeComponent();

            this.Text = $"Trrntzip .Net ({Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}) - Powered by RomVault";
            DropBox.AllowDrop = true;
            DropBox.DragEnter += PDragEnter;
            DropBox.DragDrop += PDragDrop;

            string sval = AppSettings.ReadSetting("InZip");
            if (!int.TryParse(sval, out int intVal))
            {
                intVal = 2;
            }
            cboInType.SelectedIndex = intVal;

            sval = AppSettings.ReadSetting("OutZip");
            if (!int.TryParse(sval, out intVal))
            {
                intVal = 0;
            }
            cboOutType.SelectedIndex = intVal;

            sval = AppSettings.ReadSetting("Force");
            chkForce.Checked = sval == "True";

            sval = AppSettings.ReadSetting("Fix");
            chkFix.Checked = sval != "False";

            tbProccessors.Minimum = 1;
            tbProccessors.Maximum = Environment.ProcessorCount;
            sval = AppSettings.ReadSetting("ProcCount");
            if (!int.TryParse(sval, out int procc))
            {
                procc = tbProccessors.Maximum;
            }

            if (procc > tbProccessors.Maximum)
            {
                procc = tbProccessors.Maximum;
            }

            tbProccessors.Value = procc;

            _fileList = new FileList();

            _threadLabel = new List<Label>();
            _threadProgress = new List<ProgressBar>();

            SetUpUiThreads();
            UiUpdate = false;
        }

        private void SetUpUiThreads()
        {
            _threadCount = tbProccessors.Value;

            foreach (Label t in _threadLabel)
            {
                StatusPanel.Controls.Remove(t);
                t.Dispose();
            }
            foreach (ProgressBar p in _threadProgress)
            {
                StatusPanel.Controls.Remove(p);
                p.Dispose();
            }

            _threadLabel.Clear();
            _threadProgress.Clear();
            for (int i = 0; i < _threadCount; i++)
            {
                Label pLabel = new Label();
                _threadLabel.Add(pLabel);
                pLabel.Visible = true;
                pLabel.Left = 12;
                pLabel.Top = 235 + 30 * i;
                pLabel.Width = 225;
                pLabel.Height = 15;
                pLabel.Text = $"Processor {i + 1}";
                StatusPanel.Controls.Add(pLabel);

                ProgressBar pProgress = new ProgressBar();
                _threadProgress.Add(pProgress);
                pProgress.Visible = true;
                pProgress.Left = 12;
                pProgress.Top = 250 + 30 * i;
                pProgress.Width = 225;
                pProgress.Height = 12;
                StatusPanel.Controls.Add(pProgress);
            }

            if (Height < 325 + 30 * _threadCount)
            {
                Height = 325 + 30 * _threadCount;
            }
        }

        private static void PDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void PDragDrop(object sender, DragEventArgs e)
        {
            string[] file = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!_working)
            {
                dataGrid.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
                dataGrid.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;

                _fileIndex = 0;
                _fileList.Clear();
                dataGrid.Rows.Clear();

                Trrntzip.Program.ForceReZip = chkForce.Checked;
                Trrntzip.Program.CheckOnly = !chkFix.Checked;
                Trrntzip.Program.InZip = (zipType)cboInType.SelectedIndex;
                Trrntzip.Program.OutZip = (zipType)cboOutType.SelectedIndex;
            }

            foreach (string t in file)
            {
                if (File.Exists(t))
                {
                    AddFile(t);
                }

                if (Directory.Exists(t))
                {
                    AddDirectory(t);
                }
            }

            int startRow = dataGrid.Rows.Count;

            for (int i = startRow; i < _fileList.Count(); i++)
            {
                dataGrid.Rows.Add();
                int iRow = dataGrid.Rows.Count - 1;

                dataGrid.Rows[iRow].Selected = false;
                dataGrid.Rows[iRow].Cells[0].Value = _fileList.Get(i).Filename;
            }

            lblTotalStatus.Text = @"( " + _fileIndex + @" / " + _fileList.Count() + @" )";


            if (_fileList.Count() == 0)
            {
                return;
            }
            if (_working)
            {
                ProcessZipsStartThreads();
                return;
            }

            StartWorking();

            ProcessZipsStartThreads();
        }

        private void AddFile(string filename)
        {
            string extn = Path.GetExtension(filename);
            extn = extn.ToLower();
            if ((extn != ".zip") && (extn != ".7z"))
            {
                return;
            }

            if ((extn == ".zip") && (Trrntzip.Program.InZip == zipType.sevenzip))
            {
                return;
            }
            if ((extn == ".7z") && (Trrntzip.Program.InZip == zipType.zip))
            {
                return;
            }

            TzFile tmpFile = new TzFile(filename);
            int found = _fileList.Search(tmpFile, out int index);
            if (found != 0)
            {
                _fileList.Add(index, tmpFile);
            }
        }

        private void AddDirectory(string directory)
        {
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();
            foreach (FileInfo t in fi)
            {
                AddFile(t.FullName);
            }

            DirectoryInfo[] diChild = di.GetDirectories();
            foreach (DirectoryInfo t in diChild)
            {
                AddDirectory(t.FullName);
            }
        }


        private void StartWorking()
        {
            _working = true;
            //DropBox.Enabled = false;
            cboInType.Enabled = false;
            cboOutType.Enabled = false;
            chkForce.Enabled = false;
            chkFix.Enabled = false;
            tbProccessors.Enabled = false;
            btnCancel.Enabled = true;
            btnPause.Enabled = true;
            dataGrid.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
            dataGrid.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;
            Application.DoEvents();
        }

        private void StopWorking()
        {
            _working = false;
            //DropBox.Enabled = true;
            cboInType.Enabled = true;
            cboOutType.Enabled = true;
            chkForce.Enabled = true;
            chkFix.Enabled = true;
            tbProccessors.Enabled = true;
            btnCancel.Enabled = false;
            btnPause.Enabled = false;
            dataGrid.Columns[0].SortMode = DataGridViewColumnSortMode.Automatic;
            dataGrid.Columns[1].SortMode = DataGridViewColumnSortMode.Automatic;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_working)
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private List<bool> procStatus = new List<bool>();

        private void ProcessZipsStartThreads()
        {
            lock (_fileList)
            {
                _threadsBusyCount = _threadCount;
                for (int i = 0; i < _threadCount; i++)
                {
                    if (procStatus.Count <= i)
                        procStatus.Add(false);

                    if (procStatus[i])
                        continue;

                    CProcessZip cpz = new CProcessZip
                    {
                        ThreadId = i,
                        GetNextFileCallBack = GetNextFileCallback,
                        SetFileStatusCallBack = SetFileStatusCallback,
                        StatusCallBack = StatusCallBack
                    };

                    procStatus[i] = true;

                    Thread t = new Thread(cpz.MigrateZip);
                    t.Start();
                }
            }
        }

        private void GetNextFileCallback(int processId, out int fileId, out string filename)
        {
            lock (_fileList)
            {
                if (_fileIndex < _fileList.Count() && !(Cancel || Pause))
                {
                    fileId = _fileIndex;
                    filename = _fileList.Get(_fileIndex).Filename;
                    Invoke(new StatusInvoker(DoStatusUpdate), _fileIndex, processId, filename);
                    _fileIndex += 1;
                }
                else
                {
                    fileId = -1;
                    filename = "";
                    _threadsBusyCount--;
                    procStatus[processId] = false;

                    if (Cancel)
                        Invoke(new StatusInvoker(DoStatusUpdate), _fileIndex, processId, "Cancelled");
                    else if (Pause)
                        Invoke(new StatusInvoker(DoStatusUpdate), _fileIndex, processId, "Paused");
                    else
                        Invoke(new StatusInvoker(DoStatusUpdate), _fileList.Count(), processId, "Complete");
                }
            }
        }

        private void DoStatusUpdate(int fileId, int processId, string filename)
        {
            lblTotalStatus.Text = @"( " + fileId + @" / " + _fileList.Count() + @" )";
            _threadLabel[processId].Text = Path.GetFileName(filename);

            int topfileId = fileId;
            if (topfileId < dataGrid.Rows.Count && !(Cancel || Pause))
            {
                dataGrid.Rows[topfileId].Cells[1].Value = "Processing....(" + processId + ")";
            }

            topfileId -= (int)((double)dataGrid.Height / dataGrid.Rows[0].Height * 0.8);
            if (topfileId > dataGrid.Rows.Count)
            {
                topfileId = dataGrid.Rows.Count - 1;
            }
            if (topfileId < 0)
            {
                topfileId = 0;
            }
            dataGrid.FirstDisplayedScrollingRowIndex = topfileId;


            if (_threadsBusyCount != 0)
                return;

            // all threads have finished
            if (Pause)
            {
                // we finished due to a pause, so just re-enable the Pause (Resume) and Cancel buttons
                btnPause.Enabled = true;
                btnCancel.Enabled = true;
            }
            else
            {
                // if we did not Pause, then we either finished normally or we cancelled
                Cancel = false;
                StopWorking();
            }
        }


        private void SetFileStatusCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new SetFileStatusCallback(SetFileStatusCallback), processId, fileId, trrntZipStatus);
                return;
            }
            switch (trrntZipStatus)
            {
                case TrrntZipStatus.ValidTrrntzip:
                    dataGrid.Rows[fileId].Cells[1].Value = "Valid TrrntZip";
                    break;
                case TrrntZipStatus.Trrntzipped:
                    dataGrid.Rows[fileId].Cells[1].Value = "TrrntZipped";
                    break;
                default:
                    dataGrid.Rows[fileId].Cells[1].Value = trrntZipStatus.ToString();
                    break;
            }
        }

        private void StatusCallBack(int processId, int percent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new StatusCallback(StatusCallBack), processId, percent);
                return;
            }
            _threadProgress[processId].Value = percent;
        }

        private void picTitle_Click(object sender, EventArgs e)
        {
            clickDonate();
        }

        private void picDonate_Click(object sender, EventArgs e)
        {
            clickDonate();
        }
        private void clickDonate()
        {
            Process.Start("http://paypal.me/romvault");
        }

        private void picRomVault_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.romvault.com");
        }




        private void tbProccessors_ValueChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;

            AppSettings.AddUpdateAppSettings("ProcCount", tbProccessors.Value.ToString());
            SetUpUiThreads();
        }

        private void chkFix_CheckedChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            AppSettings.AddUpdateAppSettings("Fix", chkFix.Checked.ToString());
        }

        private void chkForce_CheckedChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            AppSettings.AddUpdateAppSettings("Force", chkForce.Checked.ToString());
        }

        private void cboInType_TextChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            AppSettings.AddUpdateAppSettings("InZip", cboInType.SelectedIndex.ToString());
        }

        private void cboOutType_TextChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            AppSettings.AddUpdateAppSettings("OutZip", cboOutType.SelectedIndex.ToString());
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            // if we cancelled after a pause then just stop
            if (Pause)
            {
                for (int i = 0; i < _threadCount; i++)
                {
                    _threadLabel[i].Text = "Cancelled";
                }

                Pause = false;
                StopWorking();
                return;
            }

            // start Cancel
            Cancel = true;
            btnCancel.Enabled = false;
            btnPause.Enabled = false;
        }

        private static Bitmap GetBitmap(string bitmapName)
        {
            object bmObj = rvImages11.ResourceManager.GetObject(bitmapName);

            Bitmap bm = null;
            if (bmObj != null)
            {
                bm = (Bitmap)bmObj;
            }

            return bm;
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmTrrntzip));
            if (!Pause)
            {
                // Pause
                btnPause.Image = GetBitmap("Resume");
                Pause = true;

                // disable the Pause and Cancel buttons until all tasks have finished the file they are working on.
                btnCancel.Enabled = false;
                btnPause.Enabled = false;
            }
            else
            {
                // Resume after a Pause
                btnPause.Image = GetBitmap("Pause");
                Pause = false;
                ProcessZipsStartThreads();
            }
        }
    }
}