using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// A simple message box window for displaying information to the user.
    /// </summary>
    public partial class MessageBoxWindow : Window
    {
        private bool _showCancel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBoxWindow"/> class.
        /// </summary>
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the message text to be displayed.
        /// </summary>
        /// <param name="message">The message content.</param>
        public void SetMessage(string message)
        {
            var textBlock = this.FindControl<TextBlock>("MessageText");
            if (textBlock != null)
            {
                textBlock.Text = message;
            }
        }

        public void SetButtons(bool showCancel, string okText = "OK", string cancelText = "Cancel")
        {
            _showCancel = showCancel;

            var btnOk = this.FindControl<Button>("btnOk");
            var btnCancel = this.FindControl<Button>("btnCancel");

            if (btnOk != null) btnOk.Content = okText;
            if (btnCancel != null)
            {
                btnCancel.Content = cancelText;
                btnCancel.IsVisible = showCancel;
            }
        }

        public static async Task ShowInfo(Window owner, string message, string caption = "Message")
        {
            var win = new MessageBoxWindow
            {
                Title = caption
            };
            win.SetMessage(message);
            win.SetButtons(showCancel: false);
            await win.ShowDialog(owner);
        }

        public static async Task<bool> ShowConfirm(Window owner, string message, string caption = "Confirm", string okText = "OK", string cancelText = "Cancel")
        {
            var win = new MessageBoxWindow
            {
                Title = caption
            };
            win.SetMessage(message);
            win.SetButtons(showCancel: true, okText: okText, cancelText: cancelText);

            var result = await win.ShowDialog<bool?>(owner);
            return result == true;
        }

        /// <summary>
        /// Handles the OK button click.
        /// Closes the window.
        /// </summary>
        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
