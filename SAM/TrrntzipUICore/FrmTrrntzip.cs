using Compress.StructuredZip;
using RVIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using TrrntZip;

namespace TrrntZipUICore
{
    public partial class FrmTrrntzip : Form
    {
        private int _fileIndex;
        private int FileCount;
        private int FileCountProcessed;

        private TzipSettings tZipSettings;


        private class ThreadProcess
        {
            public Label threadLabel;
            public ProgressBar threadProgress;
            public string tLabel;
            public int tProgress;
            public CProcessZip cProcessZip;
            public Thread thread;
        }
        private readonly List<ThreadProcess> _threads;

        private class dGrid
        {
            public int fileId;
            public string filename;
            public string status;
        }

        private readonly List<dGrid> tGrid;
        private int tGridMax = 0;

        private readonly PauseCancel pc;

        private bool _working;
        private int _threadCount;

        private bool UiUpdate = false;
        private bool scanningForFiles = false;

        public FrmTrrntzip()
        {
            UiUpdate = true;
            InitializeComponent();
            DropBox.Image = null;

            Type dgvType = dataGrid.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dataGrid, true, null);

            this.Text = $"SAM-UI ({Assembly.GetExecutingAssembly().GetName().Version.ToString(3)})";
            DropBox.AllowDrop = true;
            DropBox.DragEnter += PDragEnter;
            DropBox.DragDrop += PDragDrop;

            tZipSettings = TzipSettings.ReadConfig();

            cboInType.SelectedIndex = tZipSettings.InZip;

            cboOutType.SelectedIndex = tZipSettings.OutZip;

            chkDryRun.Checked = tZipSettings.DryRun;

            tbProccessors.Minimum = 1;
            tbProccessors.Maximum = Environment.ProcessorCount;

            int procc = tZipSettings.ProcCount;
            if (procc <= 0)
            {
                procc = tbProccessors.Maximum;
            }

            if (procc > tbProccessors.Maximum)
            {
                procc = tbProccessors.Maximum;
            }

            tbProccessors.Value = procc;

            _threads = new List<ThreadProcess>();
            tGrid = new List<dGrid>();
            pc = new PauseCancel();

            SetUpWorkerThreads();

            UiUpdate = false;
        }

        private void SetUpWorkerThreads()
        {
            _threadCount = tbProccessors.Value;

            MainQueue.bccFile?.CompleteAdding();

            foreach (ThreadProcess tp in _threads)
            {
                StatusPanel.Controls.Remove(tp.threadLabel);
                tp.threadLabel.Dispose();

                StatusPanel.Controls.Remove(tp.threadProgress);
                tp.threadProgress.Dispose();

                tp.cProcessZip.ProcessFileStartCallBack = null;
                tp.cProcessZip.StatusCallBack = null;
                tp.cProcessZip.ErrorCallBack = null;
                tp.cProcessZip.ProcessFileEndCallBack = null;
                tp.thread.Join();
            }

            MainQueue.bccFile?.Dispose();

            _threads.Clear();
            MainQueue.bccFile = new BlockingCollection<cFile>();


            int workers = (Environment.ProcessorCount - 1) / _threadCount;
            if (workers == 0) workers = 1;

            for (int i = 0; i < _threadCount; i++)
            {
                ThreadProcess threadProcess = new ThreadProcess();
                _threads.Add(threadProcess);

                Label pLabel = new Label
                {
                    Visible = true,
                    Left = 12,
                    Top = 235 + 30 * i,
                    Width = 225,
                    Height = 15,
                    Text = $"Processor {i + 1}",
                    AutoEllipsis = true

                };

                StatusPanel.Controls.Add(pLabel);
                threadProcess.threadLabel = pLabel;

                ProgressBar pProgress = new ProgressBar
                {
                    Visible = true,
                    Left = 12,
                    Top = 250 + 30 * i,
                    Width = 225,
                    Height = 12
                };
                StatusPanel.Controls.Add(pProgress);
                threadProcess.threadProgress = pProgress;


                threadProcess.cProcessZip = new CProcessZip
                {
                    ThreadId = i,
                    ProcessFileStartCallBack = ProcessFileStartCallback,
                    StatusCallBack = StatusCallBack,
                    ErrorCallBack = ErrorCallBack,
                    ProcessFileEndCallBack = ProcessFileEndCallback,
                    pauseCancel = pc,
                    workerCount = workers
                };
                threadProcess.thread = new Thread(threadProcess.cProcessZip.MigrateZip);
                threadProcess.thread.Start();
            }

            if (Height < 325 + 30 * _threadCount)
            {
                Height = 325 + 30 * _threadCount;
            }

            Debug.WriteLine($"Cores found: {Environment.ProcessorCount},  File Workers: {_threadCount},   zstd core per file: {workers}");

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
            if (_working)
                return;

            string[] file = (string[])e.Data.GetData(DataFormats.FileDrop);
            Array.Sort(file);

            dataGrid.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
            dataGrid.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;

            Settings settings = new Settings
            {
                Repair = cboOutType.SelectedIndex == 6,
                DryRun = chkDryRun.Checked,
                InZip = (InputZipType)cboInType.SelectedIndex,
                OutZip = ZipStructureFromUIIndex(cboOutType.SelectedIndex)
            };

            tGrid.Clear();
            tGridMax = 0;
            dataGrid.Rows.Clear();

            StartWorking();

            FileCountProcessed = 0;
            scanningForFiles = true;
            FileAdder pm = new FileAdder(MainQueue.bccFile, file, UpdateFileCount, ProcessFileEndCallback, settings, pc);
            Thread procT = new Thread(pm.ProcFiles);
            procT.Start();

            timer1.Interval = 125;
            timer1.Enabled = true;

        }

