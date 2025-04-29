/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using RomVaultCore;
using RomVaultCore.FixFile;

namespace ROMVault
{
    public partial class FrmProgressWindowFix : Form
    {
        private readonly Form _parentForm;
        private int _rowCount;
        private readonly List<string[][]> _reportPages;
        string[][] pageNow;

        private int _rowDisplay;
        private string[][] _pageDisplay;
        private int _pageDisplayIndex;

        private bool _bDone;

        private bool _closeOnExit;


        private ThreadWorker _thWrk;
        private readonly Finished _funcFinished;

        public FrmProgressWindowFix(Form parentForm, bool closeOnExit, Finished funcFinished)
        {
            _closeOnExit = closeOnExit;
            _rowCount = 0;
            _rowDisplay = -1;
            _pageDisplayIndex = -1;
            _pageDisplay = null;

            _reportPages = new List<string[][]>();
            _parentForm = parentForm;
            _funcFinished = funcFinished;
            InitializeComponent();
            dataGridView1.Columns["FileSize"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;


            Type dgvType = dataGridView1.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dataGridView1, true, null);

            if (Settings.rvSettings.Darkness)
                Dark.dark.SetColors(this);

            timer1.Interval = 250;
            timer1.Enabled = true;
        }



        protected override CreateParams CreateParams
        {
            get
            {
                const int CP_NOCLOSE_BUTTON = 0x200;
                CreateParams mdiCp = base.CreateParams;
                mdiCp.ClassStyle = mdiCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return mdiCp;
            }
        }


        private void Timer1Tick(object sender, EventArgs e)
        {
            int tmpRowCount = _rowCount;

            if (_rowDisplay == tmpRowCount || tmpRowCount == 0)
                return;
            dataGridView1.RowCount = tmpRowCount;
            _rowDisplay = tmpRowCount;
            dataGridView1.FirstDisplayedScrollingRowIndex = tmpRowCount - 1;
        }

        private void dataGridView1_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            int pageIndex = e.RowIndex / 1000;
            int rowIndex = e.RowIndex % 1000;

            if (pageIndex != _pageDisplayIndex)
            {
                _pageDisplayIndex = pageIndex;
                _pageDisplay = _reportPages[pageIndex];
            }

            e.Value = _pageDisplay[rowIndex][e.ColumnIndex];
        }
        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            int pageIndex = e.RowIndex / 1000;
            int rowIndex = e.RowIndex % 1000;

            if (pageIndex != _pageDisplayIndex)
            {
                _pageDisplayIndex = pageIndex;
                _pageDisplay = _reportPages[pageIndex];
            }

            if (_pageDisplay[rowIndex][8] == null)
                return;

            e.CellStyle.BackColor = Color.Red;
            e.CellStyle.ForeColor = Color.Black;
        }

        private void FrmProgressWindowFixShown(object sender, EventArgs e)
        {
            SetDataGridSize();
            _thWrk = new ThreadWorker(Fix.PerformFixes) { wReport = BgwProgressChanged, wFinal = BgwRunWorkerCompleted };
            _thWrk.StartAsync();
        }



        private void BgwProgressChanged(object e)
        {
            if (e is bgwShowFix bgwSf)
            {
                int reportLineIndex = _rowCount % 1000;

                if (reportLineIndex == 0)
                {
                    pageNow = new string[1000][];
                    _reportPages.Add(pageNow);
                }

                pageNow[reportLineIndex] =
                    new[]
                    {
                        bgwSf.FixDir, bgwSf.FixZip, bgwSf.FixFile, bgwSf.Size, bgwSf.Dir,
                        bgwSf.SourceDir, bgwSf.SourceZip, bgwSf.SourceFile,null
                    };
                _rowCount += 1;
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => BgwProgressChanged(e)));
                return;
            }

            if (e is bgwShowFixError bgwSFE)
            {
                int errorRowCount = _rowCount - 1;
                int pageIndex = errorRowCount / 1000;
                int rowIndex = errorRowCount % 1000;

                string[] errorRow = _reportPages[pageIndex][rowIndex];
                errorRow[4] = bgwSFE.FixError;
                errorRow[7] = "error";

                dataGridView1.Refresh();
                return;
            }

            if (e is bgwProgress bgwProg)
            {
                if (bgwProg.Progress >= progressBar.Minimum && bgwProg.Progress <= progressBar.Maximum)
                {
                    progressBar.Value = bgwProg.Progress;
                }
                UpdateStatusText();
                return;
            }

            if (e is bgwText bgwT)
            {
                label.Text = bgwT.Text;
                return;
            }

            if (e is bgwSetRange bgwSR)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = bgwSR.MaxVal >= 0 ? bgwSR.MaxVal : 0;
                progressBar.Value = 0;
                UpdateStatusText();
            }
        }

        private void BgwRunWorkerCompleted()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(BgwRunWorkerCompleted));
                return;
            }

            RVPlayer.PlaySound("audio\\complete.wav");

            if (!_closeOnExit)
            {
                cancelButton.Text = "Close";
                cancelButton.Enabled = true;
                _bDone = true;
            }
            else
            {
                _funcFinished?.Invoke();
                _parentForm.Show();
                Close();
            }
        }

        private void UpdateStatusText()
        {
            int range = progressBar.Maximum - progressBar.Minimum;
            int percent = range > 0 ? progressBar.Value * 100 / range : 0;

            Text = $"Fixing Files - {percent}% complete";
        }

        private void CancelButtonClick(object sender, EventArgs e)
        {
            if (_bDone)
            {
                if (!_parentForm.Visible)
                {
                    _parentForm.Show();
                }
                _funcFinished?.Invoke();
                Close();
            }
            else
            {
                cancelButton.Enabled = false;
                cancelButton.Text = "Cancelling";
                _thWrk.Cancel();
            }
        }

        private void DataGridView1SelectionChanged(object sender, EventArgs e)
        {
            dataGridView1.ClearSelection();
        }

        private void FrmProgressWindowFixResize(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case FormWindowState.Minimized:
                    if (_parentForm.Visible)
                    {
                        _parentForm.Hide();
                    }
                    return;
                case FormWindowState.Maximized:
                    if (!_parentForm.Visible)
                    {
                        _parentForm.Show();
                    }
                    return;
                case FormWindowState.Normal:
                    if (!_parentForm.Visible)
                    {
                        _parentForm.Show();
                    }
                    return;
            }
        }

        private void splitContainer1_Panel2_Resize(object sender, EventArgs e)
        {
            SetDataGridSize();
        }
        private void SetDataGridSize()
        {
            dataGridView1.Top = 0;
            dataGridView1.Left = 0;
            dataGridView1.Width = Math.Max(splitContainer1.Panel2.Width, 80);
            dataGridView1.Height = Math.Max(splitContainer1.Panel2.Height, 80);
        }
    }
}