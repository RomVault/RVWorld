using RVIO;
using System;
using System.Windows.Forms;

namespace ROMVault
{
    /// <summary>
    /// Cross-platform folder picker wrapper that uses a WinForms dialog on Unix and an enhanced dialog on Windows.
    /// </summary>
    public class FolderBrowser
    {
        public bool ShowNewFolderButton;
        public string Description;
        public string SelectedPath;
        public Environment.SpecialFolder RootFolder = (Environment.SpecialFolder)0;


        public DialogResult ShowDialog()
        {
            if (Unix.IsUnix)
            {
                FolderBrowserDialog browse = new FolderBrowserDialog
                {
                    ShowNewFolderButton = this.ShowNewFolderButton,
                    Description = this.Description,
                    RootFolder = this.RootFolder,
                    SelectedPath = this.SelectedPath,
                };

                DialogResult result = browse.ShowDialog();
                if (result == DialogResult.OK)
                    this.SelectedPath = browse.SelectedPath;
                return result;
            }
            else
            {
                FolderBrowserDialogEx browse = new FolderBrowserDialogEx
                {
                    ShowNewFolderButton = this.ShowNewFolderButton,
                    Description = this.Description,
                    RootFolder = this.RootFolder,
                    SelectedPath = this.SelectedPath
                };

                DialogResult result = browse.ShowDialog();
                if (result == DialogResult.OK)
                    this.SelectedPath = browse.SelectedPath;
                return result;
            }
        }

        public DialogResult ShowDialog(IWin32Window owner)
        {
            if (Unix.IsUnix)
            {
                FolderBrowserDialog browse = new FolderBrowserDialog
                {
                    ShowNewFolderButton = this.ShowNewFolderButton,
                    Description = this.Description,
                    RootFolder = this.RootFolder,
                    SelectedPath = this.SelectedPath,
                };

                DialogResult result = browse.ShowDialog(owner);
                if (result == DialogResult.OK)
                    this.SelectedPath = browse.SelectedPath;
                return result;
            }
            else
            {
                FolderBrowserDialogEx browse = new FolderBrowserDialogEx
                {
                    ShowNewFolderButton = this.ShowNewFolderButton,
                    Description = this.Description,
                    RootFolder = this.RootFolder,
                    SelectedPath = this.SelectedPath
                };

                DialogResult result = browse.ShowDialog(owner);
                if (result == DialogResult.OK)
                    this.SelectedPath = browse.SelectedPath;
                return result;
            }
        }

    }
}
