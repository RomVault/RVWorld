using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RomVaultCore.RvDB;
using System.Text;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// A window that displays detailed information about a ROM file, including its file group status.
    /// </summary>
    public partial class RomInfoWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RomInfoWindow"/> class.
        /// </summary>
        public RomInfoWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Populates the window with information from the specified <see cref="RvFile"/>.
        /// Lists all files in the file group with their status.
        /// </summary>
        /// <param name="tFile">The ROM file to display information for.</param>
        /// <returns>True if the file has a file group and information was displayed; otherwise, false.</returns>
        public bool SetRom(RvFile tFile)
        {
            if (tFile.FileGroup == null)
                return false;

            StringBuilder sb = new StringBuilder();

            foreach(var v in tFile.FileGroup.Files)
            {
                sb.AppendLine(v.GotStatus+" | "+   v.FullName);
            }
            var textBox = this.FindControl<TextBox>("textBox1");
            if (textBox != null)
            {
                textBox.Text = sb.ToString();
            }
            return true;
        }
    }
}
