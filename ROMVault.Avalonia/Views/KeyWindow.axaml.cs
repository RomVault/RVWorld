using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomVaultCore;
using RomVaultCore.RvDB;
using System;
using System.Collections.Generic;

namespace ROMVault.Avalonia.Views
{
    /// <summary>
    /// A window that displays a legend of the various status icons and colors used in ROMVault.
    /// Explains what each status means (Correct, Missing, ToSort, etc.).
    /// </summary>
    public partial class KeyWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyWindow"/> class.
        /// Populates the key with status icons and descriptions.
        /// </summary>
        public KeyWindow()
        {
            InitializeComponent();
            LoadKey();
        }

        /// <summary>
        /// Loads the list of statuses and populates the UI.
        /// Grouped by Basic, Fix, and Problem statuses.
        /// </summary>
        private void LoadKey()
        {
            var mainStack = this.FindControl<StackPanel>("MainStackPanel");
            if (mainStack == null) return;

            List<RepStatus> displayList = new List<RepStatus>
            {
                RepStatus.Correct,
                RepStatus.CorrectMIA,
                RepStatus.Missing,
                RepStatus.MissingMIA,
                RepStatus.Unknown,
                RepStatus.UnNeeded,
                RepStatus.NotCollected,
                RepStatus.InToSort,
                RepStatus.Ignore,

                RepStatus.CanBeFixed,
                RepStatus.CanBeFixedMIA,
                RepStatus.NeededForFix,
                RepStatus.Rename,
                RepStatus.MoveToSort,
                RepStatus.Incomplete,
                RepStatus.Delete,


                RepStatus.Corrupt,
                RepStatus.UnScanned,
            };

            AddLabel(mainStack, "Basic Statuses");

            for (int i = 0; i < displayList.Count; i++)
            {
                if (i == 9)
                {
                    AddLabel(mainStack, "Fix Statuses");
                }

                if (i == 16)
                {
                    AddLabel(mainStack, "Problem Statuses");
                }

                AddRow(mainStack, displayList[i]);
            }
        }

        /// <summary>
        /// Adds a section label to the stack panel.
        /// </summary>
        /// <param name="stack">The parent stack panel.</param>
        /// <param name="text">The label text.</param>
        private void AddLabel(StackPanel stack, string text)
        {
            stack.Children.Add(new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 10, 0, 5)
            });
        }

        /// <summary>
        /// Adds a status row (icon and description) to the stack panel.
        /// </summary>
        /// <param name="stack">The parent stack panel.</param>
        /// <param name="status">The status to display.</param>
        private void AddRow(StackPanel stack, RepStatus status)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50, *"),
                Margin = new Thickness(0, 2, 0, 2)
            };

            // Image
            var image = new Image
            {
                Width = 48,
                Height = 42,
                Stretch = Stretch.None
            };
            
            try
            {
                string assetPath = $"avares://ROMVault.Avalonia/Assets/G_{status}.png";
                image.Source = new Bitmap(AssetLoader.Open(new Uri(assetPath)));
            }
            catch { }

            // Border for image
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = image,
                Width = 50,
                Height = 44
            };

            // Text
            string text = GetStatusText(status);
            var textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            
            var textBorder = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = textBlock,
                Padding = new Thickness(5),
                Background = Brushes.Transparent // Should be Control color, but transparent works
            };

            Grid.SetColumn(border, 0);
            Grid.SetColumn(textBorder, 1);

            grid.Children.Add(border);
            grid.Children.Add(textBorder);

            stack.Children.Add(grid);
        }

        /// <summary>
        /// Gets the descriptive text for a given status.
        /// </summary>
        /// <param name="status">The status enum.</param>
        /// <returns>The description string.</returns>
        private string GetStatusText(RepStatus status)
        {
            switch (status)
            {
                case RepStatus.Missing:
                    return "Red - This ROM is missing.";
                case RepStatus.MissingMIA:
                    return "Salmon - This ROM is known to be private or missing in action (MIA).";
                case RepStatus.Correct:
                    return "Green - This ROM is Correct.";
                case RepStatus.CorrectMIA:
                    return "SuperGreen - The ROM was known to be MIA (Missing In Action), but you found it. (Good Job!)";
                case RepStatus.NotCollected:
                    return "Gray - The ROM is not collected here because it belongs in the parent or primary deduped set.";
                case RepStatus.UnNeeded:
                    return "Light Cyan - The ROM is not needed here because it belongs in the parent or primary deduped set.";
                case RepStatus.Unknown:
                    return "Cyan - The ROM is not needed here. Use 'Find Fixes' to see what should be done with the ROM.";
                case RepStatus.InToSort:
                    return "Magenta - The ROM is in a ToSort directory.";
                case RepStatus.Corrupt:
                    return "Red - This file is corrupt.";
                case RepStatus.UnScanned:
                    return "Blue - The file could not be scanned. The file could be locked or have incompatible permissions.";
                case RepStatus.Ignore:
                    return "GreyBlue - The file matches an ignore rule.";
                case RepStatus.CanBeFixed:
                    return "Yellow - The ROM is missing here, but it's available elsewhere. The ROM will be fixed.";
                case RepStatus.CanBeFixedMIA:
                    return "SuperYellow - The MIA ROM is missing here, but it's available elsewhere. The ROM will be fixed.";
                case RepStatus.MoveToSort:
                    return "Purple - The ROM is not needed here, but a copy isn't located elsewhere. The ROM will be moved to the Primary ToSort.";
                case RepStatus.Delete:
                    return "Brown - The ROM is not needed here, but a copy is located elsewhere. The ROM will be deleted.";
                case RepStatus.NeededForFix:
                    return "Orange - The ROM is not needed here, but it's needed elsewhere. The ROM will be moved.";
                case RepStatus.Rename:
                    return "Light Orange - The ROM is needed here, but has the incorrect name. The ROM will be renamed.";
                case RepStatus.Incomplete:
                    return "Pink - This is a ROM that could be fixed, but will not be because it is part of an incomplete set.";
                default:
                    return "";
            }
        }
    }
}
