/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2025                                 *
 ******************************************************/

using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using RomVaultCore;

namespace ROMVault
{
    public delegate void Finished();

    public partial class FrmProgressWindow : Form
    {
        private readonly string _titleRoot;
        private readonly Form _parentForm;
        private bool _errorOpen;
        private bool _bDone;
        public bool Cancelled;
        public bool ShowTimeLog = false;

        private readonly ThreadWorker _thWrk;
        private readonly Finished _funcFinished;

        private DateTime _dateTime;
        private DateTime _dateTimeLast;
        private string _lastMessage;


        public FrmProgressWindow(Form parentForm, string titleRoot, WorkerStart function, Finished funcFinished)
        {
            Cancelled = false;
            _parentForm = parentForm;
            _titleRoot = titleRoot;
            _funcFinished = funcFinished;
            InitializeComponent();
            Type dgvType = ErrorGrid.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(ErrorGrid, true, null);

            ClientSize = new Size(511, 131);
            _dateTime = DateTime.Now;
            _dateTimeLast = _dateTime;

            _titleRoot = titleRoot;
            _lastMessage = "Initializing";

            if (Settings.rvSettings.Darkness)
                Dark.dark.SetColors(this);


            _thWrk = new ThreadWorker(function);
        }

        public void HideCancelButton()
        {
            cancelButton.Text = "Close";
            cancelButton.Enabled = false;
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


        private void FrmProgressWindowNewShown(object sender, EventArgs e)
        {
            SetDataGridSize();
            _thWrk.wReport = BgwProgressChanged;
            _thWrk.wFinal = BgwRunWorkerCompleted;
            _thWrk.StartAsync();
        }

        private void TimeLogShow(string message)
        {
            if (!_errorOpen)
            {
                _errorOpen = true;
                ClientSize = new Size(511, 292);
                MinimumSize = new Size(511, 292);
                ErrorGrid.Columns[0].HeaderText = "Time";
                ErrorGrid.Columns[1].HeaderText = "Log";
            }

            ErrorGrid.Rows.Add();
            int row = ErrorGrid.Rows.Count - 1;

            DateTime dtNow = DateTime.Now;
            string total = Math.Round((dtNow - _dateTime).TotalSeconds, 3).ToString();
            string part = Math.Round((dtNow - _dateTimeLast).TotalSeconds, 3).ToString();
            _dateTimeLast = dtNow;
            ErrorGrid.Rows[row].Cells["CError"].Value = $"{total} s  ,  ({part} s)";

            ErrorGrid.Rows[row].Cells["CErrorFile"].Value = $"Completed: {_lastMessage}";
            _lastMessage = message;

            if (row >= 0)
            {
                ErrorGrid.FirstDisplayedScrollingRowIndex = row;
            }
        }

        private void BgwProgressChanged(object obj)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => BgwProgressChanged(obj)));
                return;
            }

            if (obj is int e)
            {
                if (e >= progressBar.Minimum && e <= progressBar.Maximum)
                {
                    progressBar.Value = e;
                }
                UpdateStatusText();
                return;
            }

            if (obj is bgwText bgwT)
            {
                label.Text = bgwT.Text;
                if (ShowTimeLog)
                    TimeLogShow(bgwT.Text);
                return;
            }
            if (obj is bgwSetRange bgwSr)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = bgwSr.MaxVal >= 0 ? bgwSr.MaxVal : 0;
                progressBar.Value = 0;
                UpdateStatusText();
                return;
            }


            if (obj is bgwText2 bgwT2)
            {
                label2.Text = bgwT2.Text;
                return;
            }

            if (obj is bgwValue2 bgwV2)
            {
                if (bgwV2.Value >= progressBar2.Minimum && bgwV2.Value <= progressBar2.Maximum)
                {
                    progressBar2.Value = bgwV2.Value;
                }
                UpdateStatusText2();
                return;
            }

            if (obj is bgwSetRange2 bgwSr2)
            {
                progressBar2.Minimum = 0;
                progressBar2.Maximum = bgwSr2.MaxVal >= 0 ? bgwSr2.MaxVal : 0;
                progressBar2.Value = 0;
                UpdateStatusText2();
                return;
            }
            if (obj is bgwRange2Visible bgwR2V)
            {
                label2.Visible = bgwR2V.Visible;
                progressBar2.Visible = bgwR2V.Visible;
                lbl2Prog.Visible = bgwR2V.Visible;
                return;
            }


            if (obj is bgwText3 bgwT3)
            {
                label3.Text = bgwT3.Text;
                return;
            }


            if (obj is bgwShowError bgwSE)
            {
                if (!_errorOpen)
                {
                    _errorOpen = true;
                    ClientSize = new Size(511, 292);
                    MinimumSize = new Size(511, 292);
                }

                ErrorGrid.Rows.Add();
                int row = ErrorGrid.Rows.Count - 1;

                ErrorGrid.Rows[row].Cells["CError"].Value = bgwSE.error;
                ErrorGrid.Rows[row].Cells["CError"].Style.ForeColor = Color.FromArgb(255, 0, 0);

                ErrorGrid.Rows[row].Cells["CErrorFile"].Value = bgwSE.filename;
                ErrorGrid.Rows[row].Cells["CErrorFile"].Style.ForeColor = Color.FromArgb(255, 0, 0);

                RVPlayer.PlaySound("audio\\error.wav");

                if (row >= 0)
                {
                    ErrorGrid.FirstDisplayedScrollingRowIndex = row;
                }
            }
        }


        private void UpdateStatusText()
        {
            int range = progressBar.Maximum - progressBar.Minimum;
            int percent = range > 0 ? progressBar.Value * 100 / range : 0;

            Text = $"{_titleRoot} - {percent}% complete";
        }

        private void UpdateStatusText2()
        {
            lbl2Prog.Text = progressBar2.Maximum > 0 ? $"{progressBar2.Value}/{progressBar2.Maximum}" : "";
        }

        private void BgwRunWorkerCompleted()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(BgwRunWorkerCompleted));
                return;
            }
            RVPlayer.PlaySound("audio\\complete.wav");

            if (_errorOpen)
            {
                cancelButton.Visible = true;
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
                Cancelled = true;
                cancelButton.Visible = true;
                cancelButton.Text = "Cancelling";
                cancelButton.Enabled = false;
                _thWrk.Cancel();
            }
        }

        private void ErrorGridSelectionChanged(object sender, EventArgs e)
        {
            ErrorGrid.ClearSelection();
        }

        private void FrmProgressWindow_Resize(object sender, EventArgs e)
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
            ErrorGrid.Top = 0;
            ErrorGrid.Left = 0;
            ErrorGrid.Width = Math.Max(splitContainer1.Panel2.Width, 80);
            ErrorGrid.Height = Math.Max(splitContainer1.Panel2.Height, 80);
        }
    }
}