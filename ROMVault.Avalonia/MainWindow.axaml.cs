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
    private bool _updatingGameGrid;
    private bool _working = false;
    private GridLength _lastArtworkWidth = new GridLength(300);
    private DispatcherTimer? _filterDebounceTimer;
    private readonly Dictionary<global::Avalonia.Controls.Image, double> _imageZoom = new();
    private readonly Dictionary<Control, RvFile> _mediaContainers = new();
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// Sets up the directory tree, event handlers, and initial status aggregation.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        if (lblStatusLeft != null) lblStatusLeft.Text = "";
        if (lblStatusRight != null) lblStatusRight.Text = "";
        
        // Initialize Tree
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        var treeScrollViewer = this.FindControl<ScrollViewer>("TreeScrollViewer");
        var treeStatsHeader = this.FindControl<Grid>("TreeStatsHeader");
        var chkTreeStats = this.FindControl<CheckBox>("chkTreeStats");
        var lblTreeStatHave = this.FindControl<TextBlock>("lblTreeStatHave");
        var lblTreeStatMissing = this.FindControl<TextBlock>("lblTreeStatMissing");
        var lblTreeStatMia = this.FindControl<TextBlock>("lblTreeStatMia");
        var lblTreeStatFixes = this.FindControl<TextBlock>("lblTreeStatFixes");
        var lblTreeStatUnknown = this.FindControl<TextBlock>("lblTreeStatUnknown");

        var btnTreeAll = this.FindControl<Button>("btnTreeAll");
        var btnTreeNil = this.FindControl<Button>("btnTreeNil");
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
                var contextMenu = this.FindControl<ContextMenu>("TreeContextMenu");
                contextMenu?.Open(rvTree);
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
                rvTree.ViewportOffsetX = treeScrollViewer.Offset.X;
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
        SetupMediaContextMenus();
        LoadUiState();
        UpdateTreePresetTooltips();
        Closing += (_, _) => SaveUiState();
    }

    /// <summary>
    /// Attaches context menus to artwork images and info text panels.
    /// The actions operate on the "container" (usually the zip/dir holding the artwork/text),
    /// not the individual entry inside it.
    /// </summary>
    private void SetupMediaContextMenus()
    {
        AttachImageMenu(picLogo);
        AttachImageMenu(picArtwork);
        AttachImageMenu(picMedium1);
        AttachImageMenu(picMedium2);
        AttachImageMenu(picScreenTitle);
        AttachImageMenu(picScreenShot);

        AttachTextMenu(txtInfo);
        AttachTextMenu(txtInfo2);
    }

    /// <summary>
    /// Adds a context menu to an artwork image control.
    /// </summary>
    private void AttachImageMenu(global::Avalonia.Controls.Image? image)
    {
        if (image == null) return;

        var open = new MenuItem { Header = "Open Source" };
        open.Click += (_, _) => OpenMediaContainer(image);

        var copy = new MenuItem { Header = "Copy Source Path" };
        copy.Click += async (_, _) => await CopyMediaContainerPath(image);

        var show = new MenuItem { Header = "Show in Folder" };
        show.Click += (_, _) => ShowMediaContainerInFolder(image);

        var reset = new MenuItem { Header = "Reset Zoom" };
        reset.Click += (_, _) => ResetArtworkZoom(image);

        image.ContextMenu = new ContextMenu
        {
            Items =
            {
                open,
                copy,
                show,
                new Separator(),
                reset
            }
        };
    }

    /// <summary>
    /// Adds a context menu to an info text box.
    /// </summary>
    private void AttachTextMenu(TextBox? textBox)
    {
        if (textBox == null) return;

        var copyAll = new MenuItem { Header = "Copy All" };
        copyAll.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard == null) return;
            await topLevel.Clipboard.SetTextAsync(textBox.Text ?? "");
        };

        var open = new MenuItem { Header = "Open Source" };
        open.Click += (_, _) => OpenMediaContainer(textBox);

        var copy = new MenuItem { Header = "Copy Source Path" };
        copy.Click += async (_, _) => await CopyMediaContainerPath(textBox);

        var show = new MenuItem { Header = "Show in Folder" };
        show.Click += (_, _) => ShowMediaContainerInFolder(textBox);

        textBox.ContextMenu = new ContextMenu
        {
            Items =
            {
                copyAll,
                new Separator(),
                open,
                copy,
                show
            }
        };
    }

    /// <summary>
    /// Opens the resolved container path (file/folder) using the OS shell.
    /// </summary>
    private void OpenMediaContainer(Control control)
    {
        if (!_mediaContainers.TryGetValue(control, out var container))
            return;

        string path = container.FullName;
        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch { }
    }

    /// <summary>
    /// Copies the resolved container path (file/folder) to the clipboard.
    /// </summary>
    private async Task CopyMediaContainerPath(Control control)
    {
        if (!_mediaContainers.TryGetValue(control, out var container))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(container.FullName);
    }

    /// <summary>
    /// Opens Explorer on the container location. If it's a file, selects it.
    /// </summary>
    private void ShowMediaContainerInFolder(Control control)
    {
        if (!_mediaContainers.TryGetValue(control, out var container))
            return;

        string raw = container.FullName;
        string path = ResolveOsPath(raw);

        if (File.Exists(path))
        {
            OpenExplorerSelect(path);
            return;
        }

        if (Directory.Exists(path))
        {
            OpenExplorer(path);
        }
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
        var mainSplitGrid = this.FindControl<Grid>("MainSplitGrid");
        if (mainSplitGrid != null && mainSplitGrid.ColumnDefinitions.Count >= 3)
        {
            string? leftWidth = AppSettings.ReadSetting($"{UiStatePrefix}.MainSplit.LeftWidth");
            if (double.TryParse(leftWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w >= 300)
            {
                mainSplitGrid.ColumnDefinitions[0].Width = new GridLength(w);
            }
        }

        string? artworkWidth = AppSettings.ReadSetting($"{UiStatePrefix}.Artwork.Width");
        if (double.TryParse(artworkWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var aw) && aw >= 120)
        {
            _lastArtworkWidth = new GridLength(aw);
        }

        LoadDataGridState(GameGrid, $"{UiStatePrefix}.GameGrid");
        LoadDataGridState(RomGrid, $"{UiStatePrefix}.RomGrid");

        _gameSortHeader = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.SortHeader");
        _gameSortAsc = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.SortAsc") != "0";
        _romSortHeader = AppSettings.ReadSetting($"{UiStatePrefix}.RomGrid.SortHeader");
        _romSortAsc = AppSettings.ReadSetting($"{UiStatePrefix}.RomGrid.SortAsc") != "0";

    }

    private void SaveUiState()
    {
        var mainSplitGrid = this.FindControl<Grid>("MainSplitGrid");
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

    private void ResetArtworkZoom(global::Avalonia.Controls.Image? image)
    {
        if (image == null) return;
        _imageZoom[image] = 1.0;
        image.RenderTransformOrigin = new global::Avalonia.RelativePoint(0.5, 0.5, global::Avalonia.RelativeUnit.Relative);
        image.RenderTransform = new global::Avalonia.Media.ScaleTransform(1.0, 1.0);
    }

    private void SetArtworkZoom(global::Avalonia.Controls.Image image, double zoom)
    {
        zoom = Math.Clamp(zoom, 0.25, 6.0);
        _imageZoom[image] = zoom;
        image.RenderTransformOrigin = new global::Avalonia.RelativePoint(0.5, 0.5, global::Avalonia.RelativeUnit.Relative);
        image.RenderTransform = new global::Avalonia.Media.ScaleTransform(zoom, zoom);
    }

    private double GetArtworkZoom(global::Avalonia.Controls.Image image)
    {
        if (_imageZoom.TryGetValue(image, out var z))
            return z;
        return 1.0;
    }

    private void ResetAllArtworkZoom()
    {
        ResetArtworkZoom(picLogo);
        ResetArtworkZoom(picArtwork);
        ResetArtworkZoom(picMedium1);
        ResetArtworkZoom(picMedium2);
        ResetArtworkZoom(picScreenTitle);
        ResetArtworkZoom(picScreenShot);
    }

    private void ResetInfoTextBoxes()
    {
        ConfigureWrappedText(txtInfo);
        ConfigureWrappedText(txtInfo2);
        if (txtInfo != null) txtInfo.Text = "";
        if (txtInfo2 != null) txtInfo2.Text = "";
    }

    private static void ConfigureWrappedText(TextBox? textBox)
    {
        if (textBox == null) return;
        textBox.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        textBox.FontFamily = global::Avalonia.Media.FontFamily.Default;
    }

    private static void ConfigureMonospaceText(TextBox? textBox)
    {
        if (textBox == null) return;
        textBox.TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap;
        textBox.FontFamily = new global::Avalonia.Media.FontFamily("Consolas, Courier New, monospace");
    }

    /// <summary>
    /// Updates the small counters in the header area (visible/total and sort state).
    /// </summary>
    private void UpdateGameCountLabel(int visible, int total)
    {
        if (lblGameCount != null)
            lblGameCount.Text = $"{visible}/{total}";

        if (lblSortInfo != null)
        {
            if (string.IsNullOrWhiteSpace(_gameSortHeader))
                lblSortInfo.Text = "";
            else
                lblSortInfo.Text = $"Sort: {_gameSortHeader} {(_gameSortAsc ? "↑" : "↓")}";
        }
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
            if (RomGrid.SelectedItem is not RvFile file)
                return;

            string text = $"{file.UiDisplayName}\t{file.Size}\t{file.CRC32}\t{file.SHA1Hex}\t{file.MD5Hex}";
            await CopyTextToClipboard(text);
            if (lblStatusRight != null) lblStatusRight.Text = "Copied";
            e.Handled = true;
            return;
        }

        if (grid == GameGrid)
        {
            if (GameGrid.SelectedItem is not RvFile game)
                return;

            string desc = game.Game?.GetData(RvGame.GameData.Description) ?? "";
            if (desc == "¤") desc = "";
            string text = string.IsNullOrWhiteSpace(desc) ? (game.Name ?? "") : $"{game.Name}\t{desc}";
            await CopyTextToClipboard(text);
            if (lblStatusRight != null) lblStatusRight.Text = "Copied";
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
            if (lblStatusRight != null) lblStatusRight.Text = "Copied";
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

        if (GameGrid.SelectedItem is RvFile game)
            UpdateRomGrid(game);
    }

    private void ApplyGameSort(List<RvFile> list)
    {
        if (string.IsNullOrWhiteSpace(_gameSortHeader) || list.Count <= 1)
            return;

        bool asc = _gameSortAsc;
        string header = _gameSortHeader;

        Comparison<RvFile> cmp = header switch
        {
            "Game (Directory / Zip)" => (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase),
            "Description" => (a, b) =>
            {
                string da = a.Game?.GetData(RvGame.GameData.Description) ?? "";
                string db = b.Game?.GetData(RvGame.GameData.Description) ?? "";
                if (da == "¤") da = "";
                if (db == "¤") db = "";
                return string.Compare(da, db, StringComparison.CurrentCultureIgnoreCase);
            },
            "Modified" => (a, b) => (a.FileModTimeStamp).CompareTo(b.FileModTimeStamp),
            "ROM Status" => (a, b) =>
            {
                int wa = a.DirStatus.CountMissing() * 100000 + a.DirStatus.CountCanBeFixed() * 1000 + a.DirStatus.CountUnknown();
                int wb = b.DirStatus.CountMissing() * 100000 + b.DirStatus.CountCanBeFixed() * 1000 + b.DirStatus.CountUnknown();
                return wa.CompareTo(wb);
            },
            "Extras" => (a, b) => string.Compare(GetExtrasBadge(a), GetExtrasBadge(b), StringComparison.OrdinalIgnoreCase),
            _ => (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase)
        };

        list.Sort((a, b) => asc ? cmp(a, b) : cmp(b, a));
    }

    private static string GetExtrasBadge(RvFile dir)
    {
        bool hasText = false;
        bool hasArt = false;
        int limit = Math.Min(dir.ChildCount, 400);
        for (int i = 0; i < limit; i++)
        {
            var child = dir.Child(i);
            if (child.GotStatus != GotStatus.Got)
                continue;

            string name = child.Name ?? "";
            if (!hasText)
            {
                if (name.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".diz", StringComparison.OrdinalIgnoreCase))
                    hasText = true;
            }

            if (!hasArt)
            {
                if (name.StartsWith("Artwork/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Artwork\\", StringComparison.OrdinalIgnoreCase))
                    hasArt = true;
            }

            if (hasText && hasArt)
                break;
        }

        if (hasArt) return "ART";
        if (hasText) return "TXT";
        return "";
    }

    private void ApplyRomSort(List<RvFile> list)
    {
        if (string.IsNullOrWhiteSpace(_romSortHeader) || list.Count <= 1)
            return;

        bool asc = _romSortAsc;
        string header = _romSortHeader;

        Comparison<RvFile> cmp = header switch
        {
            "ROM (File)" => (a, b) => string.Compare(a.UiDisplayName, b.UiDisplayName, StringComparison.CurrentCultureIgnoreCase),
            "Size" => (a, b) => Nullable.Compare(a.Size, b.Size),
            "CRC32" => (a, b) => string.Compare(a.CRC32, b.CRC32, StringComparison.OrdinalIgnoreCase),
            "SHA1" => (a, b) => string.Compare(a.SHA1Hex, b.SHA1Hex, StringComparison.OrdinalIgnoreCase),
            "MD5" => (a, b) => string.Compare(a.MD5Hex, b.MD5Hex, StringComparison.OrdinalIgnoreCase),
            "Zip Index" => (a, b) => a.ZipIndex.CompareTo(b.ZipIndex),
            "Instance Count" => (a, b) => a.InstanceCount.CompareTo(b.InstanceCount),
            _ => (a, b) => string.Compare(a.UiDisplayName, b.UiDisplayName, StringComparison.CurrentCultureIgnoreCase)
        };

        list.Sort((a, b) => asc ? cmp(a, b) : cmp(b, a));
    }

    private void EnsureTreeNodeVisible(ROMVault.Avalonia.Views.RvTree rvTree, RvFile node)
    {
        var sv = this.FindControl<ScrollViewer>("TreeScrollViewer");
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
        if (GameGrid.SelectedItem is RvFile tGame && tGame.FileType == FileType.Dir)
        {
            var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
            lblStatusLeft.Text = cf.FullName;
        }

        UpdateEffectiveDatRuleDisplay(cf);

        lblDITName.Text = cf.Name ?? "";

        RvDat? tDat = null;
        if (cf.Dat != null)
        {
            tDat = cf.Dat;
        }
        else if (cf.DirDatCount == 1)
        {
            // Many tree nodes represent a directory that *contains* a DAT rather than being the DAT itself.
            // WinForms treated "single DAT under this node" as the DAT to display.
            tDat = cf.DirDat(0);
        }

        if (tDat != null)
        {
            string datName = NormalizeDatField(tDat.GetData(RvDat.DatData.DatName));
            if (!string.IsNullOrWhiteSpace(datName) && !string.Equals(lblDITName.Text, datName, StringComparison.Ordinal))
            {
                lblDITName.Text = $"{lblDITName.Text}:  {datName}";
            }

            string datId = NormalizeDatField(tDat.GetData(RvDat.DatData.Id));
            if (!string.IsNullOrWhiteSpace(datId))
                lblDITName.Text += $" (ID:{datId})";

            lblDITDescription.Text = NormalizeDatField(tDat.GetData(RvDat.DatData.Description));
            lblDITCategory.Text = NormalizeDatField(tDat.GetData(RvDat.DatData.Category));
            lblDITVersion.Text = NormalizeDatField(tDat.GetData(RvDat.DatData.Version));
            lblDITAuthor.Text = NormalizeDatField(tDat.GetData(RvDat.DatData.Author));
            lblDITDate.Text = NormalizeDatField(tDat.GetData(RvDat.DatData.Date));

            string header = NormalizeDatField(tDat.GetData(RvDat.DatData.Header));
            if (!string.IsNullOrWhiteSpace(header))
                lblDITName.Text += $" ({header})";
        }
        else
        {
            lblDITDescription.Text = "";
            lblDITCategory.Text = "";
            lblDITVersion.Text = "";
            lblDITAuthor.Text = "";
            lblDITDate.Text = "";
        }

        // Populate Stats
        lblDITRomsGot.Text = cf.DirStatus.CountCorrect().ToString();
        lblDITRomsMissing.Text = cf.DirStatus.CountMissing().ToString();
        lblDITRomsFixable.Text = cf.DirStatus.CountCanBeFixed().ToString();
        lblDITRomsUnknown.Text = cf.DirStatus.CountUnknown().ToString();

        UpdateGameGrid(cf);
    }

    /// <summary>
    /// Updates the header-area display that shows the effective (inherited) DAT rule for the currently selected subtree.
    /// Rules are defined on specific tree paths and cascade downwards, so we resolve the closest matching ancestor rule.
    /// </summary>
    private void UpdateEffectiveDatRuleDisplay(RvFile selected)
    {
        var lbl = this.FindControl<TextBlock>("lblEffectiveDatRule");
        var row = this.FindControl<Grid>("EffectiveRuleRow");
        if (lbl == null || row == null)
            return;

        var resolved = ResolveEffectiveDatRule(selected.TreeFullName);
        if (resolved == null)
        {
            lbl.Text = "";
            row.IsVisible = false;
            return;
        }

        string inherit = string.Equals(resolved.DirKey, selected.TreeFullName, StringComparison.Ordinal) ? "" : $" (from {resolved.DirKey})";
        lbl.Text = $"{FormatDatRuleSummary(resolved)}{inherit}";
        row.IsVisible = true;
    }

    /// <summary>
    /// Finds the most specific (longest DirKey) DAT rule whose path matches the selected tree path.
    /// </summary>
    private static DatRule? ResolveEffectiveDatRule(string treePath)
    {
        if (string.IsNullOrWhiteSpace(treePath))
            return null;

        DatRule? best = null;
        foreach (DatRule rule in Settings.rvSettings.DatRules)
        {
            if (string.IsNullOrWhiteSpace(rule.DirKey))
                continue;

            if (treePath.Equals(rule.DirKey, StringComparison.Ordinal) ||
                treePath.StartsWith(rule.DirKey + "\\", StringComparison.Ordinal))
            {
                if (best == null || rule.DirKey.Length > best.DirKey.Length)
                    best = rule;
            }
        }

        return best;
    }

    /// <summary>
    /// Formats the key rule fields into a compact one-line summary suitable for a header area.
    /// </summary>
    private static string FormatDatRuleSummary(DatRule rule)
    {
        string archive = rule.Compression.ToString();
        string compression = rule.Compression == FileType.Zip ? rule.CompressionSub.ToString() : "";
        string merge = rule.Merge.ToString();
        string header = rule.HeaderType.ToString();

        string summary = $"Archive {archive}";
        if (!string.IsNullOrWhiteSpace(compression))
            summary += $", Compression {compression}";
        summary += $", Merge {merge}, Header {header}";
        if (rule.SingleArchive)
            summary += ", Single";
        return summary;
    }

    /// <summary>
    /// Normalizes DAT fields where RomVault uses a sentinel value ("¤") to mean "empty".
    /// </summary>
    private static string NormalizeDatField(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "¤")
            return "";
        return value;
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
        }

        if (_gameGridSource == null) return;

        _updatingGameGrid = true;
        
        var gameList = new List<RvFile>();
        var filter = ParseGameFilter(txtFilter.Text);
        bool showDescriptionColumn = false;
        int totalDirCount = 0;

        for (int j = 0; j < _gameGridSource.ChildCount; j++)
        {
            RvFile tChildDir = _gameGridSource.Child(j);
            if (!tChildDir.IsDirectory) continue;
            totalDirCount++;

            string descValue = "";
            if (tChildDir.Game != null)
            {
                descValue = tChildDir.Game.GetData(RvGame.GameData.Description) ?? "";
                if (descValue == "¤") descValue = "";
            }

            if (!showDescriptionColumn && tChildDir.Game != null)
            {
                if (!string.IsNullOrWhiteSpace(descValue))
                {
                    showDescriptionColumn = true;
                }
            }

            ReportStatus tDirStat = tChildDir.DirStatus;

            bool gCorrect = tDirStat.HasCorrect();
            bool gMissing = tDirStat.HasMissing(false);
            bool gUnknown = tDirStat.HasUnknown();
            bool gInToSort = tDirStat.HasInToSort();
            bool gFixes = tDirStat.HasFixesNeeded();
            bool gMIA = tDirStat.HasMIA();
            bool gAllMerged = tDirStat.HasAllMerged();

            bool show = (chkBoxShowComplete.IsChecked == true && gCorrect && !gMissing && !gFixes);
            show = show || (chkBoxShowPartial.IsChecked == true && gMissing && gCorrect);
            show = show || (chkBoxShowEmpty.IsChecked == true && gMissing && !gCorrect);
            show = show || (chkBoxShowFixes.IsChecked == true && gFixes);
            show = show || (chkBoxShowMIA.IsChecked == true && gMIA);
            show = show || (chkBoxShowMerged.IsChecked == true && gAllMerged);
            show = show || gUnknown;
            show = show || gInToSort;
            show = show || tChildDir.GotStatus == GotStatus.Corrupt;
            show = show || !(gCorrect || gMissing || gUnknown || gInToSort || gFixes || gMIA || gAllMerged);

            if (!show)
                continue;

            if (!MatchesGameFilter(filter, tChildDir.Name ?? "", descValue, gCorrect, gMissing, gFixes, gMIA, gAllMerged, gUnknown, gInToSort, tChildDir.GotStatus))
                continue;

            if (show)
            {
                gameList.Add(tChildDir);
            }
        }

        ApplyGameSort(gameList);

        var gameDescColumn = GameGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Description", StringComparison.Ordinal));
        if (gameDescColumn != null)
        {
            string? persisted = AppSettings.ReadSetting($"{UiStatePrefix}.GameGrid.col.Description.visible");
            if (persisted == null)
                gameDescColumn.IsVisible = showDescriptionColumn;
        }

        GameGrid.ItemsSource = gameList;
        UpdateGameCountLabel(gameList.Count, totalDirCount);
        _updatingGameGrid = false;
        
        if (gameList.Count > 0)
        {
            // GameGrid.SelectedIndex = 0; // Optional: Select first item
        }
        else
        {
            RomGrid.ItemsSource = null;
        }
    }

    private sealed class GameFilter
    {
        public string? FreeText { get; set; }
        public string? DescText { get; set; }
        public HashSet<string> Statuses { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static GameFilter ParseGameFilter(string? text)
    {
        var f = new GameFilter();
        if (string.IsNullOrWhiteSpace(text))
            return f;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var free = new List<string>();

        foreach (var p in parts)
        {
            if (p.StartsWith("desc:", StringComparison.OrdinalIgnoreCase))
            {
                var v = p.Substring(5);
                if (!string.IsNullOrWhiteSpace(v))
                    f.DescText = v;
                continue;
            }

            if (p.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
            {
                var v = p.Substring(7);
                if (!string.IsNullOrWhiteSpace(v))
                {
                    foreach (var s in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        f.Statuses.Add(s);
                }
                continue;
            }

            free.Add(p);
        }

        if (free.Count > 0)
            f.FreeText = string.Join(' ', free);

        return f;
    }

    private static bool MatchesGameFilter(
        GameFilter filter,
        string name,
        string description,
        bool gCorrect,
        bool gMissing,
        bool gFixes,
        bool gMIA,
        bool gAllMerged,
        bool gUnknown,
        bool gInToSort,
        GotStatus gotStatus)
    {
        if (!string.IsNullOrWhiteSpace(filter.FreeText))
        {
            string t = filter.FreeText.ToLowerInvariant();
            if (!(name?.ToLowerInvariant().Contains(t) == true || description?.ToLowerInvariant().Contains(t) == true))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.DescText))
        {
            string t = filter.DescText.ToLowerInvariant();
            if (!(description?.ToLowerInvariant().Contains(t) == true))
                return false;
        }

        if (filter.Statuses.Count > 0)
        {
            bool any = false;
            foreach (var s in filter.Statuses)
            {
                if (IsStatusMatch(s, gCorrect, gMissing, gFixes, gMIA, gAllMerged, gUnknown, gInToSort, gotStatus))
                {
                    any = true;
                    break;
                }
            }
            if (!any)
                return false;
        }

        return true;
    }

    private static bool IsStatusMatch(
        string status,
        bool gCorrect,
        bool gMissing,
        bool gFixes,
        bool gMIA,
        bool gAllMerged,
        bool gUnknown,
        bool gInToSort,
        GotStatus gotStatus)
    {
        status = status.Trim();
        if (status.Length == 0)
            return false;

        return status.Equals("complete", StringComparison.OrdinalIgnoreCase) && gCorrect && !gMissing && !gFixes
            || status.Equals("partial", StringComparison.OrdinalIgnoreCase) && gMissing && gCorrect
            || status.Equals("empty", StringComparison.OrdinalIgnoreCase) && gMissing && !gCorrect
            || status.Equals("missing", StringComparison.OrdinalIgnoreCase) && gMissing
            || status.Equals("fixes", StringComparison.OrdinalIgnoreCase) && gFixes
            || status.Equals("mia", StringComparison.OrdinalIgnoreCase) && gMIA
            || status.Equals("merged", StringComparison.OrdinalIgnoreCase) && gAllMerged
            || status.Equals("unknown", StringComparison.OrdinalIgnoreCase) && gUnknown
            || status.Equals("intosort", StringComparison.OrdinalIgnoreCase) && gInToSort
            || status.Equals("corrupt", StringComparison.OrdinalIgnoreCase) && gotStatus == GotStatus.Corrupt;
    }

    /// <summary>
    /// Handles the selection change event in the Game Grid.
    /// Updates the metadata panel, ROM grid, and artwork based on the selected game.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event arguments.</param>
    private void GameGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingGameGrid) return;
        if (GameGrid.SelectedItem is RvFile tGame)
        {
             UpdateGameMetaData(tGame);
             UpdateRomGrid(tGame);
             UpdateArtworkVisibility(tGame);
        }
        else
        {
             UpdateGameMetaData(null);
             RomGrid.ItemsSource = null;
             HideAllArtworkTabs();
        }
    }

    /// <summary>
    /// Updates the Game Metadata panel (description, manufacturer, year, etc.) for the selected game.
    /// </summary>
    /// <param name="tGame">The selected game file.</param>
    private void UpdateGameMetaData(RvFile? tGame)
    {
        var lblGameName = this.FindControl<TextBlock>("lblGameName");
        
        var lblGameDescriptionLabel = this.FindControl<TextBlock>("lblGameDescriptionLabel");
        var lblGameDescription = this.FindControl<TextBlock>("lblGameDescription");
        
        var lblGameManufacturerLabel = this.FindControl<TextBlock>("lblGameManufacturerLabel");
        var lblGameManufacturer = this.FindControl<TextBlock>("lblGameManufacturer");
        
        var lblGameCloneOfLabel = this.FindControl<TextBlock>("lblGameCloneOfLabel");
        var lblGameCloneOf = this.FindControl<TextBlock>("lblGameCloneOf");
        
        var lblGameRomOfLabel = this.FindControl<TextBlock>("lblGameRomOfLabel");
        var lblGameRomOf = this.FindControl<TextBlock>("lblGameRomOf");
        
        var lblGameYearLabel = this.FindControl<TextBlock>("lblGameYearLabel");
        var lblGameYear = this.FindControl<TextBlock>("lblGameYear");
        
        var lblGameCategoryLabel = this.FindControl<TextBlock>("lblGameCategoryLabel");
        var lblGameCategory = this.FindControl<TextBlock>("lblGameCategory");

        void SetVisible(bool visible, params Control?[] controls)
        {
            foreach (var c in controls)
            {
                if (c != null) c.IsVisible = visible;
            }
        }

        if (tGame == null)
        {
            if (lblGameName != null) lblGameName.Text = "";
            SetVisible(false, lblGameDescriptionLabel, lblGameDescription, 
                              lblGameManufacturerLabel, lblGameManufacturer,
                              lblGameCloneOfLabel, lblGameCloneOf,
                              lblGameRomOfLabel, lblGameRomOf,
                              lblGameYearLabel, lblGameYear,
                              lblGameCategoryLabel, lblGameCategory);
            return;
        }

        if (lblGameName != null)
        {
            string gameId = tGame.Game?.GetData(RvGame.GameData.Id) ?? "";
            lblGameName.Text = tGame.Name + (!string.IsNullOrWhiteSpace(gameId) ? $" (ID:{gameId})" : "");
        }

        if (tGame.Game != null)
        {
            // Note: Treating EmuArc same as Standard for basic fields to match WinForms behavior
            bool isEmuArc = tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes";

            // Description
            string desc = tGame.Game.GetData(RvGame.GameData.Description);
            if (desc == "¤") desc = Path.GetFileNameWithoutExtension(tGame.Name);
            if (lblGameDescription != null) lblGameDescription.Text = desc;
            SetVisible(true, lblGameDescriptionLabel, lblGameDescription);

            // Manufacturer
            string manu = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.Manufacturer));
            if (lblGameManufacturer != null) lblGameManufacturer.Text = manu;
            SetVisible(!isEmuArc || !string.IsNullOrWhiteSpace(manu), lblGameManufacturerLabel, lblGameManufacturer);

            // CloneOf
            string clone = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.CloneOf));
            if (lblGameCloneOf != null) lblGameCloneOf.Text = clone;
            SetVisible(!isEmuArc || !string.IsNullOrWhiteSpace(clone), lblGameCloneOfLabel, lblGameCloneOf);

            // RomOf
            string romOf = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.RomOf));
            if (lblGameRomOf != null) lblGameRomOf.Text = romOf;
            SetVisible(!isEmuArc || !string.IsNullOrWhiteSpace(romOf), lblGameRomOfLabel, lblGameRomOf);

            // Year
            string year = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.Year));
            if (lblGameYear != null) lblGameYear.Text = year;
            SetVisible(!isEmuArc || !string.IsNullOrWhiteSpace(year), lblGameYearLabel, lblGameYear);

            // Category
            string cat = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.Category));
            if (string.IsNullOrWhiteSpace(cat) && isEmuArc)
            {
                string genre = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.Genre));
                string sub = NormalizeGameField(tGame.Game.GetData(RvGame.GameData.SubGenre));
                if (!string.IsNullOrWhiteSpace(genre) && !string.IsNullOrWhiteSpace(sub))
                    cat = $"{genre} | {sub}";
                else if (!string.IsNullOrWhiteSpace(genre))
                    cat = genre;
            }
            if (lblGameCategory != null) lblGameCategory.Text = cat;
            SetVisible(!isEmuArc || !string.IsNullOrWhiteSpace(cat), lblGameCategoryLabel, lblGameCategory);
        }
        else
        {
            SetVisible(false, lblGameDescriptionLabel, lblGameDescription, 
                              lblGameManufacturerLabel, lblGameManufacturer,
                              lblGameCloneOfLabel, lblGameCloneOf,
                              lblGameRomOfLabel, lblGameRomOf,
                              lblGameYearLabel, lblGameYear,
                              lblGameCategoryLabel, lblGameCategory);
        }
    }

    private static string NormalizeGameField(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "¤")
            return "";
        return value;
    }

    /// <summary>
    /// Hides all artwork tabs and collapses the artwork column.
    /// </summary>
    private void HideAllArtworkTabs()
    {
        if (GameListGrid != null && GameListGrid.ColumnDefinitions[2].Width.Value > 0)
        {
             _lastArtworkWidth = GameListGrid.ColumnDefinitions[2].Width;
        }

        ResetAllArtworkZoom();
        ResetInfoTextBoxes();
        _mediaContainers.Remove(picLogo);
        _mediaContainers.Remove(picArtwork);
        _mediaContainers.Remove(picMedium1);
        _mediaContainers.Remove(picMedium2);
        _mediaContainers.Remove(picScreenTitle);
        _mediaContainers.Remove(picScreenShot);
        _mediaContainers.Remove(txtInfo);
        _mediaContainers.Remove(txtInfo2);
        TabArtwork.Header = "Artwork";
        TabMedium.Header = "Medium";
        TabScreens.Header = "Screens";
        TabInfo.Header = "Info";
        TabInfo2.Header = "Info2";
        TabArtwork.IsVisible = false;
        TabMedium.IsVisible = false;
        TabScreens.IsVisible = false;
        TabInfo.IsVisible = false;
        TabInfo2.IsVisible = false;

        if (ArtworkSplitter != null) ArtworkSplitter.IsVisible = false;
        if (ArtworkTabs != null) ArtworkTabs.IsVisible = false;
        if (GameListGrid != null) GameListGrid.ColumnDefinitions[2].Width = new GridLength(0);
    }

    /// <summary>
    /// Shows the artwork section and restores its width.
    /// </summary>
    private void ShowArtworkSection()
    {
        if (ArtworkSplitter != null) ArtworkSplitter.IsVisible = true;
        if (ArtworkTabs != null) ArtworkTabs.IsVisible = true;
        if (GameListGrid != null) GameListGrid.ColumnDefinitions[2].Width = _lastArtworkWidth.Value > 0 ? _lastArtworkWidth : new GridLength(300);
    }

    private void OnArtworkPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not global::Avalonia.Controls.Image image)
            return;

        if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control)
            return;

        double zoom = GetArtworkZoom(image);
        if (e.Delta.Y > 0)
            zoom *= 1.12;
        else if (e.Delta.Y < 0)
            zoom /= 1.12;

        SetArtworkZoom(image, zoom);
        e.Handled = true;
    }

    private void OnArtworkDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Image image)
        {
            ResetArtworkZoom(image);
            e.Handled = true;
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
        }
    }

    /// <summary>
    /// Updates the visibility of artwork tabs based on available assets for the selected game.
    /// Checks for emulator specific artwork first, then NFOs, then C64 specifics.
    /// </summary>
    /// <param name="tGame">The selected game file.</param>
    private void UpdateArtworkVisibility(RvFile tGame)
    {
        HideAllArtworkTabs();

        if (tGame == null) return;
        if (tGame.Parent == null) return;

        bool found = false;

        if (tGame.Game != null && tGame.Game.GetData(RvGame.GameData.EmuArc) == "yes")
        {
             LoadTruRipPannel(tGame);
             return;
        }

        string path = tGame.Parent.DatTreeFullName;
        foreach (EmulatorInfo ei in Settings.rvSettings.EInfo)
        {
            if (path.Length <= 8)
                continue;

            if (!string.Equals(path.Substring(8), ei.TreeDir, StringComparison.CurrentCultureIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(ei.ExtraPath))
                continue;

            if (ei.ExtraPath != null)
            {
                found = true;
                if (ei.ExtraPath.Substring(0, 1) == "%")
                    LoadMameSLPannels(tGame, ei.ExtraPath.Substring(1));
                else
                    LoadMamePannels(tGame, ei.ExtraPath);

                break;
            }
        }

        if (!found)
            found = LoadNFOPannel(tGame);

        if (!found)
            found = LoadC64Pannel(tGame);
    }

    /// <summary>
    /// Loads MAME-style artwork panels (artwork, logo, screenshots, cabinets).
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <param name="extraPath">The path to the artwork assets.</param>
    private void LoadMamePannels(RvFile tGame, string extraPath)
    {
        string[] path = extraPath.Split('\\');
        RvFile fExtra = DB.DirRoot.Child(0);

        foreach (string p in path)
        {
            if (fExtra.ChildNameSearch(FileType.Dir, p, out int pIndex) != 0)
                return;
            fExtra = fExtra.Child(pIndex);
        }

        bool artLoaded = false;
        bool logoLoaded = false;
        bool titleLoaded = false;
        bool screenLoaded = false;
        int index;

        if (fExtra.ChildNameSearch(FileType.Zip, "artpreview.zip", out index) == 0)
            artLoaded = TryLoadImage(picArtwork, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
        else if (fExtra.ChildNameSearch(FileType.Dir, "artpreviewsnap", out index) == 0)
            artLoaded = TryLoadImage(picArtwork, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));

        if (fExtra.ChildNameSearch(FileType.Zip, "marquees.zip", out index) == 0)
            logoLoaded = TryLoadImage(picLogo, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
        else if (fExtra.ChildNameSearch(FileType.Dir, "marquees", out index) == 0)
            logoLoaded = TryLoadImage(picLogo, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));

        if (fExtra.ChildNameSearch(FileType.Zip, "snap.zip", out index) == 0)
            screenLoaded = TryLoadImage(picScreenShot, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
        else if (fExtra.ChildNameSearch(FileType.Dir, "snap", out index) == 0)
            screenLoaded = TryLoadImage(picScreenShot, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));

        if (fExtra.ChildNameSearch(FileType.Zip, "cabinets.zip", out index) == 0)
            titleLoaded = TryLoadImage(picScreenTitle, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));
        else if (fExtra.ChildNameSearch(FileType.Dir, "cabinets", out index) == 0)
            titleLoaded = TryLoadImage(picScreenTitle, fExtra.Child(index), Path.GetFileNameWithoutExtension(tGame.Name));

        if (artLoaded || logoLoaded)
        {
            TabArtwork.Header = $"Artwork ({(artLoaded ? 1 : 0) + (logoLoaded ? 1 : 0)})";
            TabArtwork.IsVisible = true;
        }
        if (titleLoaded || screenLoaded)
        {
            TabScreens.Header = $"Screens ({(titleLoaded ? 1 : 0) + (screenLoaded ? 1 : 0)})";
            TabScreens.IsVisible = true;
        }

        if (artLoaded || logoLoaded || titleLoaded || screenLoaded)
        {
            ShowArtworkSection();
        }
    }

    /// <summary>
    /// Loads MAME Software List style artwork panels.
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <param name="extraPath">The path to the artwork assets.</param>
    /// <summary>
    /// Loads MAME Software List style artwork panels.
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <param name="extraPath">The path to the artwork assets.</param>
    private void LoadMameSLPannels(RvFile tGame, string extraPath)
    {
        string[] path = extraPath.Split('\\');
        RvFile fExtra = DB.DirRoot.Child(0);

        foreach (string p in path)
        {
            if (fExtra.ChildNameSearch(FileType.Dir, p, out int pIndex) != 0)
                return;
            fExtra = fExtra.Child(pIndex);
        }

        bool artLoaded = false;
        bool logoLoaded = false;
        bool screenLoaded = false;
        int index;

        string fname = tGame.Parent.Name + "/" + Path.GetFileNameWithoutExtension(tGame.Name);

        if (fExtra.ChildNameSearch(FileType.Zip, "covers_SL.zip", out index) == 0)
            artLoaded = TryLoadImage(picArtwork, fExtra.Child(index), fname);

        if (fExtra.ChildNameSearch(FileType.Zip, "snap_SL.zip", out index) == 0)
            logoLoaded = TryLoadImage(picLogo, fExtra.Child(index), fname);

        if (fExtra.ChildNameSearch(FileType.Zip, "titles_SL.zip", out index) == 0)
            screenLoaded = TryLoadImage(picScreenShot, fExtra.Child(index), fname);

        if (artLoaded || logoLoaded)
        {
            TabArtwork.Header = $"Artwork ({(artLoaded ? 1 : 0) + (logoLoaded ? 1 : 0)})";
            TabArtwork.IsVisible = true;
        }
        if (screenLoaded)
        {
            TabScreens.Header = "Screens (1)";
            TabScreens.IsVisible = true;
        }

        if (artLoaded || logoLoaded || screenLoaded)
        {
            ShowArtworkSection();
        }
    }

    /// <summary>
    /// Loads TruRip specific artwork panels.
    /// </summary>
    /// <param name="tGame">The game file.</param>
    private void LoadTruRipPannel(RvFile tGame)
    {
        ConfigureWrappedText(txtInfo);
        bool artLoaded = TryLoadImage(picArtwork, tGame, "Artwork/artwork_front");
        bool logoLoaded = TryLoadImage(picLogo, tGame, "Artwork/logo");
        if (!logoLoaded)
            logoLoaded = TryLoadImage(picArtwork, tGame, "Artwork/artwork_back");

        bool medium1Loaded = TryLoadImage(picMedium1, tGame, "Artwork/medium_front*");
        bool medium2Loaded = TryLoadImage(picMedium2, tGame, "Artwork/medium_back*");
        bool titleLoaded = TryLoadImage(picScreenTitle, tGame, "Artwork/screentitle");
        bool screenLoaded = TryLoadImage(picScreenShot, tGame, "Artwork/screenshot");
        bool storyLoaded = LoadText(txtInfo, tGame, "Artwork/story.txt");

        if (artLoaded || logoLoaded)
        {
            TabArtwork.Header = $"Artwork ({(artLoaded ? 1 : 0) + (logoLoaded ? 1 : 0)})";
            TabArtwork.IsVisible = true;
        }
        if (medium1Loaded || medium2Loaded)
        {
            TabMedium.Header = $"Medium ({(medium1Loaded ? 1 : 0) + (medium2Loaded ? 1 : 0)})";
            TabMedium.IsVisible = true;
        }
        if (titleLoaded || screenLoaded)
        {
            TabScreens.Header = $"Screens ({(titleLoaded ? 1 : 0) + (screenLoaded ? 1 : 0)})";
            TabScreens.IsVisible = true;
        }
        if (storyLoaded) { TabInfo.Header = "Info"; TabInfo.IsVisible = true; }

        if (artLoaded || logoLoaded || medium1Loaded || medium2Loaded || titleLoaded || screenLoaded || storyLoaded)
        {
            ShowArtworkSection();
        }
    }

    /// <summary>
    /// Loads C64 specific artwork panels (Front, Cassette, Inlay).
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <returns>True if any artwork was loaded.</returns>
    private bool LoadC64Pannel(RvFile tGame)
    {
        bool artLoaded = TryLoadImage(picArtwork, tGame, "Front");
        bool logoLoaded = TryLoadImage(picLogo, tGame, "Extras/Cassette");
        bool titleLoaded = TryLoadImage(picScreenTitle, tGame, "Extras/Inlay");
        bool screenLoaded = TryLoadImage(picScreenShot, tGame, "Extras/Inlay_back");

        if (artLoaded || logoLoaded)
        {
            TabArtwork.Header = $"Artwork ({(artLoaded ? 1 : 0) + (logoLoaded ? 1 : 0)})";
            TabArtwork.IsVisible = true;
        }
        if (titleLoaded || screenLoaded)
        {
            TabScreens.Header = $"Screens ({(titleLoaded ? 1 : 0) + (screenLoaded ? 1 : 0)})";
            TabScreens.IsVisible = true;
        }

        if (artLoaded || logoLoaded || titleLoaded || screenLoaded)
        {
            ShowArtworkSection();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Loads NFO and DIZ files for display.
    /// </summary>
    /// <param name="tGame">The game file.</param>
    /// <returns>True if any text file was loaded.</returns>
    private bool LoadNFOPannel(RvFile tGame)
    {
        ConfigureMonospaceText(txtInfo);
        bool storyLoaded = LoadNFO(txtInfo, tGame, "*.nfo");
        if (storyLoaded)
        {
            TabInfo.Header = "NFO";
            TabInfo.IsVisible = true;
        }

        ConfigureMonospaceText(txtInfo2);
        bool storyLoaded2 = LoadNFO(txtInfo2, tGame, "*.diz");
        if (storyLoaded2)
        {
            TabInfo2.Header = "DIZ";
            TabInfo2.IsVisible = true;
        }
        
        if (storyLoaded || storyLoaded2)
        {
            ShowArtworkSection();
            return true;
        }
        return false;
    }

    // --- Helpers ---

    /// <summary>
    /// Tries to load an image with .png or .jpg extension.
    /// </summary>
    private bool TryLoadImage(global::Avalonia.Controls.Image pic, RvFile tGame, string filename)
    {
        return LoadImage(pic, tGame, filename + ".png") || LoadImage(pic, tGame, filename + ".jpg");
    }

    /// <summary>
    /// Loads an image from a file or zip entry into an Image control.
    /// </summary>
    private bool LoadImage(global::Avalonia.Controls.Image picBox, RvFile tGame, string filename)
    {
        ResetArtworkZoom(picBox);
        picBox.Source = null;
        if (!LoadBytes(tGame, filename, out byte[] memBuffer))
        {
            _mediaContainers.Remove(picBox);
            return false;
        }
        
        try
        {
            using (MemoryStream ms = new MemoryStream(memBuffer))
            {
                picBox.Source = new Bitmap(ms);
            }
            _mediaContainers[picBox] = tGame;
            return true;
        }
        catch
        {
            _mediaContainers.Remove(picBox);
            return false;
        }
    }

    /// <summary>
    /// Loads text from a file or zip entry into a TextBox.
    /// </summary>
    private bool LoadText(TextBox txtBox, RvFile tGame, string filename)
    {
        txtBox.Text = "";
        if (!LoadBytes(tGame, filename, out byte[] memBuffer))
        {
            _mediaContainers.Remove(txtBox);
            return false;
        }

        try
        {
            string txt = System.Text.Encoding.ASCII.GetString(memBuffer);
            txt = txt.Replace("\r\n", "\r\n\r\n");
            txtBox.Text = txt;
            _mediaContainers[txtBox] = tGame;
            return true;
        }
        catch
        {
            _mediaContainers.Remove(txtBox);
            return false;
        }
    }

    /// <summary>
    /// Loads NFO text, attempting to handle CodePage 437 or ASCII.
    /// </summary>
    private bool LoadNFO(TextBox txtBox, RvFile tGame, string search)
    {
        if (!LoadBytes(tGame, search, out byte[] memBuffer))
        {
            _mediaContainers.Remove(txtBox);
            return false;
        }

        try
        {
            // Try to use CodePage 437 if available, else ASCII
            string txt;
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                txt = System.Text.Encoding.GetEncoding(437).GetString(memBuffer);
            }
            catch
            {
                txt = System.Text.Encoding.ASCII.GetString(memBuffer);
            }
            
            txt = txt.Replace("\r\n", "\n");
            txt = txt.Replace("\r", "\n");
            txt = txt.Replace("\n", "\r\n");
            txtBox.Text = txt;
            _mediaContainers[txtBox] = tGame;
            return true;
        }
        catch
        {
            _mediaContainers.Remove(txtBox);
            return false;
        }
    }

    /// <summary>
    /// Converts a wildcard pattern to a regex.
    /// </summary>
    private static Regex WildcardToRegex(string pattern)
    {
        if (pattern.ToLower().StartsWith("regex:"))
            return new Regex(pattern.Substring(6), RegexOptions.IgnoreCase);

        return new Regex("^" + Regex.Escape(pattern).
        Replace("\\*", ".*").
        Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Loads bytes from a file or a zip entry matching the filename pattern.
    /// </summary>
    private static bool LoadBytes(RvFile tGame, string filename, out byte[] memBuffer)
    {
        memBuffer = Array.Empty<byte>();

        Regex rSearch = WildcardToRegex(filename);

        int cCount = tGame.ChildCount;
        if (cCount == 0)
            return false;

        int found = -1;
        for (int i = 0; i < cCount; i++)
        {
            RvFile rvf = tGame.Child(i);
            if (rvf.GotStatus != GotStatus.Got)
                continue;
            if (!rSearch.IsMatch(rvf.Name)) 
                continue;
            found = i;
            break;
        }

        if (found == -1)
            return false;

        try
        {
            switch (tGame.FileType)
            {
                case FileType.Zip:
                    {
                        RvFile imagefile = tGame.Child(found);
                        if (imagefile.ZipFileHeaderPosition == null)
                            return false;

                        Zip zf = new Zip();
                        if (zf.ZipFileOpen(tGame.FullNameCase, tGame.FileModTimeStamp, false) != ZipReturn.ZipGood)
                            return false;

                        if (zf.ZipFileOpenReadStreamFromLocalHeaderPointer((ulong)imagefile.ZipFileHeaderPosition, false,
                                out Stream stream, out ulong streamSize, out ushort _) != ZipReturn.ZipGood)
                        {
                            zf.ZipFileClose();
                            return false;
                        }

                        memBuffer = new byte[streamSize];
                        int bytesRead = 0;
                        while (bytesRead < (int)streamSize)
                        {
                            int read = stream.Read(memBuffer, bytesRead, (int)streamSize - bytesRead);
                            if (read == 0) break;
                            bytesRead += read;
                        }
                        zf.ZipFileClose();
                        return true;
                    }
                case FileType.Dir:
                    {
                        RvFile imagefile = tGame.Child(found);
                        string artwork = imagefile.FullNameCase;
                        if (!File.Exists(artwork))
                            return false;

                        using (FileStream stream = new FileStream(artwork, FileMode.Open, FileAccess.Read))
                        {
                            memBuffer = new byte[stream.Length];
                            int bytesRead = 0;
                            while (bytesRead < memBuffer.Length)
                            {
                                int read = stream.Read(memBuffer, bytesRead, memBuffer.Length - bytesRead);
                                if (read == 0) break;
                                bytesRead += read;
                            }
                        }
                        return true;
                    }
                default:
                    return false;
            }

        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// Updates the ROM Grid (list of individual ROMs inside a game) for the selected game.
    /// </summary>
    /// <param name="tGame">The selected game file.</param>
    private void UpdateRomGrid(RvFile tGame)
    {
        var fileList = new List<RvFile>();
        AddDir(tGame, "", ref fileList);

        bool showMergeColumn = false;
        bool altFound = false;
        bool showStatus = false;
        bool showFileModDate = false;

        for (int i = 0; i < fileList.Count; i++)
        {
            var tFile = fileList[i];

            if (!showMergeColumn && !string.IsNullOrWhiteSpace(tFile.Merge))
            {
                showMergeColumn = true;
            }

            if (!altFound)
            {
                altFound = (tFile.AltSize != null) || (tFile.AltCRC != null) || (tFile.AltSHA1 != null) || (tFile.AltMD5 != null);
            }

            if (!showStatus && !string.IsNullOrWhiteSpace(tFile.Status))
            {
                showStatus = true;
            }

            if (!showFileModDate)
            {
                showFileModDate =
                    (tFile.FileModTimeStamp != 0) &&
                    (tFile.FileModTimeStamp != long.MinValue) &&
                    (tFile.FileModTimeStamp != Compress.StructuredZip.StructuredZip.TrrntzipDateTime) &&
                    (tFile.FileModTimeStamp != Compress.StructuredZip.StructuredZip.TrrntzipDosDateTime);
            }
        }

        var romMergeColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Merge", StringComparison.Ordinal));
        if (romMergeColumn != null) romMergeColumn.IsVisible = showMergeColumn;

        var romAltSizeColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt Size", StringComparison.Ordinal));
        if (romAltSizeColumn != null) romAltSizeColumn.IsVisible = altFound;

        var romAltCRC32Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt CRC32", StringComparison.Ordinal));
        if (romAltCRC32Column != null) romAltCRC32Column.IsVisible = altFound;

        var romAltSHA1Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt SHA1", StringComparison.Ordinal));
        if (romAltSHA1Column != null) romAltSHA1Column.IsVisible = altFound;

        var romAltMD5Column = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Alt MD5", StringComparison.Ordinal));
        if (romAltMD5Column != null) romAltMD5Column.IsVisible = altFound;

        var romStatusColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Status", StringComparison.Ordinal));
        if (romStatusColumn != null) romStatusColumn.IsVisible = showStatus;

        var romFileModDateColumn = RomGrid.Columns.FirstOrDefault(c => string.Equals(c.Header?.ToString(), "Modified Date/Time", StringComparison.Ordinal));
        if (romFileModDateColumn != null) romFileModDateColumn.IsVisible = showFileModDate;

        ApplyRomSort(fileList);
        RomGrid.ItemsSource = fileList;
    }

    /// <summary>
    /// Recursively adds files from a directory to the file list for the ROM Grid.
    /// </summary>
    /// <param name="tGame">The current directory to process.</param>
    /// <param name="pathAdd">The path prefix to add to file names.</param>
    /// <param name="fileList">The list to populate.</param>
    private void AddDir(RvFile tGame, string pathAdd, ref List<RvFile> fileList)
    {
        if (tGame == null) return;

        try
        {
            for (int l = 0; l < tGame.ChildCount; l++)
            {
                RvFile tBase = tGame.Child(l);
                RvFile tFile = tBase;

                if (tFile.IsFile)
                {
                    AddRom(tFile, pathAdd, ref fileList);
                }

                if (tGame.Dat == null) continue;

                RvFile tDir = tBase;
                if (!tDir.IsDirectory) continue;

                if (tDir.Game == null)
                {
                    AddDir(tDir, pathAdd + tDir.Name + "/", ref fileList);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Adds a single ROM file to the file list if it meets the display criteria.
    /// </summary>
    /// <param name="tFile">The file to add.</param>
    /// <param name="pathAdd">The path prefix.</param>
    /// <param name="fileList">The list to populate.</param>
    private void AddRom(RvFile tFile, string pathAdd, ref List<RvFile> fileList)
    {
        try
        {
            if (tFile.DatStatus != DatStatus.InDatMerged || tFile.RepStatus != RepStatus.NotCollected ||
                chkBoxShowMerged.IsChecked == true)
            {
                tFile.UiDisplayName = pathAdd + tFile.Name;
                fileList.Add(tFile);
            }
        }
        catch { }
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
        
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        ScanRoms(scanLevel, rvTree?.Selected);
    }

    /// <summary>
    /// Handles the "Set Dir Dat Settings" context menu click.
    /// Opens the directory settings window.
    /// </summary>
    private async void OnSetDirDatSettingsClick(object? sender, RoutedEventArgs e) 
    {
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
    private async void OnGlobalDirMappingsClick(object? sender, RoutedEventArgs e)
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
        var rvFile = textBlock?.DataContext as RvFile;
        if (rvFile != null)
        {
            var win = new Views.RomInfoWindow();
            win.SetRom(rvFile);
            win.ShowDialog(this);
        }
    }

    private async void OnRomGridCopyCrcClick(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RvFile file)
            return;
        await CopyTextToClipboard(file.CRC32 ?? "");
    }

    private async void OnRomGridCopySha1Click(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RvFile file)
            return;
        await CopyTextToClipboard(file.SHA1Hex ?? "");
    }

    private async void OnRomGridCopyMd5Click(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RvFile file)
            return;
        await CopyTextToClipboard(file.MD5Hex ?? "");
    }

    private void OnRomGridOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (RomGrid.SelectedItem is not RvFile file)
            return;

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
        if (RomGrid.SelectedItem is not RvFile file)
            return;

        var win = new Views.RomInfoWindow();
        win.SetRom(file);
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
        
        if (GameGrid.SelectedItem is RvFile selected)
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
        if (GameGrid.SelectedItem is RvFile thisFile)
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
        if (GameGrid.SelectedItem is RvFile thisFile)
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
        if (GameGrid.SelectedItem is RvFile tGame)
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
        if (GameGrid.SelectedItem is RvFile thisGame)
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
    private void OnUpdateNewDatsClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        UpdateDats();
    }

    /// <summary>
    /// Handles the "Update All DATs" menu click.
    /// Checks for changes in all DATs and updates the database.
    /// </summary>
    private void OnUpdateAllDatsClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        DatUpdate.CheckAllDats(DB.DirRoot.Child(0), @"DatRoot\");
        UpdateDats();
    }

    /// <summary>
    /// Handles the "Scan ROMs" menu click.
    /// </summary>
    private void OnScanRomsClick(object? sender, RoutedEventArgs e) 
    {
         if (_working) return;
         EScanLevel scanLevel = EScanLevel.Level2;
         if (sender is MenuItem menuItem && menuItem.Tag is string level)
        {
            Enum.TryParse(level, out scanLevel);
        }
        ScanRoms(scanLevel);
    }

    private void OnFixRomsClick(object? sender, RoutedEventArgs e)
    {
        if (_working) return;
        FixFiles();
    }
    
    /// <summary>
    /// Handles the "Fix DAT Report" menu click.
    /// </summary>
    private async void OnFixDatReportClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        await Code.Report.CreateFixDat(this, DB.DirRoot.Child(0), true);
    }
    
    /// <summary>
    /// Handles the "Generate Full Report" menu click.
    /// </summary>
    private async void OnFullReportClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        await Code.Report.GenerateReport(this);
    }
    
    /// <summary>
    /// Handles the "Generate Fix Report" menu click.
    /// </summary>
    private async void OnFixReportClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        await Code.Report.GenerateFixReport(this);
    }
    
    /// <summary>
    /// Handles the "Global Dir Dat Settings" menu click.
    /// </summary>
    private async void OnGlobalDirDatSettingsClick(object? sender, RoutedEventArgs e)
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
    private async void OnRomVaultSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_working) return;
        var win = new Views.SettingsWindow();
        await win.ShowDialog(this);
    }

    /// <summary>
    /// Handles the "Add To Sort" menu click.
    /// Adds a new directory to be sorted into the database.
    /// </summary>
    private async void OnAddToSortClick(object? sender, RoutedEventArgs e)
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
    private void OnHelpTorrentZipClick(object? sender, RoutedEventArgs e) 
    {
        var win = new Views.TrrntZipWindow();
        win.Show();
    }

    /// <summary>
    /// Opens the online Wiki.
    /// </summary>
    private void OnHelpWikiClick(object? sender, RoutedEventArgs e) 
    {
        try { Process.Start(new ProcessStartInfo { FileName = "https://wiki.romvault.com/doku.php?id=help", UseShellExecute = true }); } catch { }
    }

    /// <summary>
    /// Shows the Color Key window.
    /// </summary>
    private void OnHelpColorKeyClick(object? sender, RoutedEventArgs e) 
    {
        var win = new Views.KeyWindow();
        win.Show(this);
    }

    private async void OnHelpShortcutsClick(object? sender, RoutedEventArgs e)
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

    /// <summary>
    /// Opens the What's New online page.
    /// </summary>
    private void OnHelpWhatsNewClick(object? sender, RoutedEventArgs e) 
    {
        try { Process.Start(new ProcessStartInfo { FileName = "https://wiki.romvault.com/doku.php?id=whats_new", UseShellExecute = true }); } catch { }
    }

    /// <summary>
    /// Shows the About window.
    /// </summary>
    private void OnHelpAboutClick(object? sender, RoutedEventArgs e) 
    {
        var win = new Views.HelpAboutWindow();
        win.ShowDialog(this);
    }

    /// <summary>
    /// Handles the "Update DATs" toolbar button click.
    /// </summary>
    private void OnUpdateDatsClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        UpdateDats();
    }

    /// <summary>
    /// Handles the "Find Fixes" toolbar button click.
    /// </summary>
    private void OnFindFixesClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        FindFixes();
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
            if (lblStatusRight != null) lblStatusRight.Text = $"Saved preset {index}";
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
            if (lblStatusRight != null) lblStatusRight.Text = $"Cleared preset {index}";
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
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        rvTree?.Setup(DB.DirRoot);
    }

    private void ApplyTreeCheckAll(bool selected)
    {
        if (DB.DirRoot == null)
            return;

        ApplyTreeCheckAllInternal(DB.DirRoot, selected);

        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
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
    /// Handles the "Fix Files" toolbar button click.
    /// </summary>
    private void OnFixFilesClick(object? sender, RoutedEventArgs e) 
    {
         if (_working) return;
         FixFiles();
    }

    /// <summary>
    /// Handles the "Report" toolbar button click.
    /// </summary>
    private async void OnReportClick(object? sender, RoutedEventArgs e) 
    {
        if (_working) return;
        await Code.Report.CreateFixDat(this, DB.DirRoot.Child(0), true);
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

    // Worker Functions

    /// <summary>
    /// Sets the UI to a "Working" state (busy cursor, disabled controls).
    /// </summary>
    private void Start(string activity)
    {
        _working = true;
        this.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Wait);
        if (lblStatusRight != null) lblStatusRight.Text = activity;
        if (StatusProgress != null) StatusProgress.IsVisible = true;
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        if (rvTree != null) rvTree.Working = true;
        var menu = this.FindControl<Menu>("MainMenu");
        if (menu != null) menu.IsEnabled = false;
        var toolbarActions = this.FindControl<StackPanel>("ToolbarActions");
        if (toolbarActions != null) toolbarActions.IsEnabled = false;
    }

    /// <summary>
    /// Resets the UI from a "Working" state.
    /// </summary>
    private void Finish()
    {
        _working = false;
        this.Cursor = global::Avalonia.Input.Cursor.Default;
        if (lblStatusRight != null) lblStatusRight.Text = "";
        if (StatusProgress != null) StatusProgress.IsVisible = false;
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        if (rvTree != null)
        {
            rvTree.Working = false;
            DatSetSelected(rvTree.Selected);
        }
        var menu = this.FindControl<Menu>("MainMenu");
        if (menu != null) menu.IsEnabled = true;
        var toolbarActions = this.FindControl<StackPanel>("ToolbarActions");
        if (toolbarActions != null) toolbarActions.IsEnabled = true;
    }

    /// <summary>
    /// Starts the ROM scanning process in a background thread.
    /// </summary>
    /// <param name="sd">The scan level (depth).</param>
    /// <param name="StartAt">The file/directory to start scanning from. If null, scans everything.</param>
    public void ScanRoms(EScanLevel sd, RvFile? StartAt = null)
    {
        FileScanning.StartAt = StartAt;
        FileScanning.EScanLevel = sd;
        
        Start("Scanning ROMs...");
        
        var thWrk = new ThreadWorker(FileScanning.ScanFiles);
        
        var progressWindow = new Views.ProgressWindow(thWrk);
        progressWindow.Title = "Scanning Roms";
        progressWindow.ShowDialog(this);
        
        thWrk.wFinal += OnScanFinal;
        thWrk.StartAsync();
    }

    private void OnScanReport(object obj)
    {
        // Handled by ProgressWindow
    }

    private void OnScanFinal()
    {
        Dispatcher.UIThread.Post(() => {
            Finish();
        });
    }

    /// <summary>
    /// Starts the DAT update process in a background thread.
    /// Updates the internal database from DAT files.
    /// </summary>
    public void UpdateDats()
    {
        // Preserve selection
        var rvTree = this.FindControl<ROMVault.Avalonia.Views.RvTree>("RvTreeControl");
        RvFile? selected = rvTree?.Selected;
        List<RvFile> parents = new List<RvFile>();
        while (selected != null)
        {
            parents.Add(selected);
            selected = selected.Parent;
        }

        Start("Updating DATs...");

        var thWrk = new ThreadWorker(DatUpdate.UpdateDat);
        
        var progressWindow = new Views.ProgressWindow(thWrk);
        progressWindow.Title = "Updating Dats";
        progressWindow.ShowDialog(this);

        thWrk.wFinal += () => {
             Dispatcher.UIThread.Post(() => {
                // Rebuild Tree
                if (rvTree != null) rvTree.Setup(DB.DirRoot);

                // Restore selection
                while (parents.Count > 1 && parents[0].Parent == null)
                    parents.RemoveAt(0);

                if (parents.Count > 0)
                    selected = parents[0];
                else
                    selected = null;

                if (rvTree != null)
                {
                    rvTree.SetSelected(selected);
                }
                
                Finish();
                
                // Extra Finish steps for UpdateDats
                 DatSetSelected(selected);
            });
        };
        thWrk.StartAsync();
    }

    /// <summary>
    /// Starts the process to find fixes for missing/broken ROMs.
    /// </summary>
    public void FindFixes()
    {
        Start("Finding fixes...");
        var thWrk = new ThreadWorker(RomVaultCore.FindFix.FindFixes.ScanFiles);
        
        var progressWindow = new Views.ProgressWindow(thWrk);
        progressWindow.Title = "Finding Fixes";
        progressWindow.ShowDialog(this);

        thWrk.wFinal += OnScanFinal;
        thWrk.StartAsync();
    }

    /// <summary>
    /// Starts the process to apply fixes (move/rename/copy files).
    /// </summary>
    public void FixFiles()
    {
        Start("Fixing files...");
        var thWrk = new ThreadWorker(RomVaultCore.FixFile.Fix.PerformFixes);
        
        var progressWindow = new Views.ProgressWindow(thWrk);
        progressWindow.Title = "Fixing Files";
        progressWindow.ShowDialog(this);
        
        thWrk.wFinal += OnScanFinal;
        thWrk.StartAsync();
    }

}
