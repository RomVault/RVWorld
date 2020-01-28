/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RVCore;
using RVCore.FixFile;

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


        private ThreadWorker ThWrk;

        public FrmProgressWindowFix(Form parentForm)
        {
            _rowCount = 0;
            _rowDisplay = -1;
            _pageDisplayIndex = -1;
            _pageDisplay = null;

            _reportPages = new List<string[][]>();
            _parentForm = parentForm;
            InitializeComponent();
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
            if (_rowDisplay == _rowCount || _rowCount==0)
                return;
            dataGridView1.RowCount = _rowCount;
            _rowDisplay = _rowCount;
            dataGridView1.FirstDisplayedScrollingRowIndex = _rowCount - 1;
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
            ThWrk = new ThreadWorker(Fix.PerformFixes) { wReport = BgwProgressChanged, wFinal = BgwRunWorkerCompleted };
            ThWrk.StartAsync();
        }



        private void BgwProgressChanged(object e)
        {

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => BgwProgressChanged(e)));
                return;
            }

            if (e is bgwShowFix bgwSf)
            {
                int reportLineIndex = _rowCount % 1000;

                if (reportLineIndex == 0)
                {
                    pageNow=new string[1000][];
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
                Invoke(new MethodInvoker(BgwRunWorkerCompleted));
                return;
            }

            cancelButton.Text = "Close";
            cancelButton.Enabled = true;
            _bDone = true;
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
                Close();
            }
            else
            {
                cancelButton.Enabled = false;
                cancelButton.Text = "Cancelling";
                ThWrk.Cancel();
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

    }
}