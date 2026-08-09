using Avalonia.Controls;
using Avalonia.Interactivity;
using RomVaultCore.RvDB;
using RomVaultCore;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using System;
using RomVaultCore.Utils;
using RomVaultCore.Scanner;
using RomVaultCore.ReadDat;
using RomVaultCore.FindFix;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using System.IO;
using DATReader.DatStore;
using DATReader.DatWriter;
using Avalonia.Media.Imaging;
using Compress;
using Compress.ZipFile;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;
using ROMVault.Avalonia.Utils;
using ROMVault.Avalonia.ViewModels;
using ROMVault.Avalonia.Services;
using System.Threading.Tasks;
using Path = System.IO.Path;
using File = System.IO.File;

namespace ROMVault.Avalonia;

/// <summary>
/// The main window of the ROMVault Avalonia application.
/// Handles the primary UI logic, including the directory tree, game grid, and main menu actions.
/// </summary>
public partial class MainWindow : Window
{
    private RvFile? _gameGridSource;
    private readonly GameBrowserViewModel _gameBrowser = new();
    private readonly RomBrowserViewModel _romBrowser = new();
    private readonly MediaInspectorViewModel _mediaInspector = new(new GameMediaPreviewService());
    private readonly MainWindowViewModel _viewModel;
    private readonly BackgroundOperationRunner _operationRunner;
    private bool _updatingGameGrid;
    private bool _working = false;
    private GridLength _lastArtworkWidth = new(300);
    private bool _inspectorAutoCollapsed;
    private bool _applyingResponsiveLayout;
    private DispatcherTimer? _filterDebounceTimer;
    private static readonly string[] StatusTokenSuggestions =
    {
        "missing",
        "fixes",
        "unknown",
        "mia",
        "merged",
        "intosort",
        "complete",
        "partial",
        "empty",
        "corrupt"
    };

    private string? _gameSortHeader;
    private bool _gameSortAsc = true;
    private string? _romSortHeader;
    private bool _romSortAsc = true;

    private const string UiStatePrefix = "MainWindow";

