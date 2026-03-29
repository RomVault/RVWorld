using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using RomVaultCore;
using RomVaultCore.RvDB;
using RomVaultCore.ReadDat;
using RomVaultCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// Window for managing directory mappings (where ROMs are stored on disk vs where they appear in the tree).
    /// </summary>
    public partial class DirectoryMappingsWindow : Window
    {
        private Color _cMagenta = Color.FromRgb(255, 214, 255);
        private Color _cGreen = Color.FromRgb(214, 255, 214);
        private Color _cYellow = Color.FromRgb(255, 255, 214);
        private Color _cRed = Color.FromRgb(255, 214, 214);

        private DirMapping _rule = null!;
        private bool _displayType;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryMappingsWindow"/> class.
        /// </summary>
        public DirectoryMappingsWindow()
        {
            InitializeComponent();
            
            if (Settings.rvSettings.DirMappings.Count > 0)
                _rule = Settings.rvSettings.DirMappings[0];
            else
                _rule = new DirMapping { DirKey = "RomVault" };

            // Fix colors for dark mode if needed (Avalonia handles themes differently, but logic preserved)
            if (Settings.rvSettings.Darkness)
            {
                _cMagenta = Color.FromRgb((byte)(255 * 0.8), (byte)(214 * 0.8), (byte)(255 * 0.8));
                _cGreen = Color.FromRgb((byte)(214 * 0.8), (byte)(255 * 0.8), (byte)(214 * 0.8));
                _cYellow = Color.FromRgb((byte)(255 * 0.8), (byte)(255 * 0.8), (byte)(214 * 0.8));
                _cRed = Color.FromRgb((byte)(255 * 0.8), (byte)(214 * 0.8), (byte)(214 * 0.8));
            }

            // Setup events
            var btnSetROMLocation = this.FindControl<Button>("btnSetROMLocation");
            var btnClearROMLocation = this.FindControl<Button>("btnClearROMLocation");
            var btnApply = this.FindControl<Button>("btnSet");
            var btnDelete = this.FindControl<Button>("btnDelete");
            var btnDeleteSelected = this.FindControl<Button>("btnDeleteSelected");
            var btnResetAll = this.FindControl<Button>("btnResetAll");
            var btnClose = this.FindControl<Button>("btnClose");
            var dgRules = this.FindControl<DataGrid>("DGDirectoryMappingRules");

            if (btnSetROMLocation != null) btnSetROMLocation.Click += BtnSetROMLocationClick;
            if (btnClearROMLocation != null) btnClearROMLocation.Click += BtnClearROMLocation_Click;
            if (btnApply != null) btnApply.Click += BtnApplyClick;
            if (btnDelete != null) btnDelete.Click += BtnDeleteClick;
            if (btnDeleteSelected != null) btnDeleteSelected.Click += BtnDeleteSelectedClick;
            if (btnResetAll != null) btnResetAll.Click += BtnResetAllClick;
            if (btnClose != null) btnClose.Click += BtnCloseClick;
            if (dgRules != null) dgRules.DoubleTapped += DataGridGamesDoubleClick;

            UpdateGrid();
            SetDisplay();
        }

        /// <summary>
        /// Sets the directory location to edit.
        /// </summary>
        /// <param name="dLocation">The directory key (tree path).</param>
        public void SetLocation(string dLocation)
        {
            _rule = FindRule(dLocation);
            SetDisplay();
            UpdateGrid();
        }

        /// <summary>
        /// Configures the window display mode (Global vs Specific).
        /// </summary>
        /// <param name="type">If true, shows specific directory editing mode. If false, shows global mapping list.</param>
        public void SetDisplayType(bool type)
        {
            _displayType = type;
            var btnDelete = this.FindControl<Button>("btnDelete");
            if (btnDelete != null) btnDelete.IsVisible = type;

            // Hide/Show controls based on type (simplified logic compared to WinForms loop)
            var lblDelete = this.FindControl<TextBlock>("lblDelete"); // "Existing Mapping"
            var dgRules = this.FindControl<DataGrid>("DGDirectoryMappingRules");
            var btnDeleteSelected = this.FindControl<Button>("btnDeleteSelected");
            var btnResetAll = this.FindControl<Button>("btnResetAll");
            var btnClose = this.FindControl<Button>("btnClose");

            bool showGrid = !type;
            if (lblDelete != null) lblDelete.IsVisible = showGrid;
            if (dgRules != null) dgRules.IsVisible = showGrid;
            if (btnDeleteSelected != null) btnDeleteSelected.IsVisible = showGrid;
            if (btnResetAll != null) btnResetAll.IsVisible = showGrid;
            if (btnClose != null) btnClose.IsVisible = showGrid;

            if (type)
            {
                Height = 320;
                MinHeight = 260;
            }
            else
            {
                Height = 520;
                MinHeight = 420;
            }
            CanResize = true;
        }

        /// <summary>
        /// Finds an existing mapping rule or creates a new one for the given location.
        /// </summary>
        private static DirMapping FindRule(string dLocation)
        {
            foreach (DirMapping t in Settings.rvSettings.DirMappings)
            {
                if (string.Compare(t.DirKey, dLocation, StringComparison.Ordinal) == 0)
                    return t;
            }

            return new DirMapping { DirKey = dLocation };
        }

        /// <summary>
        /// Updates the UI text blocks with the current rule values.
        /// </summary>
        private void SetDisplay()
        {
            var txtDATLocation = this.FindControl<TextBox>("txtDATLocation");
            var txtROMLocation = this.FindControl<TextBox>("txtROMLocation");
            
            if (txtDATLocation != null) txtDATLocation.Text = _rule.DirKey;
            if (txtROMLocation != null) txtROMLocation.Text = _rule.DirPath;
        }

        /// <summary>
        /// Updates the DataGrid with the list of directory mappings.
        /// Applies color coding based on mapping status.
        /// </summary>
        private void UpdateGrid()
        {
            var dgRules = this.FindControl<DataGrid>("DGDirectoryMappingRules");
            if (dgRules == null) return;

            var items = new List<DirMappingViewModel>();
            foreach (DirMapping t in Settings.rvSettings.DirMappings)
            {
                var vm = new DirMappingViewModel(t);
                
                if (t.DirPath == "ToSort")
                {
                    vm.BgColor = new SolidColorBrush(_cMagenta);
                }
                else if (t == _rule)
                {
                    vm.BgColor = new SolidColorBrush(_cGreen);
                }
                else if (t.DirKey.Length > _rule.DirKey.Length)
                {
                    if (t.DirKey.Substring(0, _rule.DirKey.Length + 1) == _rule.DirKey + "\\")
                    {
                        vm.BgColor = new SolidColorBrush(_cYellow);
                    }
                }

                if (!Directory.Exists(t.DirPath))
                {
                    vm.BgColor = new SolidColorBrush(_cRed);
                }
                items.Add(vm);
            }
            dgRules.ItemsSource = items;
        }

        /// <summary>
        /// Clears the ROM location for the current rule.
        /// </summary>
        private void BtnClearROMLocation_Click(object? sender, RoutedEventArgs e)
        {
            var txtROMLocation = this.FindControl<TextBox>("txtROMLocation");
            if (txtROMLocation == null) return;

            if (_rule.DirKey == "RomVault")
            {
                txtROMLocation.Text = "RomRoot";
                return;
            }

            if (_rule.DirKey == "ToSort")
            {
                txtROMLocation.Text = "ToSort";
                return;
            }

            txtROMLocation.Text = null;
        }

        /// <summary>
        /// Opens a folder picker to set the ROM location.
        /// </summary>
        private async void BtnSetROMLocationClick(object? sender, RoutedEventArgs e)
        {
            var txtROMLocation = this.FindControl<TextBox>("txtROMLocation");
            
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Please select a folder for This Rom Set",
                AllowMultiple = false
            });

            if (folders.Count == 1 && txtROMLocation != null)
            {
                string selectedPath = folders[0].Path.LocalPath;
                string relPath = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, selectedPath);
                txtROMLocation.Text = relPath;
            }
        }

        private async Task CopyToClipboard(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard == null)
                return;

            await topLevel.Clipboard.SetTextAsync(text);
        }

        /// <summary>
        /// Applies the changes to the current mapping rule and saves settings.
        /// </summary>
        private async void BtnApplyClick(object? sender, RoutedEventArgs e)
        {
            var txtROMLocation = this.FindControl<TextBox>("txtROMLocation");
            string? newDir = txtROMLocation?.Text;

            if (string.IsNullOrWhiteSpace(newDir))
            {
                await MessageBoxWindow.ShowInfo(this, "Please select a directory location before applying.", "Missing Directory");
                return;
            }

            if (newDir != "RomRoot" && newDir != "ToSort" && !Directory.Exists(newDir))
            {
                bool ok = await MessageBoxWindow.ShowConfirm(
                    this,
                    $"The directory does not exist:\r\n\r\n{newDir}\r\n\r\nApply anyway?",
                    "Directory Not Found",
                    okText: "Apply",
                    cancelText: "Cancel");
                if (!ok) return;
            }
            
            _rule.DirPath = newDir;

            bool updatingRule = false;
            int i;
            for (i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
            {
                if (Settings.rvSettings.DirMappings[i] == _rule)
                {
                    updatingRule = true;
                    break;
                }

                if (string.Compare(Settings.rvSettings.DirMappings[i].DirKey, _rule.DirKey, StringComparison.Ordinal) > 0)
                {
                    break;
                }
            }

            if (!updatingRule)
                Settings.rvSettings.DirMappings.Insert(i, _rule);

            UpdateGrid();
            Settings.WriteConfig(Settings.rvSettings);

            if (_displayType)
                Close();
        }

        /// <summary>
        /// Deletes the current mapping rule.
        /// </summary>
        private async void BtnDeleteClick(object? sender, RoutedEventArgs e)
        {
            string datLocation = _rule.DirKey;

            if (datLocation == "RomVault")
            {
                await MessageBoxWindow.ShowInfo(this, "The 'RomVault' mapping cannot be deleted.", "Not Allowed");
                return;
            }
            else
            {
                bool ok = await MessageBoxWindow.ShowConfirm(
                    this,
                    $"Delete mapping for:\r\n\r\n{datLocation}\r\n\r\nThis will remove it from settings.",
                    "Confirm Delete",
                    okText: "Delete",
                    cancelText: "Cancel");
                if (!ok) return;

                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                for (int i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
                {
                    if (Settings.rvSettings.DirMappings[i].DirKey == datLocation)
                    {
                        Settings.rvSettings.DirMappings.RemoveAt(i);
                        i--;
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);

            UpdateGrid();
            Close();
        }

        /// <summary>
        /// Deletes selected mapping rules from the grid.
        /// </summary>
        private async void BtnDeleteSelectedClick(object? sender, RoutedEventArgs e)
        {
            var dgRules = this.FindControl<DataGrid>("DGDirectoryMappingRules");
            if (dgRules?.SelectedItems == null) return;

            int deleteCount = 0;
            foreach (var item in dgRules.SelectedItems)
            {
                if (item is DirMappingViewModel vm && vm.DirKey != "RomVault")
                    deleteCount++;
            }

            if (deleteCount == 0)
                return;

            bool ok = await MessageBoxWindow.ShowConfirm(
                this,
                $"Delete {deleteCount} selected mapping(s)?",
                "Confirm Delete",
                okText: "Delete",
                cancelText: "Cancel");
            if (!ok) return;

            foreach (var item in dgRules.SelectedItems)
            {
                if (item is DirMappingViewModel vm)
                {
                    string datLocation = vm.DirKey;
                    if (datLocation == "RomVault")
                    {
                        continue;
                    }
                    else
                    {
                        for (int i = 0; i < Settings.rvSettings.DirMappings.Count; i++)
                        {
                            if (Settings.rvSettings.DirMappings[i].DirKey == datLocation)
                            {
                                Settings.rvSettings.DirMappings.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);
            UpdateGrid();
        }

        /// <summary>
        /// Resets all directory mappings to defaults.
        /// </summary>
        private async void BtnResetAllClick(object? sender, RoutedEventArgs e)
        {
            bool ok = await MessageBoxWindow.ShowConfirm(
                this,
                "Reset all directory mappings to defaults?",
                "Confirm Reset",
                okText: "Reset",
                cancelText: "Cancel");
            if (!ok) return;

            Settings.rvSettings.ResetDirMappings();
            Settings.WriteConfig(Settings.rvSettings);
            _rule = Settings.rvSettings.DirMappings[0];
            UpdateGrid();
            SetDisplay();
        }

        private void BtnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles double-click on the data grid to edit a rule.
        /// </summary>
        private void DataGridGamesDoubleClick(object? sender, global::Avalonia.Input.TappedEventArgs e)
        {
            var dgRules = this.FindControl<DataGrid>("DGDirectoryMappingRules");
            if (dgRules?.SelectedItem == null) return;

            if (dgRules.SelectedItem is DirMappingViewModel vm)
            {
                Title = "Edit Existing Directory / DATs Mapping";
                _rule = FindRule(vm.DirKey);
                UpdateGrid();
                SetDisplay();
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var btnCopyRulePath = this.FindControl<Button>("btnCopyRulePath");
            if (btnCopyRulePath != null)
            {
                btnCopyRulePath.Click += async (_, _) =>
                {
                    var tb = this.FindControl<TextBox>("txtDATLocation");
                    await CopyToClipboard(tb?.Text);
                };
            }

            var btnCopyDirPath = this.FindControl<Button>("btnCopyDirPath");
            if (btnCopyDirPath != null)
            {
                btnCopyDirPath.Click += async (_, _) =>
                {
                    var tb = this.FindControl<TextBox>("txtROMLocation");
                    await CopyToClipboard(tb?.Text);
                };
            }
        }
    }

    /// <summary>
    /// ViewModel for displaying directory mappings in the DataGrid.
    /// </summary>
    public class DirMappingViewModel
    {
        public string DirKey { get; set; }
        public string DirPath { get; set; }
        public IBrush BgColor { get; set; }

        public DirMappingViewModel(DirMapping mapping)
        {
            DirKey = mapping.DirKey;
            DirPath = mapping.DirPath;
            BgColor = Brushes.Transparent;
        }
    }
}
