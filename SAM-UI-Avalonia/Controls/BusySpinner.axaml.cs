using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SAM_UI_Avalonia.Controls
{
    public partial class BusySpinner : UserControl
    {
        public static readonly StyledProperty<bool> IsBusyProperty =
            AvaloniaProperty.Register<BusySpinner, bool>(nameof(IsBusy), true);

        public bool IsBusy
        {
            get => GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public BusySpinner()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
