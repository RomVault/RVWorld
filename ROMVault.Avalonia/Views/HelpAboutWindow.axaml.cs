using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Reflection;
using ROMVault.Avalonia.Utils;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// Window that displays application version information and links to the website and donation page.
    /// </summary>
    public partial class HelpAboutWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HelpAboutWindow"/> class.
        /// Sets the window title and version label.
        /// </summary>
        public HelpAboutWindow()
        {
            InitializeComponent();
            
            string strVersion = BuildInfo.DisplayString;
            Title = "Version " + strVersion + " : " + AppDomain.CurrentDomain.BaseDirectory;
            
            var lblVersion = this.FindControl<TextBlock>("lblVersion");
            if (lblVersion != null)
            {
                lblVersion.Text = "Version " + strVersion;
            }
        }

        /// <summary>
        /// Handles click on the website link.
        /// Opens the ROMVault website in the default browser.
        /// </summary>
        private void Website_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            try 
            { 
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://www.romvault.com/",
                    UseShellExecute = true
                });
            } 
            catch { }
        }

        /// <summary>
        /// Handles click on the PayPal link.
        /// Opens the PayPal donation page in the default browser.
        /// </summary>
        private void PayPal_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            try 
            { 
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://paypal.me/romvault",
                    UseShellExecute = true
                });
            } 
            catch { }
        }
    }
}
