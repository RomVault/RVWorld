using Avalonia;
using System;
using System.Threading.Tasks;
using RomVaultCore;

namespace ROMVault.Avalonia;

/// <summary>
    /// The main entry point for the application.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Initialization code. Don't use any Avalonia, third-party APIs or any
        /// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        /// yet and stuff might break.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    ReportError.UnhandledExceptionHandler(ex);
                else
                    ReportError.UnhandledExceptionHandler(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                ReportError.UnhandledExceptionHandler(e.Exception);
                e.SetObserved();
            };

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// Avalonia configuration, don't remove; also used by visual designer.
        /// </summary>
        /// <returns>The configured AppBuilder.</returns>
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
