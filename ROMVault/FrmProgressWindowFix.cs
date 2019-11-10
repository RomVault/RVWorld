/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RVCore;
using RVCore.FixFile;

namespace ROMVault
{
    public partial class FrmProgressWindowFix : Form
    {
        private readonly Form _parentForm;

        private readonly Queue<DataGridViewRow> _rowQueue;

        private bool _bDone;


        private ThreadWorker ThWrk;

        public FrmProgressWindowFix(Form parentForm)
        {
            _rowQueue = new Queue<DataGridViewRow>();
            _parentForm = parentForm;
            InitializeComponent();
            timer1.Interval = 100;
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
            lock (_rowQueue)
            {
                int rowCount = _rowQueue.Count;
                if (rowCount == 0)
                {
                    return;
                }

                DataGridViewRow[] dgvr = new DataGridViewRow[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    dgvr[i] = _rowQueue.Dequeue();
                }

                dataGridView1.Rows.AddRange(dgvr);
            }
            int iRow = dataGridView1.Rows.Count - 1;
            dataGridView1.FirstDisplayedScrollingRowIndex = iRow;
        }


        private void FrmProgressWindowFixShown(object sender, EventArgs e)
        {
            ThWrk = new ThreadWorker(Fix.PerformFixes) {wReport = BgwProgressChanged, wFinal = BgwRunWorkerCompleted};
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
                DataGridViewRow dgrq = (DataGridViewRow)dataGridView1.RowTemplate.Clone();
                dgrq.CreateCells(dataGridView1, bgwSf.FixDir, bgwSf.FixZip, bgwSf.FixFile, bgwSf.Size, bgwSf.Dir, bgwSf.SourceDir, bgwSf.SourceZip, bgwSf.SourceFile);
                lock (_rowQueue)
                {
                    _rowQueue.Enqueue(dgrq);
                }
                return;
            }

            if (e is bgwShowFixError bgwSFE)
            {
                lock (_rowQueue)
                {
                    DataGridViewRow setError;
                    if (_rowQueue.Count == 0)
                    {
                        int iRow = dataGridView1.Rows.Count - 1;

                        //this should never happen
                        if (iRow == -1)
                            return;
                        setError = dataGridView1.Rows[iRow];
                    }
                    else
                    {
                        setError = _rowQueue.Last();
                        if (setError == null)
                            return;
                    }
                    setError.Cells[4].Style.BackColor = Color.Red;
                    setError.Cells[4].Style.ForeColor = Color.Black;
                    setError.Cells[4].Value = bgwSFE.FixError;
                }

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