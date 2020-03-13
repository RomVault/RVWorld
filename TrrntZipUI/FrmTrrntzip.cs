using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using RVIO;
using Trrntzip;

namespace TrrntZipUI
{
    public delegate void GetNextFileCallback(int threadId, out int fileId, out string filename);

    public delegate void SetFileStatusCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus);

    public partial class FrmTrrntzip : Form
    {
        private delegate void StatusInvoker(int fileId, int processId, string filename);

        private readonly FileList _fileList;
        private readonly Counter _fileIndex = new Counter();

        private int _threadCount;

        private readonly List<Label> _threadLabel;
        private readonly List<ProgressBar> _threadProgress;

        private bool _working;
        private int _threadsBusyCount;

        private bool UiUpdate = false;

        public FrmTrrntzip()
        {

            UiUpdate = true;
            InitializeComponent();

            this.Text = $"Trrntzip .Net ({Assembly.GetExecutingAssembly().GetName().Version.ToString(3)})";
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
            chkForce.Checked = sval=="True";

            sval = AppSettings.ReadSetting("Fix");
            chkFix.Checked = sval!="False";

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
                pLabel.Text = $"Processor {i+1}";
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
            Trrntzip.Program.ForceReZip = chkForce.Checked;
            Trrntzip.Program.CheckOnly = !chkFix.Checked;
            Trrntzip.Program.InZip = (zipType)cboInType.SelectedIndex;
            Trrntzip.Program.OutZip = (zipType)cboOutType.SelectedIndex;

            StartWorking();

            string[] file = (string[])e.Data.GetData(DataFormats.FileDrop);
            _fileList.Clear();

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

            dataGrid.Rows.Clear();
            for (int i = 0; i < _fileList.Count(); i++)
            {
                dataGrid.Rows.Add();
                int iRow = dataGrid.Rows.Count - 1;

                dataGrid.Rows[iRow].Selected = false;
                dataGrid.Rows[iRow].Cells[0].Value = _fileList.Get(i).Filename;
            }
            if (_fileList.Count() == 0)
            {
                StopWorking();
                return;
            }
            ProcessZipsStartThreads();
        }

        private void AddFile(string filename)
        {
            string extn = Path.GetExtension(filename);
            extn = extn.ToLower();
            if ((extn != ".zip") && (extn != ".7z") && (extn != ".iso"))
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
            int index;
            int found = _fileList.Search(tmpFile, out index);
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
            DropBox.Enabled = false;
            cboInType.Enabled = false;
            cboOutType.Enabled = false;
            chkForce.Enabled = false;
            chkFix.Enabled = false;
            tbProccessors.Enabled = false;
            Application.DoEvents();
        }

        private void StopWorking()
        {
            _working = false;
            DropBox.Enabled = true;
            cboInType.Enabled = true;
            cboOutType.Enabled = true;
            chkForce.Enabled = true;
            chkFix.Enabled = true;
            tbProccessors.Enabled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_working)
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void ProcessZipsStartThreads()
        {
            _fileIndex.Value = 0;

            _threadsBusyCount = _threadCount;
            for (int i = 0; i < _threadCount; i++)
            {
                CProcessZip cpz = new CProcessZip
                {
                    ThreadId = i,
                    GetNextFileCallBack = GetNextFileCallback,
                    SetFileStatusCallBack = SetFileStatusCallback,
                    StatusCallBack = StatusCallBack
                };

                Thread t = new Thread(cpz.MigrateZip);
                t.Start();
            }
        }

        private void GetNextFileCallback(int processId, out int fileId, out string filename)
        {
            lock (_fileIndex)
            {
                if (_fileIndex.Value < _fileList.Count())
                {
                    fileId = _fileIndex.Value;
                    filename = _fileList.Get(_fileIndex.Value).Filename;
                    Invoke(new StatusInvoker(DoStatusUpdate), _fileIndex.Value, processId, filename);
                    _fileIndex.Value += 1;
                }
                else
                {
                    fileId = -1;
                    filename = "";
                    _threadsBusyCount--;
                    Invoke(new StatusInvoker(DoStatusUpdate), _fileList.Count(), processId, "Complete");
                }
            }
        }

        private void DoStatusUpdate(int fileId, int processId, string filename)
        {
            lblTotalStatus.Text = @"( " + fileId + @" / " + _fileList.Count() + @" )";
            _threadLabel[processId].Text = Path.GetFileName(filename);
            if (_threadsBusyCount == 0)
            {
                StopWorking();
            }


            int topfileId = fileId;
            if (topfileId < dataGrid.Rows.Count)
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
                    dataGrid.Rows[fileId].Cells[1].Value = "Valid RV ZIP";
                    break;
                default:
                    dataGrid.Rows[fileId].Cells[1].Value = trrntZipStatus;
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

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            clickDonate();
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            clickDonate();
        }


        private void clickDonate()
        {
            Process.Start("http://paypal.me/romvault");
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

    }

    public class Counter
    {
        public int Value;
    }
}