using Avalonia.Controls;
using SAM_UI_Avalonia.ViewModels;

namespace SAM_UI_Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        public MainWindow()
        {
            InitializeComponent();

        }
    }
}