    private RvFile? SelectedGame => (GameGrid.SelectedItem as GameRowViewModel)?.Source;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowComplete => GameHeader.ShowComplete;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowPartial => GameHeader.ShowPartial;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowEmpty => GameHeader.ShowEmpty;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowFixes => GameHeader.ShowFixes;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowMIA => GameHeader.ShowMia;
    private global::Avalonia.Controls.Primitives.ToggleButton chkBoxShowMerged => GameHeader.ShowMerged;
    private TextBox txtFilter => GameHeader.Filter;
    private Button btnClear => GameHeader.Clear;
    private Button btnFilterHelp => GameHeader.Help;
    private global::Avalonia.Controls.Primitives.Popup FilterSuggestionsPopup => GameHeader.FilterPopup;
    private ListBox FilterSuggestionsList => GameHeader.FilterSuggestions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// Sets up the directory tree, event handlers, and initial status aggregation.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(
            _gameBrowser,
            _romBrowser,
            _mediaInspector,
            new MainWindowActions(
                UpdateDatsIfIdle,
                UpdateAllDatsIfIdle,
                ScanRomsIfIdle,
                FindFixesIfIdle,
                FixFilesIfIdle,
                CreateFixDatReportAsync,
                CreateFullReportAsync,
                CreateFixReportAsync,
                OpenSettingsAsync,
                OpenGlobalDirectorySettingsAsync,
                OpenGlobalDirectoryMappingsAsync,
                AddToSortAsync,
                OpenTorrentZip,
                () => DesktopShellService.Instance.OpenUrl("https://wiki.romvault.com/doku.php?id=help"),
                OpenColorKey,
                OpenShortcutsAsync,
                () => DesktopShellService.Instance.OpenUrl("https://wiki.romvault.com/doku.php?id=whats_new"),
                OpenAboutAsync,
                compact => ApplyCompactDensity(compact, true)));
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnShellPropertyChanged;
        _operationRunner = new BackgroundOperationRunner(this, Start, Finish);

        _viewModel.Status = string.Empty;
        _viewModel.Activity = string.Empty;
        
        // Initialize Tree
        var rvTree = RvTreeControl;
        var treeScrollViewer = TreeScrollViewer;
        var treeStatsHeader = TreeStatsHeader;
        var chkTreeStats = this.chkTreeStats;
        var lblTreeStatHave = this.lblTreeStatHave;
        var lblTreeStatMissing = this.lblTreeStatMissing;
        var lblTreeStatMia = this.lblTreeStatMia;
        var lblTreeStatFixes = this.lblTreeStatFixes;
        var lblTreeStatUnknown = this.lblTreeStatUnknown;

        var btnTreeAll = this.btnTreeAll;
        var btnTreeNil = this.btnTreeNil;
        if (rvTree != null)
        {
            rvTree.Setup(DB.DirRoot);
            rvTree.RvSelected += OnRvTreeSelected;
            rvTree.RvChecked += (s, e) =>
            {
                RepairStatus.ReportStatusReset(DB.DirRoot);
                DatSetSelected(rvTree.Selected);
            };
            rvTree.RvRightClicked += (s, e) =>
            {
                TreeContextMenu.Open(rvTree);
            };
        }

        if (treeStatsHeader != null && rvTree != null && chkTreeStats != null)
        {
            bool applying = false;

            void ApplyStatsEnabled(bool enabled)
            {
                if (applying) return;
                applying = true;

                rvTree.ShowStats = enabled;
                rvTree.StatColumns = enabled
                    ? ROMVault.Avalonia.Views.RvTree.TreeStatColumns.Have |
                      ROMVault.Avalonia.Views.RvTree.TreeStatColumns.Missing |
                      ROMVault.Avalonia.Views.RvTree.TreeStatColumns.MIA
                    : ROMVault.Avalonia.Views.RvTree.TreeStatColumns.None;

                AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.Tree.ShowStats", enabled ? "1" : "0");

                treeStatsHeader.IsVisible = true;

                if (lblTreeStatHave != null) lblTreeStatHave.IsVisible = enabled;
                if (lblTreeStatMissing != null) lblTreeStatMissing.IsVisible = enabled;
                if (lblTreeStatMia != null) lblTreeStatMia.IsVisible = enabled;
                if (lblTreeStatFixes != null) lblTreeStatFixes.IsVisible = false;
                if (lblTreeStatUnknown != null) lblTreeStatUnknown.IsVisible = false;

                if (treeStatsHeader.ColumnDefinitions.Count >= 6)
                {
                    treeStatsHeader.ColumnDefinitions[1].Width = enabled ? new GridLength(56) : new GridLength(0);
                    treeStatsHeader.ColumnDefinitions[2].Width = enabled ? new GridLength(56) : new GridLength(0);
                    treeStatsHeader.ColumnDefinitions[3].Width = enabled ? new GridLength(56) : new GridLength(0);
                    treeStatsHeader.ColumnDefinitions[4].Width = new GridLength(0);
                    treeStatsHeader.ColumnDefinitions[5].Width = new GridLength(0);
                }

                chkTreeStats.IsChecked = enabled;
                rvTree.InvalidateVisual();
                applying = false;
            }

            bool enabled = AppSettings.ReadSetting($"{UiStatePrefix}.Tree.ShowStats") == "1";
            ApplyStatsEnabled(enabled);

            chkTreeStats.IsCheckedChanged += (_, _) =>
            {
                if (applying) return;
                ApplyStatsEnabled(chkTreeStats.IsChecked == true);
            };
        }

        if (btnTreeAll != null)
            btnTreeAll.Click += (_, _) => ApplyTreeCheckAll(true);

        if (btnTreeNil != null)
            btnTreeNil.Click += (_, _) => ApplyTreeCheckAll(false);

        if (treeScrollViewer != null && rvTree != null)
        {
            void SyncTreeViewport()
            {
                rvTree.ViewportWidth = treeScrollViewer.Viewport.Width;
                rvTree.ViewportHeight = treeScrollViewer.Viewport.Height;
                rvTree.ViewportOffsetX = treeScrollViewer.Offset.X;
                rvTree.ViewportOffsetY = treeScrollViewer.Offset.Y;
                rvTree.InvalidateVisual();
            }

            treeScrollViewer.SizeChanged += (_, _) => SyncTreeViewport();
            treeScrollViewer.PropertyChanged += (_, e) =>
            {
                if (e.Property == ScrollViewer.OffsetProperty)
                    SyncTreeViewport();
            };
            SyncTreeViewport();
        }

        // Ensure status is calculated
        if (DB.DirRoot != null)
        {
             // Force initialization of RepairStatus
             RepairStatus.InitStatusCheck();
             // Force aggregation of DirStatus since it's not persisted and RepStatusReset might skip it
             AggregateDirStatus(DB.DirRoot);
        }

        // Initialize Events
        chkBoxShowComplete.Click += (s, e) => UpdateGameGrid();
        chkBoxShowPartial.Click += (s, e) => UpdateGameGrid();
        chkBoxShowFixes.Click += (s, e) => UpdateGameGrid();
        chkBoxShowMIA.Click += (s, e) => UpdateGameGrid();
        chkBoxShowMerged.Click += (s, e) => UpdateGameGrid();
        chkBoxShowEmpty.Click += (s, e) => UpdateGameGrid();
        
        txtFilter.TextChanged += (s, e) =>
        {
            UpdateFilterSuggestions();
            ScheduleGameGridUpdate();
        };
        txtFilter.KeyDown += OnFilterKeyDown;
        btnClear.Click += (s, e) => { txtFilter.Text = ""; };
        btnFilterHelp.Click += async (_, _) => await ShowFilterHelp();

        GameGrid.SelectionChanged += GameGrid_SelectionChanged;
        GameGrid.DoubleTapped += GameGrid_DoubleTapped;
        GameGrid.Sorting += OnGameGridSorting;
        RomGrid.Sorting += OnRomGridSorting;
        GameGrid.KeyDown += OnGridCopyKeyDown;
        RomGrid.KeyDown += OnGridCopyKeyDown;
        GameGrid.PointerPressed += OnGridPointerPressedCopy;
        RomGrid.PointerPressed += OnGridPointerPressedCopy;

        FilterSuggestionsList.DoubleTapped += (_, _) => ApplySelectedFilterSuggestion();
        FilterSuggestionsList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplySelectedFilterSuggestion();
                e.Handled = true;
            }
        };

        SetupColumnMenus();
        LoadUiState();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
        UpdateTreePresetTooltips();
        Closing += (_, _) =>
        {
            SaveUiState();
            _filterDebounceTimer?.Stop();
            _mediaInspector.Dispose();
            AppSettings.Flush();
        };
    }

    /// <summary>
    /// Hooks the "Columns" context menu so it gets rebuilt each time it opens.
    /// This keeps the visible/hidden state in sync with the live grid columns.
    /// </summary>
    private void SetupColumnMenus()
    {
        if (GameGrid.ContextMenu != null)
        {
            GameGrid.ContextMenu.Opened += (_, _) =>
            {
                PopulateColumnsMenu(GameGrid, GameGridColumnsMenu, $"{UiStatePrefix}.GameGrid");
            };
        }

        if (RomGrid.ContextMenu != null)
        {
            RomGrid.ContextMenu.Opened += (_, _) =>
            {
                PopulateColumnsMenu(RomGrid, RomGridColumnsMenu, $"{UiStatePrefix}.RomGrid");
            };
        }
    }

    /// <summary>
    /// Builds a column visibility menu and persists changes in AppSettings.
    /// </summary>
    private static void PopulateColumnsMenu(DataGrid grid, MenuItem hostMenu, string keyPrefix)
    {
        hostMenu.Items.Clear();

        foreach (var col in grid.Columns)
        {
            string header = col.Header?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(header))
                continue;

            var mi = new MenuItem
            {
                Header = header,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = col.IsVisible
            };

            mi.Click += (_, _) =>
            {
                col.IsVisible = mi.IsChecked;
                AppSettings.AddUpdateAppSettings($"{keyPrefix}.col.{SanitizeKey(header)}.visible", col.IsVisible ? "1" : "0");
            };

            hostMenu.Items.Add(mi);
        }
    }

    /// <summary>
    /// Normalizes a column header into a settings-safe key.
    /// </summary>
    private static string SanitizeKey(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int idx = 0;
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer[idx++] = c;
            else
                buffer[idx++] = '_';
        }
        return new string(buffer[..idx]);
    }

    private void LoadUiState()
    {
        var mainSplitGrid = MainSplitGrid;
        if (mainSplitGrid != null && mainSplitGrid.ColumnDefinitions.Count >= 3)
        {
            string? leftWidth = AppSettings.ReadSetting($"{UiStatePrefix}.MainSplit.LeftWidth");
            if (double.TryParse(leftWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w >= 240)
            {
                mainSplitGrid.ColumnDefinitions[0].Width = new GridLength(w);
            }
        }

        string? artworkWidth = AppSettings.ReadSetting($"{UiStatePrefix}.Artwork.Width");
        if (double.TryParse(artworkWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var aw) && aw >= 120)
        {
            _lastArtworkWidth = new GridLength(aw);
        }

        _viewModel.IsInspectorOpen = AppSettings.ReadSetting($"{UiStatePrefix}.Inspector.Open") != "0";

        LoadDataGridState(GameGrid, $"{UiStatePrefix}.GameGrid");
        LoadDataGridState(RomGrid, $"{UiStatePrefix}.RomGrid");

        _gameSortHeader = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.SortHeader");
        _gameSortAsc = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.SortAsc") != "0";
        _romSortHeader = AppSettings.ReadSetting($"{UiStatePrefix}.RomGrid.SortHeader");
        _romSortAsc = AppSettings.ReadSetting($"{UiStatePrefix}.RomGrid.SortAsc") != "0";
        ApplyCompactDensity(AppSettings.ReadSetting($"{UiStatePrefix}.CompactDensity") == "1", false);
        ApplyInspectorLayout();
    }

    private void SaveUiState()
    {
        var mainSplitGrid = MainSplitGrid;
        if (mainSplitGrid != null && mainSplitGrid.ColumnDefinitions.Count >= 3)
        {
            AppSettings.AddUpdateAppSettings(
                $"{UiStatePrefix}.MainSplit.LeftWidth",
                mainSplitGrid.ColumnDefinitions[0].Width.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (GameListGrid != null && GameListGrid.ColumnDefinitions.Count >= 3 && GameListGrid.ColumnDefinitions[2].Width.Value > 0)
        {
            _lastArtworkWidth = GameListGrid.ColumnDefinitions[2].Width;
        }

        if (_lastArtworkWidth.Value > 0)
        {
            AppSettings.AddUpdateAppSettings(
                $"{UiStatePrefix}.Artwork.Width",
                _lastArtworkWidth.Value.ToString(CultureInfo.InvariantCulture));
        }

        SaveDataGridState(GameGrid, $"{UiStatePrefix}.GameGrid");
        SaveDataGridState(RomGrid, $"{UiStatePrefix}.RomGrid");

        if (!string.IsNullOrWhiteSpace(_gameSortHeader))
            AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.GameGrid.SortHeader", _gameSortHeader);
        AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.GameGrid.SortAsc", _gameSortAsc ? "1" : "0");

        if (!string.IsNullOrWhiteSpace(_romSortHeader))
            AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.RomGrid.SortHeader", _romSortHeader);
        AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.RomGrid.SortAsc", _romSortAsc ? "1" : "0");
        AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.Inspector.Open", _viewModel.IsInspectorOpen ? "1" : "0");

    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsInspectorOpen))
        {
            return;
        }

        if (!_applyingResponsiveLayout && _viewModel.IsInspectorOpen)
        {
            _inspectorAutoCollapsed = false;
        }

        ApplyInspectorLayout();
    }

    private void ApplyResponsiveLayout()
    {
        double width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        if (MainSplitGrid.ColumnDefinitions.Count >= 3)
        {
            MainSplitGrid.ColumnDefinitions[0].MinWidth = width < 900 ? 220 : 240;
            if (width < 900 && MainSplitGrid.ColumnDefinitions[0].Width.Value > 280)
            {
                MainSplitGrid.ColumnDefinitions[0].Width = new GridLength(260);
            }
        }

        if (width < 1120 && _viewModel.IsInspectorOpen && !_inspectorAutoCollapsed)
        {
            _inspectorAutoCollapsed = true;
            _applyingResponsiveLayout = true;
            _viewModel.IsInspectorOpen = false;
            _applyingResponsiveLayout = false;
        }
        else if (width > 1240 && _inspectorAutoCollapsed)
        {
            _inspectorAutoCollapsed = false;
            _applyingResponsiveLayout = true;
            _viewModel.IsInspectorOpen = true;
            _applyingResponsiveLayout = false;
        }

        ApplyInspectorLayout();
    }

    private void ApplyInspectorLayout()
    {
        if (GameListGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        ColumnDefinition inspectorColumn = GameListGrid.ColumnDefinitions[2];
        if (_viewModel.IsInspectorOpen)
        {
            inspectorColumn.MinWidth = 220;
            inspectorColumn.Width = _lastArtworkWidth.Value >= 120 ? _lastArtworkWidth : new GridLength(300);
            ArtworkSplitter.IsVisible = true;
            return;
        }

        if (inspectorColumn.Width.Value > 0)
        {
            _lastArtworkWidth = inspectorColumn.Width;
        }

        inspectorColumn.MinWidth = 0;
        inspectorColumn.Width = new GridLength(0);
        ArtworkSplitter.IsVisible = false;
    }

    private static void LoadDataGridState(DataGrid grid, string keyPrefix)
    {
        foreach (var col in grid.Columns)
        {
            string header = col.Header?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(header))
                continue;

            string safe = SanitizeKey(header);
            string? vis = AppSettings.ReadSetting($"{keyPrefix}.col.{safe}.visible");
            if (vis == "0") col.IsVisible = false;
            if (vis == "1") col.IsVisible = true;

            string? width = AppSettings.ReadSetting($"{keyPrefix}.col.{safe}.width");
            if (double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w >= 20)
            {
                col.Width = new DataGridLength(w);
            }
        }
    }

    private static void SaveDataGridState(DataGrid grid, string keyPrefix)
    {
        foreach (var col in grid.Columns)
        {
            string header = col.Header?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(header))
                continue;

            string safe = SanitizeKey(header);
            AppSettings.AddUpdateAppSettings($"{keyPrefix}.col.{safe}.visible", col.IsVisible ? "1" : "0");
            if (col.ActualWidth > 0)
            {
                AppSettings.AddUpdateAppSettings(
                    $"{keyPrefix}.col.{safe}.width",
                    col.ActualWidth.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private void ScheduleGameGridUpdate()
    {
        if (_filterDebounceTimer == null)
        {
            _filterDebounceTimer = new DispatcherTimer();
            _filterDebounceTimer.Interval = TimeSpan.FromMilliseconds(250);
            _filterDebounceTimer.Tick += (_, _) =>
            {
                _filterDebounceTimer?.Stop();
                UpdateGameGrid();
            };
        }

        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    /// <summary>
    /// Shows quick help for the filter syntax.
    /// </summary>
    private async Task ShowFilterHelp()
    {
        string msg =
            "Filter syntax:\r\n\r\n" +
            "- plain text: matches name + description\r\n" +
            "- desc:term: matches description only\r\n" +
            "- status:value: matches status tokens\r\n\r\n" +
            "Examples:\r\n" +
            "- mario\r\n" +
            "- desc:capcom\r\n" +
            "- status:missing\r\n" +
            "- status:fixes,unknown\r\n";
        await Views.MessageBoxWindow.ShowInfo(this, msg, "Filter Help");
    }

    /// <summary>
    /// Copies the current selection when Ctrl+C is pressed on either grid.
    /// </summary>
    private async void OnGridCopyKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control || e.Key != Key.C)
            return;

        if (sender is not DataGrid grid)
            return;

        if (grid == RomGrid)
        {
            if (RomGrid.SelectedItem is not RomRowViewModel row)
                return;

            string text = $"{row.DisplayName}\t{row.Size}\t{row.Crc32}\t{row.Sha1}\t{row.Md5}";
            await CopyTextToClipboard(text);
            _viewModel.Activity = "Copied";
            e.Handled = true;
            return;
        }

        if (grid == GameGrid)
        {
            RvFile? game = SelectedGame;
            if (game is null)
                return;

            string desc = game.Game?.GetData(RvGame.GameData.Description) ?? "";
            if (desc == "¤") desc = "";
            string text = string.IsNullOrWhiteSpace(desc) ? (game.Name ?? "") : $"{game.Name}\t{desc}";
            await CopyTextToClipboard(text);
            _viewModel.Activity = "Copied";
            e.Handled = true;
        }
    }

    /// <summary>
    /// Ctrl+clicking on a cell copies the cell text to the clipboard (quick copy workflow).
    /// </summary>
    private async void OnGridPointerPressedCopy(object? sender, PointerPressedEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            await CopyTextToClipboard(tb.Text);
            _viewModel.Activity = "Copied";
            e.Handled = true;
        }
    }

    /// <summary>
    /// Updates the small filter suggestion popup (currently for status: tokens).
    /// </summary>
    private void UpdateFilterSuggestions()
    {
        if (FilterSuggestionsPopup == null || FilterSuggestionsList == null || txtFilter == null)
            return;

        if (!txtFilter.IsFocused)
        {
            FilterSuggestionsPopup.IsOpen = false;
            return;
        }

        string text = txtFilter.Text ?? "";
        int lastSpace = text.LastIndexOf(' ');
        string token = lastSpace >= 0 ? text[(lastSpace + 1)..] : text;

        if (!token.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
        {
            FilterSuggestionsPopup.IsOpen = false;
            return;
        }

        string typed = token.Substring("status:".Length);
        string typedLower = typed.ToLowerInvariant();

        var items = StatusTokenSuggestions
            .Where(s => s.StartsWith(typedLower, StringComparison.OrdinalIgnoreCase))
            .Select(s => $"status:{s}")
            .ToArray();

        if (items.Length == 0)
        {
            FilterSuggestionsPopup.IsOpen = false;
            return;
        }

        FilterSuggestionsList.ItemsSource = items;
        if (FilterSuggestionsList.SelectedIndex < 0)
            FilterSuggestionsList.SelectedIndex = 0;
        FilterSuggestionsPopup.IsOpen = true;
    }

    /// <summary>
    /// Applies the currently selected suggestion by replacing the active token.
    /// </summary>
    private void ApplySelectedFilterSuggestion()
    {
        if (FilterSuggestionsPopup == null || FilterSuggestionsList == null || txtFilter == null)
            return;

        if (!FilterSuggestionsPopup.IsOpen)
            return;

        if (FilterSuggestionsList.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected))
            return;

        string text = txtFilter.Text ?? "";
        int lastSpace = text.LastIndexOf(' ');
        string prefix = lastSpace >= 0 ? text[..(lastSpace + 1)] : "";
        txtFilter.Text = prefix + selected;
        txtFilter.CaretIndex = txtFilter.Text.Length;
        FilterSuggestionsPopup.IsOpen = false;
        txtFilter.Focus();
    }

    /// <summary>
    /// Handles keyboard navigation inside the filter box, including suggestion navigation and committing the filter.
    /// </summary>
    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (FilterSuggestionsPopup?.IsOpen == true)
        {
            if (e.Key == Key.Down)
            {
                FilterSuggestionsList.SelectedIndex = Math.Min(FilterSuggestionsList.Items.Count - 1, FilterSuggestionsList.SelectedIndex + 1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                FilterSuggestionsList.SelectedIndex = Math.Max(0, FilterSuggestionsList.SelectedIndex - 1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                ApplySelectedFilterSuggestion();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                FilterSuggestionsPopup.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter)
        {
            FilterSuggestionsPopup!.IsOpen = false;
            UpdateGameGrid();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Captures and applies GameGrid sorting so it can be persisted and re-applied after refresh.
    /// </summary>
    private void OnGameGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        string header = e.Column.Header?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(header))
            return;

        if (string.Equals(_gameSortHeader, header, StringComparison.Ordinal))
            _gameSortAsc = !_gameSortAsc;
        else
        {
            _gameSortHeader = header;
            _gameSortAsc = true;
        }

        UpdateGameGrid();
    }

    /// <summary>
    /// Captures and applies RomGrid sorting so it can be persisted and re-applied after refresh.
    /// </summary>
    private void OnRomGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        string header = e.Column.Header?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(header))
            return;

        if (string.Equals(_romSortHeader, header, StringComparison.Ordinal))
            _romSortAsc = !_romSortAsc;
        else
        {
            _romSortHeader = header;
            _romSortAsc = true;
        }

        if (SelectedGame is { } game)
            UpdateRomGrid(game);
    }

    private void EnsureTreeNodeVisible(ROMVault.Avalonia.Views.RvTree rvTree, RvFile node)
    {
        var sv = TreeScrollViewer;
        if (sv == null)
            return;

        double? y = rvTree.GetRowTop(node);
        if (y == null)
            return;

        double target = Math.Max(0, y.Value - 40);
        sv.Offset = new global::Avalonia.Vector(sv.Offset.X, target);
    }

    private void GameGrid_DoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (SelectedGame is { FileType: FileType.Dir } tGame)
        {
            var rvTree = RvTreeControl;
            if (rvTree != null)
            {
                rvTree.SetSelected(tGame);
            }
        }
    }

    /// <summary>
    /// Recursively aggregates directory status counts from children to parents.
    /// This is necessary because DirStatus is not persisted and needs to be recalculated on load.
    /// </summary>
    /// <param name="dir">The directory to process.</param>
    private void AggregateDirStatus(RvFile dir)
    {
        if (dir.ChildCount == 0) return;

        // Reset the DirStatus for the current directory
        // Since we don't have a Clear method, we assume it's fresh (all zeros) on load.
        // If this is called multiple times, counts would be wrong, but we only call it once on startup.

        for (int i = 0; i < dir.ChildCount; i++)
        {
            RvFile child = dir.Child(i);
            
            if (child.IsDirectory)
            {
                AggregateDirStatus(child);
            }

            // Manually add child status to parent (dir)
            dir.DirStatus.RepStatusAddRemove(child.RepStatus, 1);
            
            if (child.IsDirectory)
            {
                dir.DirStatus.RepStatusArrayAddRemove(child.DirStatus, 1);
            }
        }
    }

    /// <summary>
    /// Handles the selection event from the RvTree control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The selected RvFile.</param>
    private void OnRvTreeSelected(object? sender, RvFile e)
    {
        if (sender is ROMVault.Avalonia.Views.RvTree tree)
        {
            EnsureTreeNodeVisible(tree, e);
        }
        DatSetSelected(e);
    }

    /// <summary>
    /// Updates the UI when a DAT or Directory is selected in the tree.
    /// Populates the DAT Info panel and updates the game grid.
    /// </summary>
    /// <param name="cf">The selected RvFile (Directory or DAT).</param>
    private void DatSetSelected(RvFile? cf)
    {
        if (cf == null) return;

        if (lblStatusLeft != null)
        {
            _viewModel.Status = cf.FullName;
        }

        _viewModel.DatDetails.SetSelection(cf);

        UpdateGameGrid(cf);
    }

    /// <summary>
    /// Updates the Game Grid (main list of games/files) based on the selected directory and filters.
    /// </summary>
    /// <param name="tDir">The directory to display. If null, uses the previously selected directory.</param>
    private void UpdateGameGrid(RvFile? tDir = null)
    {
        if (tDir != null)
        {
            _gameGridSource = tDir;
            _gameBrowser.SetRows(GameListService.CreateRows(tDir));
        }

        if (_gameGridSource == null)
        {
            _gameBrowser.Clear();
            return;
        }

        _updatingGameGrid = true;
        var visibility = new GameVisibilityOptions(
            chkBoxShowComplete.IsChecked == true,
            chkBoxShowPartial.IsChecked == true,
            chkBoxShowEmpty.IsChecked == true,
            chkBoxShowFixes.IsChecked == true,
            chkBoxShowMIA.IsChecked == true,
            chkBoxShowMerged.IsChecked == true);
        GameFilter filter = GameFilter.Parse(txtFilter.Text);
        _gameBrowser.Apply(
            visibility,
            filter,
            _gameSortHeader,
            _gameSortAsc,
            !string.IsNullOrWhiteSpace(txtFilter.Text));
        bool showDescriptionColumn = _gameBrowser.AllRows.Any(row => !string.IsNullOrWhiteSpace(row.Description));

        var gameDescColumn = GameGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Description", StringComparison.Ordinal));
        if (gameDescColumn != null)
        {
            string? persisted = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.col.Description.visible");
            if (persisted == null)
                gameDescColumn.IsVisible = showDescriptionColumn;
        }

        _updatingGameGrid = false;

        if (_gameBrowser.Items.Count == 0)
        {
            _romBrowser.SetGame(null, includeMerged: false);
            RomGrid.ItemsSource = _romBrowser.Items;
            _ = _mediaInspector.SelectGameAsync(null);
        }
    }

    /// <summary>
    /// Handles the selection change event in the Game Grid.
    /// Updates the metadata panel, ROM grid, and artwork based on the selected game.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event arguments.</param>
    private async void GameGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingGameGrid) return;
        if (SelectedGame is { } tGame)
        {
             _viewModel.GameDetails.SetGame(tGame);
             UpdateRomGrid(tGame);
             await _mediaInspector.SelectGameAsync(tGame);
        }
        else
        {
             _viewModel.GameDetails.SetGame(null);
             _romBrowser.SetGame(null, includeMerged: false);
             RomGrid.ItemsSource = _romBrowser.Items;
             await _mediaInspector.SelectGameAsync(null);
        }
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control && e.Key == Key.F)
        {
            txtFilter.Focus();
            txtFilter.SelectionStart = 0;
            txtFilter.SelectionEnd = txtFilter.Text?.Length ?? 0;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(txtFilter.Text))
            {
                txtFilter.Text = "";
                e.Handled = true;
            }

            return;
        }

        if (_working)
            return;

        switch (e.Key)
        {
            case Key.F5:
                UpdateDats();
                e.Handled = true;
                break;
            case Key.F6:
                ScanRoms(EScanLevel.Level2);
                e.Handled = true;
                break;
            case Key.F7:
                FindFixes();
                e.Handled = true;
                break;
            case Key.F8:
                FixFiles();
                e.Handled = true;
                break;
        }
    }

    private void ApplyCompactDensity(bool compact, bool persist)
    {
        _viewModel.IsCompact = compact;
        MenuCompactDensity.IsChecked = compact;
        if (compact)
        {
            if (!Classes.Contains("Compact"))
                Classes.Add("Compact");
        }
        else
        {
            Classes.Remove("Compact");
        }

        GameGrid.RowHeight = compact ? 18 : 22;
        RomGrid.RowHeight = compact ? 18 : 22;
        GameGrid.FontSize = compact ? 10 : 11;
        RomGrid.FontSize = compact ? 10 : 11;

        if (persist)
            AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.CompactDensity", compact ? "1" : "0");
    }

    /// <summary>
    /// Updates the ROM Grid (list of individual ROMs inside a game) for the selected game.
    /// </summary>
    /// <param name="tGame">The selected game file.</param>
    private void UpdateRomGrid(RvFile tGame)
    {
        _romBrowser.SetGame(tGame, chkBoxShowMerged.IsChecked == true);
        _romBrowser.SetSort(_romSortHeader, _romSortAsc);

        var romMergeColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Merge", StringComparison.Ordinal));
        if (romMergeColumn != null) romMergeColumn.IsVisible = _romBrowser.ShowMergeColumn;

        var romAltSizeColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt Size", StringComparison.Ordinal));
        if (romAltSizeColumn != null) romAltSizeColumn.IsVisible = _romBrowser.ShowAlternateColumns;

        var romAltCRC32Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt CRC32", StringComparison.Ordinal));
        if (romAltCRC32Column != null) romAltCRC32Column.IsVisible = _romBrowser.ShowAlternateColumns;

        var romAltSHA1Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt SHA1", StringComparison.Ordinal));
        if (romAltSHA1Column != null) romAltSHA1Column.IsVisible = _romBrowser.ShowAlternateColumns;

        var romAltMD5Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt MD5", StringComparison.Ordinal));
        if (romAltMD5Column != null) romAltMD5Column.IsVisible = _romBrowser.ShowAlternateColumns;

        var romStatusColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Status", StringComparison.Ordinal));
        if (romStatusColumn != null) romStatusColumn.IsVisible = _romBrowser.ShowStatusColumn;

        var romFileModDateColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Modified Date/Time", StringComparison.Ordinal));
        if (romFileModDateColumn != null) romFileModDateColumn.IsVisible = _romBrowser.ShowModifiedColumn;

        RomGrid.ItemsSource = _romBrowser.Items;
    }

    // Context Menu Handlers
    
    /// <summary>
    /// Handles the "Scan" context menu click on the tree.
    /// </summary>
    private void OnTreeScanClick(object? sender, RoutedEventArgs e)
    {
        if (_working) return;
        EScanLevel scanLevel = EScanLevel.Level2;
        if (sender is MenuItem menuItem && menuItem.Tag is string level)
        {
             Enum.TryParse(level, out scanLevel);
        }
        
        var rvTree = RvTreeControl;
        ScanRoms(scanLevel, rvTree?.Selected);
    }

    /// <summary>
    /// Handles the "Set Dir Dat Settings" context menu click.
    /// Opens the directory settings window.
    /// </summary>
    private async void OnSetDirDatSettingsClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = RvTreeControl;
        var selected = rvTree?.Selected;
        if (selected != null)
        {
             var win = new Views.DirectorySettingsWindow();
             win.SetLocation(selected.TreeFullName);
             win.SetDisplayType(true);
             await win.ShowDialog(this);
             
             if (win.ChangesMade)
             {
                 UpdateDats();
             }
        }
    }

    /// <summary>
    /// Handles the "Set Dir Mappings" context menu click.
    /// Opens the directory mappings window for a specific directory.
    /// </summary>
    private async void OnSetDirMappingsClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = RvTreeControl;
        var selected = rvTree?.Selected;
        if (selected != null)
        {
             var win = new Views.DirectoryMappingsWindow();
             win.SetLocation(selected.TreeFullName);
             win.SetDisplayType(true);
             await win.ShowDialog(this);
        }
    }

    /// <summary>
    /// Handles the "Global Dir Mappings" menu click.
    /// Opens the global directory mappings window.
    /// </summary>
    private async Task OpenGlobalDirectoryMappingsAsync()
    {
         if (_working) return;
         var win = new Views.DirectoryMappingsWindow();
         win.SetDisplayType(false);
         await win.ShowDialog(this);
    }

    /// <summary>
    /// Handles the "Open Directory" context menu click.
    /// Opens the selected directory in the OS file explorer.
    /// </summary>
    private void OnOpenDirectoryClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = RvTreeControl;
        var selected = rvTree?.Selected;
        if (selected != null)
        {
             string tDir = selected.FullName;
             if (Directory.Exists(tDir))
             {
                 try 
                 { 
                     Process.Start(new ProcessStartInfo
                     {
                         FileName = tDir,
                         UseShellExecute = true,
                         Verb = "open"
                     }); 
                 } catch { }
             }
        }
    }

    /// <summary>
    /// Handles the "Save Fix DATs" context menu click.
    /// Generates fix DATs for the selected directory.
    /// </summary>
    private async void OnSaveFixDatsClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = RvTreeControl;
        var selected = rvTree?.Selected;
        if (selected != null)
        {
             await Code.Report.CreateFixDat(this, selected, true);
        }
    }

    /// <summary>
    /// Handles the "Save Full DAT" context menu click.
    /// Saves the DAT file to disk.
    /// </summary>
    private async void OnSaveFullDatClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = RvTreeControl;
        var selected = rvTree?.Selected;
        if (selected == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save an Dat File",
            SuggestedFileName = selected.Name,
            DefaultExtension = "dat",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("DAT file") { Patterns = new[] { "*.dat" } }
            }
        });

        if (file != null)
        {
             DatHeader dh = (new ExternalDatConverterTo()).ConvertToExternalDat(selected);
             using (var stream = await file.OpenWriteAsync())
             {
                 DatXMLWriter.WriteDat(stream, dh);
             }
        }
    }

    /// <summary>
    /// Handles the pointer press event on the instance count text block.
    /// Shows the ROM Info window for the selected file.
    /// </summary>
    private void OnInstanceCountPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var textBlock = sender as TextBlock;
        var row = textBlock?.DataContext as RomRowViewModel;
        if (row != null)
        {
            var win = new Views.RomInfoWindow();
            win.SetRom(row.Source);
            win.ShowDialog(this);
        }
    }

    private async void OnRomGridCopyCrcClick(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RomRowViewModel row)
            return;
        await CopyTextToClipboard(row.Crc32);
    }

    private async void OnRomGridCopySha1Click(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RomRowViewModel row)
            return;
        await CopyTextToClipboard(row.Sha1);
    }

    private async void OnRomGridCopyMd5Click(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RomRowViewModel row)
            return;
        await CopyTextToClipboard(row.Md5);
    }

    private void OnRomGridOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RomRowViewModel row)
            return;
        RvFile file = row.Source;

        string candidate = ResolveOsPath(file.FullNameCase);
        if (File.Exists(candidate))
        {
            OpenExplorerSelect(candidate);
            return;
        }

        if (file.Parent != null)
        {
            string parentPath = ResolveOsPath(file.Parent.FullNameCase);
            if (Directory.Exists(parentPath))
            {
                OpenExplorer(parentPath);
            }
        }
    }

    private void OnRomGridShowOccurrencesClick(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RomRowViewModel row)
            return;

        var win = new Views.RomInfoWindow();
        win.SetRom(row.Source);
        win.ShowDialog(this);
    }

    private async Task CopyTextToClipboard(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }

    private static string ResolveOsPath(string path)
    {
        if (path.StartsWith("RomRoot\\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (path.StartsWith("ToSort\\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (path.StartsWith("DatRoot\\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        return path;
    }

    private static void OpenExplorer(string folderPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static void OpenExplorerSelect(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }


    /// <summary>
    /// Handles the "Scan" context menu click on the Game Grid.
    /// </summary>
    private void OnGameGridScanClick(object? sender, RoutedEventArgs e)
    {
        if (_working) return;
        EScanLevel scanLevel = EScanLevel.Level2;
        if (sender is MenuItem menuItem && menuItem.Tag is string level)
        {
             Enum.TryParse(level, out scanLevel);
        }
        
        if (SelectedGame is { } selected)
        {
            ScanRoms(scanLevel, selected);
        }
        else
        {
            ScanRoms(scanLevel);
        }
    }

    /// <summary>
    /// Handles the "Open Directory" context menu click on the Game Grid.
    /// Opens the directory or zip file location in the OS file explorer.
    /// </summary>
    private void OnGameGridOpenDirClick(object? sender, RoutedEventArgs e) 
    { 
        if (SelectedGame is { } thisFile)
        {
            if (thisFile.FileType == FileType.Dir)
            {
                string folderPath = thisFile.FullNameCase;
                if (Directory.Exists(folderPath))
                {
                     try 
                     { 
                         Process.Start(new ProcessStartInfo
                         {
                             FileName = folderPath,
                             UseShellExecute = true,
                             Verb = "open"
                         }); 
                     } catch { }
                }
            }
            else if (thisFile.FileType == FileType.Zip || thisFile.FileType == FileType.SevenZip)
            {
                string zipPath = thisFile.FullNameCase;
                if (File.Exists(zipPath))
                {
                     try 
                     { 
                         Process.Start(new ProcessStartInfo
                         {
                             FileName = zipPath,
                             UseShellExecute = true,
                             Verb = "open"
                         }); 
                     } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Handles the "Open Parent Directory" context menu click on the Game Grid.
    /// </summary>
    private void OnGameGridOpenParentClick(object? sender, RoutedEventArgs e) 
    { 
        if (SelectedGame is { } thisFile)
        {
            var parent = thisFile.Parent;
            if (parent != null && parent.FileType == FileType.Dir)
            {
                string folderPath = parent.FullNameCase;
                if (Directory.Exists(folderPath))
                {
                     try 
                     { 
                         Process.Start(new ProcessStartInfo
                         {
                             FileName = folderPath,
                             UseShellExecute = true,
                             Verb = "open"
                         }); 
                     } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Handles the "Launch Emulator" context menu click.
    /// </summary>
    private void OnLaunchEmulatorClick(object? sender, RoutedEventArgs e) 
    { 
        if (SelectedGame is { } tGame)
        {
            LaunchEmulator(tGame);
        }
    }

    /// <summary>
    /// Handles the "Open Web Page" context menu click.
    /// Opens the No-Intro or Redump page for the game if available.
    /// </summary>
    private void OnOpenWebPageClick(object? sender, RoutedEventArgs e) 
    { 
        if (SelectedGame is { } thisGame)
        {
            if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "No-Intro")
            {
                string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                string datId = thisGame.Dat.GetData(RvDat.DatData.Id);
                if (!string.IsNullOrWhiteSpace(gameId) && !string.IsNullOrWhiteSpace(datId))
                    try { Process.Start(new ProcessStartInfo { FileName = $"https://datomatic.no-intro.org/index.php?page=show_record&s={datId}&n={gameId}", UseShellExecute = true }); } catch { }
            }
            if (thisGame.Game != null && thisGame.Dat?.GetData(RvDat.DatData.HomePage) == "redump.org")
            {
                string gameId = thisGame.Game.GetData(RvGame.GameData.Id);
                if (!string.IsNullOrWhiteSpace(gameId))
                    try { Process.Start(new ProcessStartInfo { FileName = $"http://redump.org/disc/{gameId}/", UseShellExecute = true }); } catch { }
            }
        }
    }

    // Main Menu / Toolbar Handlers
    
    /// <summary>
    /// Handles the "Update New DATs" menu click.
    /// </summary>
    private void UpdateDatsIfIdle()
    {
        if (_working) return;
        UpdateDats();
    }

    /// <summary>
    /// Handles the "Update All DATs" menu click.
    /// Checks for changes in all DATs and updates the database.
    /// </summary>
    private void UpdateAllDatsIfIdle()
    {
        if (_working) return;
        DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
        UpdateDats();
    }

    /// <summary>
    /// Handles the "Scan ROMs" menu click.
    /// </summary>
    private void ScanRomsIfIdle(string? level)
    {
         if (_working) return;
         EScanLevel scanLevel = EScanLevel.Level2;
         if (!string.IsNullOrWhiteSpace(level))
             Enum.TryParse(level, out scanLevel);
        ScanRoms(scanLevel);
    }

    private void FixFilesIfIdle()
    {
        if (_working) return;
        FixFiles();
    }

    private void FindFixesIfIdle()
    {
        if (_working) return;
        FindFixes();
    }
    
    /// <summary>
    /// Handles the "Fix DAT Report" menu click.
    /// </summary>
    private async Task CreateFixDatReportAsync()
    {
        if (_working) return;
        await Code.Report.CreateFixDat(this, DB.DirRoot.Child(0), true);
    }
    
    /// <summary>
    /// Handles the "Generate Full Report" menu click.
    /// </summary>
    private async Task CreateFullReportAsync()
    {
        if (_working) return;
        await Code.Report.GenerateReport(this);
    }
    
    /// <summary>
    /// Handles the "Generate Fix Report" menu click.
    /// </summary>
    private async Task CreateFixReportAsync()
    {
        if (_working) return;
        await Code.Report.GenerateFixReport(this);
    }
    
    /// <summary>
    /// Handles the "Global Dir Dat Settings" menu click.
    /// </summary>
    private async Task OpenGlobalDirectorySettingsAsync()
    {
         if (_working) return;
         var win = new Views.DirectorySettingsWindow();
         win.SetLocation("RomVault");
         win.SetDisplayType(false);
         await win.ShowDialog(this);
         
         if (win.ChangesMade)
         {
             UpdateDats();
         }
    }

    /// <summary>
    /// Handles the "Settings" menu click.
    /// </summary>
    private async Task OpenSettingsAsync()
    {
        if (_working) return;
        var win = new Views.SettingsWindow();
        await win.ShowDialog(this);
    }

    /// <summary>
    /// Handles the "Add To Sort" menu click.
    /// Adds a new directory to be sorted into the database.
    /// </summary>
    private async Task AddToSortAsync()
    {
        if (_working) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select new ToSort Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        string selectedPath = folders[0].Path.LocalPath;

        string relPath = RelativePath.MakeRelative(AppDomain.CurrentDomain.BaseDirectory, selectedPath);

        RvFile ts = new RvFile(FileType.Dir)
        {
            Name = relPath,
            DatStatus = DatStatus.InToSort,
            Tree = new RvTreeRow()
        };
        ts.Tree.SetChecked(RvTreeRow.TreeSelect.Locked, false);

        DB.DirRoot.ChildAdd(ts, DB.DirRoot.ChildCount);

        RepairStatus.ReportStatusReset(DB.DirRoot);
        
        // Refresh Tree
        var rvTree = RvTreeControl;
        if (rvTree != null)
        {
             rvTree.Setup(DB.DirRoot);
             rvTree.SetSelected(ts);
        }
        
        DatSetSelected(ts);
        DB.Write();
    }

    /// <summary>
    /// Shows the TorrentZip help window.
    /// </summary>
    private void OpenTorrentZip()
    {
        var win = new Views.TrrntZipWindow();
        win.Show();
    }

    /// <summary>
    /// Opens the online Wiki.
    /// </summary>
    private void OpenColorKey()
    {
        var win = new Views.KeyWindow();
        win.Show(this);
    }

    private async Task OpenShortcutsAsync()
    {
        string msg =
            "Shortcuts:\r\n\r\n" +
            "- Ctrl+F: focus game filter\r\n" +
            "- Esc: clear game filter\r\n" +
            "- Ctrl+C: copy selected row (games/roms)\r\n\r\n" +
            "Tree:\r\n" +
            "- Up/Down: move selection\r\n" +
            "- Left/Right: collapse/expand\r\n" +
            "- Space: toggle check\r\n" +
            "- Tree search box: Enter jumps to next match\r\n\r\n" +
            "Artwork:\r\n" +
            "- Ctrl+MouseWheel: zoom\r\n" +
            "- Double-click: reset zoom\r\n";
        await Views.MessageBoxWindow.ShowInfo(this, msg, "Shortcuts");
    }

    private async Task OpenAboutAsync()
    {
        var win = new Views.HelpAboutWindow();
        await win.ShowDialog(this);
    }

    private async void OnTreePresetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.Tag is not string tag) return;
        if (!int.TryParse(tag, out int index)) return;
        bool right = e.GetCurrentPoint(control).Properties.IsRightButtonPressed;
        if (!right)
        {
            TreeDefault(set: false, index);
            return;
        }

        var menu = new ContextMenu();

        var miSave = new MenuItem { Header = "Save Current" };
        miSave.Click += (_, _) =>
        {
            TreeDefault(set: true, index);
            _viewModel.Activity = $"Saved preset {index}";
        };

        var miLoad = new MenuItem { Header = "Load" };
        miLoad.Click += (_, _) => TreeDefault(set: false, index);

        var miRename = new MenuItem { Header = "Rename…" };
        miRename.Click += async (_, _) =>
        {
            string current = AppSettings.ReadSetting($"{UiStatePrefix}.TreePreset.{index}.Name") ?? "";
            string? name = await PromptAsync($"Preset {index}", "Name", current);
            if (name == null) return;
            AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.TreePreset.{index}.Name", name.Trim());
            UpdateTreePresetTooltips();
        };

        var miClear = new MenuItem { Header = "Clear" };
        miClear.Click += (_, _) =>
        {
            try
            {
                string fn = $"treeDefault{index}.xml";
                if (System.IO.File.Exists(fn))
                    System.IO.File.Delete(fn);
            }
            catch { }

            AppSettings.AddUpdateAppSettings($"{UiStatePrefix}.TreePreset.{index}.Name", "");
            UpdateTreePresetTooltips();
            _viewModel.Activity = $"Cleared preset {index}";
        };

        menu.Items.Add(miSave);
        menu.Items.Add(miLoad);
        menu.Items.Add(new Separator());
        menu.Items.Add(miRename);
        menu.Items.Add(miClear);

        await Task.Yield();
        menu.Open(control);
    }

    private void TreeDefault(bool set, int index)
    {
        var dtss = new DatTreeStatusStore();
        if (set)
        {
            dtss.write(index);
            return;
        }
        dtss.read(index);
        var rvTree = RvTreeControl;
        rvTree?.Setup(DB.DirRoot);
    }

    private void ApplyTreeCheckAll(bool selected)
    {
        if (DB.DirRoot == null)
            return;

        ApplyTreeCheckAllInternal(DB.DirRoot, selected);

        var rvTree = RvTreeControl;
        rvTree?.Setup(DB.DirRoot);
    }

    private static void ApplyTreeCheckAllInternal(RvFile node, bool selected)
    {
        if (node.Tree != null && node.Tree.Checked != RvTreeRow.TreeSelect.Locked)
            node.Tree.SetChecked(selected ? RvTreeRow.TreeSelect.Selected : RvTreeRow.TreeSelect.UnSelected, false);

        if (!node.IsDirectory)
            return;

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            if (child.IsDirectory)
                ApplyTreeCheckAllInternal(child, selected);
        }
    }

    private void UpdateTreePresetTooltips()
    {
        for (int i = 1; i <= 4; i++)
        {
            var btn = i switch
            {
                1 => btnTreePreset1,
                2 => btnTreePreset2,
                3 => btnTreePreset3,
                4 => btnTreePreset4,
                _ => null
            };

            if (btn == null) continue;

            string name = AppSettings.ReadSetting($"{UiStatePrefix}.TreePreset.{i}.Name") ?? "";
            string tip = string.IsNullOrWhiteSpace(name)
                ? $"Preset {i} (right-click for options)"
                : $"Preset {i}: {name} (right-click for options)";
            ToolTip.SetTip(btn, tip);
        }
    }

    private async Task<string?> PromptAsync(string title, string label, string initialValue)
    {
        var tcs = new TaskCompletionSource<string?>();

        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var tbLabel = new TextBlock
        {
            Text = label,
            Margin = new global::Avalonia.Thickness(0, 0, 0, 6),
            FontSize = 12
        };

        var input = new TextBox
        {
            Text = initialValue,
            FontSize = 12
        };

        var btnCancel = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
            IsCancel = true
        };

        var btnOk = new Button
        {
            Content = "OK",
            MinWidth = 92,
            IsDefault = true
        };

        btnOk.Click += (_, _) =>
        {
            tcs.TrySetResult(input.Text);
            win.Close();
        };

        btnCancel.Click += (_, _) =>
        {
            tcs.TrySetResult(null);
            win.Close();
        };

        win.Closed += (_, _) =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(null);
        };

        var cardBorder = new Border
        {
            Padding = new global::Avalonia.Thickness(12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    tbLabel,
                    input
                }
            }
        };
        cardBorder.Classes.Add("Card");

        win.Content = new DockPanel
        {
            Margin = new global::Avalonia.Thickness(16),
            Children =
            {
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Padding = new global::Avalonia.Thickness(0, 12, 0, 0),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            btnCancel,
                            btnOk
                        }
                    }
                },
                cardBorder
            }
        };

        Grid.SetColumn(btnCancel, 1);
        Grid.SetColumn(btnOk, 2);

        await win.ShowDialog(this);
        return await tcs.Task;
    }
    
    /// <summary>
    /// Launches the configured emulator for the selected game.
    /// </summary>
    /// <param name="tGame">The game file to launch.</param>
    private void LaunchEmulator(RvFile tGame)
    {
        EmulatorInfo? ei = FindEmulatorInfo(tGame);
        if (ei == null)
            return;

        string commandLineOptions = ei.CommandLine;
        string dirname = tGame.Parent.FullName;
        if (dirname.StartsWith("RomRoot\\"))
             dirname = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dirname);

        commandLineOptions = commandLineOptions.Replace("{gamename}", Path.GetFileNameWithoutExtension(tGame.Name));
        commandLineOptions = commandLineOptions.Replace("{gamefilename}", tGame.Name);
        commandLineOptions = commandLineOptions.Replace("{gamedirectory}", dirname);

        string? workingDir = ei.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDir))
            workingDir = Path.GetDirectoryName(ei.ExeName);

        if (workingDir == null) return;

        using (Process exeProcess = new Process())
        {
            exeProcess.StartInfo.WorkingDirectory = workingDir;
            exeProcess.StartInfo.FileName = ei.ExeName;
            exeProcess.StartInfo.Arguments = commandLineOptions;
            exeProcess.StartInfo.UseShellExecute = false;
            exeProcess.StartInfo.CreateNoWindow = true;
            exeProcess.Start();
        }
    }

    /// <summary>
    /// Finds the configured emulator information for a given game path.
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <returns>The <see cref="EmulatorInfo"/> if found, otherwise null.</returns>
    private EmulatorInfo? FindEmulatorInfo(RvFile tGame)
    {
        string path = tGame.Parent.DatTreeFullName;
        if (Settings.rvSettings?.EInfo == null)
            return null;
        if (path == "Error")
            return null;
        if (path.Length <= 8)
            return null;

        foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
        {
            if (!string.Equals(path.Substring(8), ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(ei.CommandLine))
                continue;

            if (!File.Exists(ei.ExeName))
                continue;
            return ei;
        }
        return null;
    }

}
