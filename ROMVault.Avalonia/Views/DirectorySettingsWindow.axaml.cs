using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Compress;
using Compress.ZipFile;
using DATReader.DatClean;

namespace ROMVault.Avalonia.Views;

/// <summary>
    /// Window for configuring DAT and directory settings.
    /// Allows setting compression, merging, filtering, and header options for specific directories.
    /// </summary>
    public partial class DirectorySettingsWindow : Window
    {
        private Color _cMagenta = Color.FromRgb(255, 214, 255);
        private Color _cGreen = Color.FromRgb(214, 255, 214);
        private Color _cYellow = Color.FromRgb(255, 255, 214);

        private DatRule _rule = null!;
        public bool ChangesMade;
        private bool _displayType;

        private ObservableCollection<DatRuleViewModel> _datRules;
        private ObservableCollection<string> _categories;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectorySettingsWindow"/> class.
        /// </summary>
        public DirectorySettingsWindow()
        {
            InitializeComponent();
            InitializeControls();
            
            _datRules = new ObservableCollection<DatRuleViewModel>();
            DataGridGames.ItemsSource = _datRules;
            DataGridGames.SelectionChanged += DataGridGames_SelectionChanged;

            _categories = new ObservableCollection<string>();
            dgCategories.ItemsSource = _categories;
        }

        /// <summary>
        /// Sets up the dropdown items and event handlers.
        /// </summary>
        private void InitializeControls()
        {
            cboFileType.Items.Add("Uncompressed");
            cboFileType.Items.Add("Zip");
            cboFileType.Items.Add("SevenZip");
            cboFileType.Items.Add("Mixed (Archive as File)");

            cboMergeType.Items.Add("Nothing");
            cboMergeType.Items.Add("Split");
            cboMergeType.Items.Add("Merge");
            cboMergeType.Items.Add("NonMerge");

            cboFilterType.Items.Add("Roms & CHDs");
            cboFilterType.Items.Add("Roms Only");
            cboFilterType.Items.Add("CHDs Only");

            cboDirType.Items.Add("Use subdirs for all sets");
            cboDirType.Items.Add("Do not use subdirs for sets");
            cboDirType.Items.Add("Use subdirs for rom name conflicts");
            cboDirType.Items.Add("Use subdirs for multi-rom sets");
            cboDirType.Items.Add("Use subdirs for multi-rom sets or set/rom name mismatches");

            cboHeaderType.Items.Add("Optional");
            cboHeaderType.Items.Add("Headered");
            cboHeaderType.Items.Add("Headerless");

            cboFileType.SelectionChanged += (s, e) => SetCompressionTypeFromArchive();
            chkSingleArchive.Click += (s, e) => cboDirType.IsEnabled = chkSingleArchive.IsChecked == true;
            chkAddCategorySubDirs.Click += (s, e) => ToggleCategoryList();
            
            btnSet.Click += BtnApplyClick;
            btnDelete.Click += BtnDeleteClick;
            btnDeleteSelected.Click += BtnDeleteSelectedClick;
            btnResetAll.Click += BtnResetAllClick;
            btnClose.Click += BtnCloseClick;
            
            btnUp.Click += BtnUpClick;
            btnDown.Click += BtnDownClick;
        }

        /// <summary>
        /// Sets the directory location to edit settings for.
        /// </summary>
        /// <param name="dLocation">The directory key (tree path).</param>
        public void SetLocation(string dLocation)
        {
            dLocation = NormalizeDirKey(dLocation);
            Settings.rvSettings.DatRules.Sort();
            _rule = FindRule(dLocation);
            SetDisplay();
            UpdateGrid();
        }

        /// <summary>
        /// Configures the window display mode (Global vs Specific).
        /// </summary>
        /// <param name="type">If true, shows specific directory settings. If false, shows global settings list.</param>
        public void SetDisplayType(bool type)
        {
            _displayType = type;
            btnDelete.IsVisible = type;

            var bottomGrid = this.FindControl<Grid>("BottomGrid");
            if (bottomGrid != null)
            {
                bottomGrid.IsVisible = !type;
            }
        }

        /// <summary>
        /// Finds an existing DAT rule or creates a new one for the given location.
        /// </summary>
        private static DatRule FindRule(string dLocation)
        {
            dLocation = NormalizeDirKey(dLocation);
            foreach (DatRule t in Settings.rvSettings.DatRules)
            {
                if (string.Equals(NormalizeDirKey(t.DirKey), dLocation, StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            return new DatRule { DirKey = dLocation, IgnoreFiles = new List<string>() };
        }

        private static string NormalizeDirKey(string? dirKey)
        {
            if (string.IsNullOrWhiteSpace(dirKey))
                return "";

            dirKey = dirKey.Trim().Replace('/', '\\');
            while (dirKey.EndsWith("\\", StringComparison.Ordinal))
                dirKey = dirKey.Substring(0, dirKey.Length - 1);
            return dirKey;
        }

        /// <summary>
        /// Updates the compression options based on the selected archive type.
        /// </summary>
        private void SetCompressionTypeFromArchive()
        {
            cboCompression.Items.Clear();
            switch (cboFileType.SelectedIndex)
            {
                case 0:
                    chkFileTypeOverride.IsEnabled = true;
                    cboCompression.IsEnabled = false;
                    chkConvertWhenFixing.IsEnabled = false;
                    break;
                case 1:
                    chkFileTypeOverride.IsEnabled = true;
                    cboCompression.Items.Add("Deflate - Trrntzip");
                    cboCompression.Items.Add("ZSTD");
                    cboCompression.IsEnabled = true;
                    chkConvertWhenFixing.IsEnabled = true;
                    if (_rule.CompressionSub == ZipStructure.ZipTrrnt)
                        cboCompression.SelectedIndex = 0;
                    else if (_rule.CompressionSub == ZipStructure.ZipZSTD)
                        cboCompression.SelectedIndex = 1;
                    else
                        cboCompression.SelectedIndex = 0;
                    break;
                case 2:
                    chkFileTypeOverride.IsEnabled = true;
                    cboCompression.Items.Add("LZMA Solid - rv7z");
                    cboCompression.Items.Add("LZMA Non-Solid");
                    cboCompression.Items.Add("ZSTD Solid");
                    cboCompression.Items.Add("ZSTD Non-Solid");
                    cboCompression.IsEnabled = true;
                    chkConvertWhenFixing.IsEnabled = true;
                    if (_rule.CompressionSub == ZipStructure.SevenZipSLZMA)
                        cboCompression.SelectedIndex = 0;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipNLZMA)
                        cboCompression.SelectedIndex = 1;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipSZSTD)
                        cboCompression.SelectedIndex = 2;
                    else if (_rule.CompressionSub == ZipStructure.SevenZipNZSTD)
                        cboCompression.SelectedIndex = 3;
                    else
                        cboCompression.SelectedIndex = 0;
                    break;
                case 3:
                    chkFileTypeOverride.IsEnabled = false;
                    cboCompression.IsEnabled = false;
                    chkConvertWhenFixing.IsEnabled = false;
                    break;
            }
        }

        /// <summary>
        /// Updates the UI controls with the current rule values.
        /// </summary>
        private void SetDisplay()
        {
            txtDATLocation.Text = _rule.DirKey;

            cboFileType.SelectedIndex = _rule.Compression == FileType.FileOnly ? 3 : (int)_rule.Compression - 1;
            chkFileTypeOverride.IsChecked = _rule.CompressionOverrideDAT;

            SetCompressionTypeFromArchive();
            chkConvertWhenFixing.IsChecked = _rule.ConvertWhileFixing;

            cboMergeType.SelectedIndex = (int)_rule.Merge;
            chkMergeTypeOverride.IsChecked = _rule.MergeOverrideDAT;

            cboFilterType.SelectedIndex = (int)_rule.Filter;

            chkMultiDatDirOverride.IsChecked = _rule.MultiDATDirOverride;
            chkUseDescription.IsChecked = _rule.UseDescriptionAsDirName;
            chkUseIdForName.IsChecked = _rule.UseIdForName;

            chkSingleArchive.IsChecked = _rule.SingleArchive;

            cboDirType.IsEnabled = chkSingleArchive.IsChecked == true;
            cboDirType.SelectedIndex = (int)_rule.SubDirType;

            cboHeaderType.SelectedIndex = (int)_rule.HeaderType;

            textBox1.Text = "";
            foreach (string file in _rule.IgnoreFiles)
            {
                textBox1.Text += file + Environment.NewLine;
            }

            chkCompleteOnly.IsChecked = _rule.CompleteOnly;

            chkAddCategorySubDirs.IsChecked = _rule.AddCategorySubDirs;
            if (_rule.AddCategorySubDirs)
                SetCategoryList();
            else
                ToggleCategoryList();
        }

        /// <summary>
        /// Updates the DataGrid with the list of DAT rules.
        /// Applies color coding based on rule status.
        /// </summary>
        private void UpdateGrid()
        {
            _datRules.Clear();
            string rootKey = NormalizeDirKey(_rule.DirKey);
            foreach (DatRule t in Settings.rvSettings.DatRules)
            {
                string tKey = NormalizeDirKey(t.DirKey);
                var vm = new DatRuleViewModel(t);
                
                if (t.DirPath == "ToSort")
                {
                     vm.Background = new SolidColorBrush(Down(_cMagenta));
                }
                else if (tKey.Equals(rootKey, StringComparison.OrdinalIgnoreCase))
                {
                     vm.Background = new SolidColorBrush(Down(_cGreen));
                }
                else if (tKey.Length > rootKey.Length)
                {
                    if (tKey.StartsWith(rootKey + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                         vm.Background = new SolidColorBrush(Down(_cYellow));
                    }
                }
                _datRules.Add(vm);
            }
        }

        private static bool IsRuleRelevant(string rootKey, string ruleKey)
        {
            if (string.IsNullOrWhiteSpace(ruleKey))
                return false;
            if (string.IsNullOrWhiteSpace(rootKey))
                return true;

            if (ruleKey.Equals(rootKey, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ruleKey.StartsWith(rootKey + "\\", StringComparison.OrdinalIgnoreCase))
                return true;

            if (rootKey.StartsWith(ruleKey + "\\", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static Color Down(Color c)
        {
            if (Settings.rvSettings.Darkness)
            {
                return Color.FromRgb(
                    (byte)(c.R * 0.8),
                    (byte)(c.G * 0.8),
                    (byte)(c.B * 0.8));
            }
            return c;
        }

        /// <summary>
        /// Handles selection change in the DataGrid to load a rule.
        /// </summary>
        private void DataGridGames_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataGridGames.SelectedItem is DatRuleViewModel vm)
            {
                _rule = vm.Rule;
                UpdateGrid();
                SetDisplay();
            }
        }

        /// <summary>
        /// Determines the ZipStructure based on selected checkboxes.
        /// </summary>
        private ZipStructure ReadFromCheckBoxes()
        {
            if (cboFileType.SelectedIndex == 0)
                return ZipStructure.None;

            else if (cboFileType.SelectedIndex == 1)
            {
                if (cboCompression.SelectedIndex == 0)
                    return ZipStructure.ZipTrrnt;
                if (cboCompression.SelectedIndex == 1)
                    return ZipStructure.ZipZSTD;
            }
            else if (cboFileType.SelectedIndex == 2)
            {
                if (cboCompression.SelectedIndex == 0)
                    return ZipStructure.SevenZipSLZMA;
                if (cboCompression.SelectedIndex == 1)
                    return ZipStructure.SevenZipNLZMA;
                if (cboCompression.SelectedIndex == 2)
                    return ZipStructure.SevenZipSZSTD;
                if (cboCompression.SelectedIndex == 3)
                    return ZipStructure.SevenZipNZSTD;
            }
            else if (cboFileType.SelectedIndex == 3)
                return ZipStructure.None;

            return ZipStructure.None;
        }

        /// <summary>
        /// Applies changes to the current rule and saves settings.
        /// </summary>
        private void BtnApplyClick(object? sender, RoutedEventArgs e)
        {
            ChangesMade = true;

            _rule.DirKey = NormalizeDirKey(_rule.DirKey);
            _rule.Compression = cboFileType.SelectedIndex == 3 ? FileType.FileOnly : (FileType)cboFileType.SelectedIndex + 1;
            _rule.CompressionOverrideDAT = chkFileTypeOverride.IsChecked == true;
            _rule.CompressionSub = ReadFromCheckBoxes();
            _rule.ConvertWhileFixing = chkConvertWhenFixing.IsChecked == true;
            _rule.Merge = (MergeType)cboMergeType.SelectedIndex;
            _rule.MergeOverrideDAT = chkMergeTypeOverride.IsChecked == true;
            _rule.Filter = (FilterType)cboFilterType.SelectedIndex;
            _rule.HeaderType = (HeaderType)cboHeaderType.SelectedIndex;
            _rule.SingleArchive = chkSingleArchive.IsChecked == true;
            _rule.SubDirType = (RemoveSubType)cboDirType.SelectedIndex;
            _rule.MultiDATDirOverride = chkMultiDatDirOverride.IsChecked == true;
            _rule.UseDescriptionAsDirName = chkUseDescription.IsChecked == true;
            _rule.UseIdForName = chkUseIdForName.IsChecked == true;

            _rule.CompleteOnly = chkCompleteOnly.IsChecked == true;

            _rule.AddCategorySubDirs = chkAddCategorySubDirs.IsChecked == true;
            if (_rule.AddCategorySubDirs)
                SetCategoryList();
            else
                ToggleCategoryList();

            string strtxt = textBox1.Text ?? "";
            strtxt = strtxt.Replace("\r", "");
            string[] strsplit = strtxt.Split('\n');

            _rule.IgnoreFiles = new List<string>(strsplit);
            int i;
            for (i = 0; i < _rule.IgnoreFiles.Count; i++)
            {
                _rule.IgnoreFiles[i] = _rule.IgnoreFiles[i].Trim();
                if (string.IsNullOrEmpty(_rule.IgnoreFiles[i]))
                {
                    _rule.IgnoreFiles.RemoveAt(i);
                    i--;
                }
            }

            bool updatingRule = false;
            for (i = 0; i < Settings.rvSettings.DatRules.Count; i++)
            {
                if (Settings.rvSettings.DatRules[i] == _rule)
                {
                    updatingRule = true;
                    break;
                }

                string left = NormalizeDirKey(Settings.rvSettings.DatRules[i].DirKey);
                string right = NormalizeDirKey(_rule.DirKey);
                if (string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    break;
                }
            }

            if (!updatingRule)
                Settings.rvSettings.DatRules.Insert(i, _rule);

            Settings.rvSettings.SetRegExRules();
            Settings.rvSettings.DatRules.Sort();

            UpdateGrid();
            Settings.WriteConfig(Settings.rvSettings);
            DatUpdate.CheckAllDats(DB.DirRoot.Child(0), _rule.DirKey);

            if (_displayType)
                Close();
        }

        /// <summary>
        /// Deletes the current rule.
        /// </summary>
        private async void BtnDeleteClick(object? sender, RoutedEventArgs e)
        {
            string datLocation = _rule.DirKey;

            if (datLocation == "RomVault")
            {
                await MessageBoxWindow.ShowInfo(this, "The 'RomVault' directory settings cannot be deleted.", "Not Allowed");
                return;
            }
            else
            {
                bool ok = await MessageBoxWindow.ShowConfirm(
                    this,
                    $"Delete directory settings for:\r\n\r\n{datLocation}\r\n\r\nThis will remove the rule from settings.",
                    "Confirm Delete",
                    okText: "Delete",
                    cancelText: "Cancel");
                if (!ok) return;

                ChangesMade = true;

                DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                for (int i = 0; i < Settings.rvSettings.DatRules.Count; i++)
                {
                    if (Settings.rvSettings.DatRules[i].DirKey == datLocation)
                    {
                        Settings.rvSettings.DatRules.RemoveAt(i);
                        i--;
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);

            UpdateGrid();
            Close();
        }

        /// <summary>
        /// Deletes the selected rules from the grid.
        /// </summary>
        private async void BtnDeleteSelectedClick(object? sender, RoutedEventArgs e)
        {
            var selectedItems = DataGridGames.SelectedItems;
            if (selectedItems == null) return;

            int deleteCount = 0;
            foreach (var item in selectedItems)
            {
                if (item is DatRuleViewModel vm && vm.DirKey != "RomVault")
                    deleteCount++;
            }
            if (deleteCount == 0) return;

            bool ok = await MessageBoxWindow.ShowConfirm(
                this,
                $"Delete {deleteCount} selected rule(s)?",
                "Confirm Delete",
                okText: "Delete",
                cancelText: "Cancel");
            if (!ok) return;

            ChangesMade = true;
            foreach (var item in selectedItems)
            {
                if (item is DatRuleViewModel vm)
                {
                    string datLocation = vm.DirKey;
                    if (datLocation == "RomVault") continue;

                    DatUpdate.CheckAllDats(DB.DirRoot.Child(0), datLocation);
                    for (int i = 0; i < Settings.rvSettings.DatRules.Count; i++)
                    {
                        if (Settings.rvSettings.DatRules[i].DirKey == datLocation)
                        {
                            Settings.rvSettings.DatRules.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            Settings.WriteConfig(Settings.rvSettings);

            UpdateGrid();
        }

        /// <summary>
        /// Resets all rules to defaults.
        /// </summary>
        private async void BtnResetAllClick(object? sender, RoutedEventArgs e)
        {
            bool ok = await MessageBoxWindow.ShowConfirm(
                this,
                "Reset all directory settings rules to defaults?",
                "Confirm Reset",
                okText: "Reset",
                cancelText: "Cancel");
            if (!ok) return;

            ChangesMade = true;
            // Logic from WinForms
            Settings.rvSettings.ResetDatRules();
            Settings.WriteConfig(Settings.rvSettings);
            _rule = Settings.rvSettings.DatRules[0];
            UpdateGrid();
            SetDisplay();
        }

        private void BtnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Toggles the enabled state of the category list based on the checkbox.
        /// Loads default categories if the list is empty.
        /// </summary>
        private void ToggleCategoryList()
        {
            bool enabled = chkAddCategorySubDirs.IsChecked == true;
            dgCategories.IsEnabled = enabled;
            btnUp.IsEnabled = enabled;
            btnDown.IsEnabled = enabled;

            if (enabled)
            {
                 if (_rule.CategoryOrder == null || _rule.CategoryOrder.Count == 0)
                {
                    _rule.CategoryOrder = new List<string>()
                    {
                        "Preproduction", "Educational", "Guides", "Manuals", "Magazines", "Documents",
                        "Audio", "Video", "Multimedia", "Coverdiscs", "Covermount", "Bonus Discs",
                        "Bonus", "Add-Ons", "Source Code", "Updates", "Applications", "Demos",
                        "Games", "Miscellaneous"
                    };
                }
                SetCategoryList();
            }
        }

        /// <summary>
        /// Refreshes the category list in the UI from the rule's category order.
        /// </summary>
        private void SetCategoryList()
        {
            _categories.Clear();
            if (_rule.CategoryOrder != null)
            {
                foreach (string s in _rule.CategoryOrder)
                {
                    _categories.Add(s);
                }
            }
        }

        /// <summary>
        /// Moves the selected category up in the list.
        /// </summary>
        private void BtnUpClick(object? sender, RoutedEventArgs e)
        {
            int idx = dgCategories.SelectedIndex;
            if (idx <= 0) return;
            
            string v = _rule.CategoryOrder[idx];
            _rule.CategoryOrder[idx] = _rule.CategoryOrder[idx - 1];
            _rule.CategoryOrder[idx - 1] = v;
            
            SetCategoryList();
            dgCategories.SelectedIndex = idx - 1;
        }

        /// <summary>
        /// Moves the selected category down in the list.
        /// </summary>
        private void BtnDownClick(object? sender, RoutedEventArgs e)
        {
            int idx = dgCategories.SelectedIndex;
            if (idx < 0 || idx >= _rule.CategoryOrder.Count - 1) return;

            string v = _rule.CategoryOrder[idx];
            _rule.CategoryOrder[idx] = _rule.CategoryOrder[idx + 1];
            _rule.CategoryOrder[idx + 1] = v;

            SetCategoryList();
            dgCategories.SelectedIndex = idx + 1;
        }
    }

    /// <summary>
    /// ViewModel for displaying DAT rules in the DataGrid.
    /// </summary>
    public class DatRuleViewModel
    {
        public DatRule Rule { get; }
        public string DirKey => Rule.DirKey;
        public ZipStructure CompressionSub => Rule.CompressionSub;
        public MergeType Merge => Rule.Merge;
        public bool SingleArchive => Rule.SingleArchive;
        public IBrush Background { get; set; } = Brushes.Transparent;

        public DatRuleViewModel(DatRule rule)
        {
            Rule = rule;
        }
    }
