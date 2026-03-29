using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RomVaultCore;
using System;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// A window for displaying critical errors or crash reports.
    /// Allows the user to view the error message before the application exits.
    /// </summary>
    public partial class ShowErrorWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShowErrorWindow"/> class.
        /// Checks settings to display appropriate feedback message.
        /// </summary>
        public ShowErrorWindow()
        {
            InitializeComponent();
            var label1 = this.FindControl<TextBlock>("label1");
            if (Settings.rvSettings.DoNotReportFeedback && label1 != null)
                label1.Text = "You have opted out of sending this Crash Report";
        }

        /// <summary>
        /// Sets the error message text to be displayed.
        /// </summary>
        /// <param name="s">The error message.</param>
        public void settype(string s)
        {
            var textBox = this.FindControl<TextBox>("textBox1");
            if (textBox != null)
            {
                textBox.Text = s;
            }
        }

        /// <summary>
        /// Handles the OK/Close button click.
        /// Terminates the application.
        /// </summary>
        private void Button1_Click(object? sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
