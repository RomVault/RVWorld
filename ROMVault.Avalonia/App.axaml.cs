using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using RomVaultCore;
using RomVaultCore.FixFile.FixAZipCore;
using RomVaultCore.Utils;
using ROMVault.Avalonia.Views;
using ROMVault.Avalonia.Services;

namespace ROMVault.Avalonia;

/// <summary>
/// Configures the Avalonia application and initializes the ROMVault core.
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            ConfigureGlobalExceptionLogging();
            ConfigureErrorReporting();
            InitializeCore();
            ConfigureTheme();
            ConfigureDesktopLifetime();

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception exception)
        {
            ExceptionReporter.Write(exception, "Application initialization");
            throw;
        }
    }

    private static void ConfigureGlobalExceptionLogging()
    {
        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
            ExceptionReporter.Write(eventArgs.Exception, "Avalonia UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                ExceptionReporter.Write(exception, "AppDomain");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            ExceptionReporter.Write(eventArgs.Exception, "Unobserved task");
    }

    private void ConfigureErrorReporting()
    {
        ReportError.ErrorForm += ShowError;
        ReportError.Dialog += ShowDialog;
    }

    private static void ShowError(string message)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = new ShowErrorWindow();
            window.settype(message);
            window.Show();
        });
    }

    private void ShowDialog(string text, string caption)
    {
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = new MessageBoxWindow
            {
                Title = caption
            };
            window.SetMessage(text);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is { IsVisible: true } owner)
            {
                await window.ShowDialog(owner);
                return;
            }

            window.Show();
        });
    }

    private static void InitializeCore()
    {
        Settings.rvSettings = Settings.SetDefaults(out string errorReadingSettings);

        if (!string.IsNullOrWhiteSpace(errorReadingSettings))
        {
            ReportError.Show(errorReadingSettings, "Error Reading Settings");
            Debug.WriteLine($"Error Reading Settings: {errorReadingSettings}");
        }

        FindSourceFile.SetFixOrderSettings();
        RootDirsCreate.CheckDatRoot();
        RootDirsCreate.CheckRomRoot();
        RootDirsCreate.CheckToSort();
    }

    private void ConfigureTheme()
    {
        RequestedThemeVariant = Settings.rvSettings.Darkness
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    private void ConfigureDesktopLifetime()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashWindow();
        desktop.MainWindow = splash;
        splash.Closed += (_, _) => ShowMainWindow(desktop);
        splash.Show();
    }

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var mainWindow = new MainWindow();
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
