using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.Services;
using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Views.Main;

public partial class MediaInspectorView : UserControl
{
    private readonly Dictionary<Image, double> _zoom = new();
    private readonly IDesktopShellService _shell = DesktopShellService.Instance;
    private readonly IClipboardService _clipboard = AvaloniaClipboardService.Instance;

    public MediaInspectorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SetupContextMenus();
    }

    private void SetupContextMenus()
    {
        AttachContextMenu(LogoImage);
        AttachContextMenu(ArtworkImage);
        AttachContextMenu(MediumFrontImage);
        AttachContextMenu(MediumBackImage);
        AttachContextMenu(ScreenTitleImage);
        AttachContextMenu(ScreenshotImage);
        AttachContextMenu(InfoText);
        AttachContextMenu(Info2Text);
    }

    private void AttachContextMenu(Control control)
    {
        if (control.ContextMenu is not null)
        {
            return;
        }

        var open = new MenuItem { Header = "Open media container" };
        open.Click += (_, _) => OpenContainer(control);
        var show = new MenuItem { Header = "Show media container in folder" };
        show.Click += (_, _) => ShowContainer(control);
        var copy = new MenuItem { Header = "Copy media container path" };
        copy.Click += async (_, _) => await CopyContainerPath(control);
        control.ContextMenu = new ContextMenu
        {
            ItemsSource = new object[] { open, show, copy }
        };
    }

    private void OnArtworkPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not Image image ||
            (e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control)
        {
            return;
        }

        double current = _zoom.GetValueOrDefault(image, 1.0);
        SetZoom(image, current * (e.Delta.Y > 0 ? 1.12 : 0.89));
        e.Handled = true;
    }

    private void OnArtworkDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Image image)
        {
            SetZoom(image, 1.0);
            e.Handled = true;
        }
    }

    private void SetZoom(Image image, double zoom)
    {
        zoom = Math.Clamp(zoom, 0.25, 6.0);
        _zoom[image] = zoom;
        image.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        image.RenderTransform = new ScaleTransform(zoom, zoom);
    }

    private void OpenContainer(Control control)
    {
        if (ResolveContainer(control) is { } container)
        {
            _shell.OpenPath(container.FullNameCase);
        }
    }

    private void ShowContainer(Control control)
    {
        if (ResolveContainer(control) is { } container)
        {
            _shell.ShowInFolder(container.FullNameCase);
        }
    }

    private async System.Threading.Tasks.Task CopyContainerPath(Control control)
    {
        if (ResolveContainer(control) is { } container)
        {
            await _clipboard.SetTextAsync(this, _shell.ResolvePath(container.FullNameCase));
        }
    }

    private RvFile? ResolveContainer(Control control)
    {
        if (DataContext is not MediaInspectorViewModel viewModel)
        {
            return null;
        }

        if (ReferenceEquals(control, LogoImage)) return viewModel.LogoContainer;
        if (ReferenceEquals(control, ArtworkImage)) return viewModel.ArtworkContainer;
        if (ReferenceEquals(control, MediumFrontImage)) return viewModel.MediumFrontContainer;
        if (ReferenceEquals(control, MediumBackImage)) return viewModel.MediumBackContainer;
        if (ReferenceEquals(control, ScreenTitleImage)) return viewModel.ScreenTitleContainer;
        if (ReferenceEquals(control, ScreenshotImage)) return viewModel.ScreenshotContainer;
        if (ReferenceEquals(control, InfoText)) return viewModel.InfoContainer;
        if (ReferenceEquals(control, Info2Text)) return viewModel.Info2Container;
        return null;
    }
}