        private void StartWorking()
        {
            _working = true;
            DropBox.Image = rvImages1.giphy;
            cboInType.Enabled = false;
            cboOutType.Enabled = false;
            chkDryRun.Enabled = false;
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
            DropBox.Image = null;
            cboInType.Enabled = true;
            cboOutType.Enabled = true;
            chkDryRun.Enabled = true;
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
            else
            {
                MainQueue.bccFile?.CompleteAdding();
                foreach (ThreadProcess tp in _threads)
                {
                    tp.cProcessZip.ProcessFileStartCallBack = null;
                    tp.cProcessZip.StatusCallBack = null;
                    tp.cProcessZip.ErrorCallBack = null;
                    tp.cProcessZip.ProcessFileEndCallBack = null;
                    tp.thread.Join();
                }

                MainQueue.bccFile?.Dispose();
            }
            if (frmErrorLog != null)
            {
                frmErrorLog.closing = true;
                frmErrorLog.Close();
            }

            base.OnFormClosing(e);
        }


        private void picTitle_Click(object sender, EventArgs e)
        {
            StartURL("http://www.romvault.com");
        }

        private void picDonate_Click(object sender, EventArgs e)
        {
            StartURL("http://paypal.me/romvault");
        }

        private void picRomVault_Click(object sender, EventArgs e)
        {
            StartURL("http://www.romvault.com");
        }


        public static void StartURL(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }


        private void tbProccessors_ValueChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;

