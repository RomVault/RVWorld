using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace ROMVault.Avalonia.Views.Main;

public partial class GameHeaderView : UserControl
{
    private const double StackThreshold = 760;

    public GameHeaderView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    public ToggleButton ShowComplete => ShowCompleteToggle;
    public ToggleButton ShowPartial => ShowPartialToggle;
    public ToggleButton ShowEmpty => ShowEmptyToggle;
    public ToggleButton ShowFixes => ShowFixesToggle;
    public ToggleButton ShowMia => ShowMiaToggle;
    public ToggleButton ShowMerged => ShowMergedToggle;
    public TextBox Filter => FilterTextBox;
    public Button Clear => ClearButton;
    public Button Help => HelpButton;
    public Popup FilterPopup => SuggestionsPopup;
    public ListBox FilterSuggestions => SuggestionsList;

    private void ApplyResponsiveLayout()
    {
        bool stack = Bounds.Width > 0 && Bounds.Width < StackThreshold;
        HeaderLayout.ColumnDefinitions[0].Width = stack ? new GridLength(1, GridUnitType.Star) : new GridLength(2, GridUnitType.Star);
        HeaderLayout.ColumnDefinitions[1].Width = stack ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        Grid.SetColumn(FiltersPanel, stack ? 0 : 1);
        Grid.SetRow(FiltersPanel, stack ? 1 : 0);
        DetailsPanel.HorizontalAlignment = stack ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }
}