            tZipSettings.ProcCount = tbProccessors.Value;
            TzipSettings.WriteConfig(tZipSettings);
            SetUpWorkerThreads();
        }

        private void chkDryRun_CheckedChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            tZipSettings.DryRun = chkDryRun.Checked;
            TzipSettings.WriteConfig(tZipSettings);
        }

        private void cboInType_TextChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            tZipSettings.InZip = cboInType.SelectedIndex;
            if (cboOutType.SelectedIndex == 6 && cboInType.SelectedIndex > 2)
            {
                UiUpdate = true;
                cboOutType.SelectedIndex = 0;
                tZipSettings.OutZip = cboOutType.SelectedIndex;
                UiUpdate = false;
            }

            TzipSettings.WriteConfig(tZipSettings);
        }

        private void cboOutType_TextChanged(object sender, EventArgs e)
        {
            if (UiUpdate)
                return;
            tZipSettings.OutZip = cboOutType.SelectedIndex;
            if (cboOutType.SelectedIndex==6 && cboInType.SelectedIndex>2)
            {
                UiUpdate = true;
                cboInType.SelectedIndex = 2;
                tZipSettings.InZip = cboInType.SelectedIndex;
                UiUpdate = false;
            }
            TzipSettings.WriteConfig(tZipSettings);
        }


        private static Bitmap GetBitmap(string bitmapName)
        {
            object bmObj = rvImages1.ResourceManager.GetObject(bitmapName);

            Bitmap bm = null;
            if (bmObj != null)
            {
                bm = (Bitmap)bmObj;
            }

            return bm;
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            if (!pc.Paused)
            {
                // Pause
                btnPause.Image = GetBitmap("Resume");
                DropBox.Enabled = false;
                pc.Pause();
            }
            else
            {
                // Resume after a Pause
                btnPause.Image = GetBitmap("Pause");
                DropBox.Enabled = true;
                pc.UnPause();
            }
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            // start Cancel
            btnPause.Image = GetBitmap("Pause");
            pc.Cancel();
            DropBox.Enabled = true;
            btnCancel.Enabled = false;
            btnPause.Enabled = false;
        }

        private static ZipStructure ZipStructureFromUIIndex(int cboIndex)
        {
            switch (cboIndex)
            {
                case 0: return ZipStructure.ZipTrrnt;
                case 1: return ZipStructure.ZipZSTD;
                case 2: return ZipStructure.SevenZipNZSTD;
                case 3: return ZipStructure.SevenZipSZSTD;
                case 4: return ZipStructure.SevenZipNLZMA;
                case 5: return ZipStructure.SevenZipSLZMA;
                case 6: return ZipStructure.None; // this is the repair option, and is not used as an output
                default: return ZipStructure.ZipTrrnt;
            }
        }

        #region callbacks

        private void UpdateFileCount(int fileCount)
        {
            FileCount = fileCount;
        }


        private void ProcessFileStartCallback(int processId, int fileId, string filename)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ProcessFileStartCallback(ProcessFileStartCallback), processId, fileId, filename);
                return;
            }

            lock (tGrid)
            {
                _fileIndex = fileId + 1;

                _threads[processId].tLabel = Path.GetFileName(filename);
                _threads[processId].tProgress = 0;

                if ((fileId + 1) > tGridMax)
                    tGridMax = (fileId + 1);

                tGrid.Add(new dGrid() { fileId = fileId, filename = filename, status = "Processing....(" + processId + ")" });
            }
        }

        private void ProcessFileEndCallback(int processId, int fileId, TrrntZipStatus trrntZipStatus, ZipStructure zipStruct)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ProcessFileEndCallback(ProcessFileEndCallback), processId, fileId, trrntZipStatus, zipStruct);
                return;
            }

            if (processId == -1)
            {
                scanningForFiles = false;

                if (FileCount == 0)
                {
                    StopWorking();
                    if (pc.Cancelled)
                        pc.ResetCancel();
                }
            }
            else
            {
                _threads[processId].tProgress = 100;

                dGrid tGridn = new dGrid() { fileId = fileId, filename = null };
                switch (trrntZipStatus)
                {
                    case TrrntZipStatus.ValidTrrntzip:
                        tGridn.status = $"Valid {StructuredArchive.GetZipStructureName(zipStruct)}";
                        break;
                    case TrrntZipStatus.Trrntzipped:
                        tGridn.status = $"Re Struc {StructuredArchive.GetZipStructureName(zipStruct)}";
                        break;
                    case TrrntZipStatus.NeedsRepaired:
                        tGridn.status = $"Needs Repair {StructuredArchive.GetZipStructureName(zipStruct)}";
                        break;
                    case TrrntZipStatus.Trrntzipped | TrrntZipStatus.NeedsRepaired:
                        tGridn.status = $"Repaired {StructuredArchive.GetZipStructureName(zipStruct)}";
                        break;
                    default:
                        tGridn.status = trrntZipStatus.ToString();
                        break;
                }

                lock (tGrid)
                {
                    if ((fileId + 1) > tGridMax)
                        tGridMax = (fileId + 1);

                    tGrid.Add(tGridn);

                    FileCountProcessed += 1;
                }

                if (!scanningForFiles && FileCountProcessed == FileCount)
                {
                    StopWorking();
                    if (pc.Cancelled)
                        pc.ResetCancel();
                }
            }

        }

        private void StatusCallBack(int processId, int percent)
        {
            _threads[processId].tProgress = percent;
        }

        frmErrorLog frmErrorLog = null;
        private void ErrorCallBack(int processId, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ErrorCallback(ErrorCallBack), processId, message);
                return;
            }

            if (frmErrorLog == null)
            {
                frmErrorLog = new frmErrorLog();
            }
            frmErrorLog.AddError(message);
        }

        #endregion

        private int uiFileCount = -1;
        private int uiFileIndex = -1;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_fileIndex != uiFileIndex || FileCount != uiFileCount)
            {
                uiFileIndex = _fileIndex;
                uiFileCount = FileCount;

                lblTotalStatus.Text = @"( " + uiFileIndex + @" / " + uiFileCount + @" )";
            }

            foreach (ThreadProcess tp in _threads)
            {
                if (tp.tProgress != tp.threadProgress.Value)
                    tp.threadProgress.Value = tp.tProgress;
                if (tp.tLabel != tp.threadLabel.Text)
                    tp.threadLabel.Text = tp.tLabel;
            }

            if (dataGrid.RowCount != tGridMax)
            {
                dataGrid.RowCount = tGridMax;
                dataGrid.FirstDisplayedScrollingRowIndex = tGridMax - 1;

            }

            lock (tGrid)
            {
                foreach (dGrid dg in tGrid)
                {
                    var c = dataGrid.Rows[dg.fileId].Cells;
                    if (dg.filename != null)
                        c[0].Value = dg.filename;
                    if (dg.status != null)
                        c[1].Value = dg.status;
                }
                tGrid.Clear();
            }
        }
    }
}