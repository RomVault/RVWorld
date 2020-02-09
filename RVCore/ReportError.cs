using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Threading;
using RVCore.RvDB;
using RVCore.RVServices;
using RVCore.Utils;

namespace RVCore
{
    public static class ReportError
    {
        public delegate void ShowError(string message);
        public delegate void MessageDialog(string text, string caption);
        
        public static ShowError ErrorForm;
        public static MessageDialog Dialog;

        public static int vMajor;
        public static int vMinor;
        public static int vBuild;

        public static void UnhandledExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                // Create Error Message
                string message = $"An Application Error has occurred.\r\n\r\nEXCEPTION:\r\nSource: {e.Exception.Source}\r\nMessage: {e.Exception.Message}\r\n";
                if (e.Exception.InnerException != null)
                {
                    message += $"\r\nINNER EXCEPTION:\r\nSource: {e.Exception.InnerException.Source}\r\nMessage: {e.Exception.InnerException.Message}\r\n";
                }
                message += $"\r\nSTACK TRACE:\r\n{e.Exception.StackTrace}";

                SendErrorMessage(message);
                ErrorForm?.Invoke(message);

                Close();
                Environment.Exit(0);
            }
            catch
            {
                Close();
                Environment.Exit(0);
            }
        }

        public static void UnhandledExceptionHandler(Exception e)
        {
            try
            {
                // Create Error Message
                string message = $"An Application Error has occurred.\r\n\r\nEXCEPTION:\r\nSource: {e.Source}\r\nMessage: {e.Message}\r\n";
                if (e.InnerException != null)
                {
                    message += $"\r\nINNER EXCEPTION:\r\nSource: {e.InnerException.Source}\r\nMessage: {e.InnerException.Message}\r\n";
                }
                message += $"\r\nSTACK TRACE:\r\n{e.StackTrace}";

                SendErrorMessage(message);
                ErrorForm?.Invoke(message);
            }
            catch
            {
            }
        }

        public static void UnhandledExceptionHandler(string e1)
        {
            try
            {
                // Create Error Message
                string message = "An Application Error has occurred.\r\n\r\nEXCEPTION:\r\nMessage:";
                message += e1 + "\r\n";

                message += $"\r\nSTACK TRACE:\r\n{Environment.StackTrace}";

                SendErrorMessage(message);
                ErrorForm?.Invoke(message);

                Environment.Exit(0);
            }
            catch
            {
                Environment.Exit(0);
            }
        }



        public static void SendAndShow(string message)
        {
            SendErrorMessage(message);
            Show(message);
        }

        public static void Show(string text, string caption = "RomVault")
        {
            Dialog?.Invoke(text, caption);
        }


        private static void SendErrorMessage(string message)
        {
            if (Settings.OptOut)
                return;

            BasicHttpBinding b = new BasicHttpBinding();
            EndpointAddress e = new EndpointAddress(@"http://services.romvault.com/RVService.svc");
            RVServiceClient s = new RVServiceClient(b, e);

            s.SendErrorMessageV2(Settings.Username + " : " + Settings.EMail + " : " + Settings.IsUnix, vMajor,vMinor,vBuild, message);
            s.Close();
        }

        private static string GetLogFilname()
        {
            string dir = Path.Combine(Environment.CurrentDirectory, "Logs");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string now = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            now = now.Replace("\\", "-");
            now = now.Replace("/", "-");
            now = now.Replace(":", "-");

            return Path.Combine(dir, now + " UpdateLog.txt");
        }
        private static void OpenLog()
        {
            if ((_lastLogEntry.Day == DateTime.Now.Day) && _logStreamWriter != null) 
                return;

            if (_logStreamWriter != null)
            {
                _logStreamWriter.Flush();
                _logStreamWriter.Close();
            }

            _lastLogEntry = DateTime.Now;
            string logFilename = GetLogFilname();
            _logStreamWriter = new StreamWriter(logFilename, true);
        }

        private static void ReportFile(TextWriter sw, RvFile f)
        {
            sw.WriteLine($"{f?.Size?.ToString().PadRight(12)} {f?.CRC?.ToHexString()} {f?.AltSize?.ToString().PadRight(12)} {f?.AltCRC?.ToHexString()} {f?.HeaderFileType} {f?.GotStatus.ToString().PadRight(10)} {f?.RepStatus.ToString().PadRight(15)} {f?.TreeFullName}");
        }

        public static void ReportList(List<RvFile> files)
        {
            if (!Settings.rvSettings.DebugLogsEnabled)
            {
                return;
            }

            OpenLog();
            for (int i = 0; i < files.Count; i++)
            {
                RvFile f = files[i];
                ReportFile(_logStreamWriter, f);
            }
            _logStreamWriter.Flush();
        }

        private static StreamWriter _logStreamWriter;
        private static DateTime _lastLogEntry = DateTime.Now;

        public static void LogOut(string s)
        {
            if (!Settings.rvSettings.DebugLogsEnabled)
            {
                return;
            }

            OpenLog();
            _logStreamWriter.WriteLine(s);
            _logStreamWriter.Flush();
        }

        public static void LogOut(RvFile f)
        {
            if (!Settings.rvSettings.DebugLogsEnabled)
            {
                return;
            }

            OpenLog();
            ReportFile(_logStreamWriter, f);
            _logStreamWriter.Flush();
        }


        public static void Close()
        {
            if (_logStreamWriter == null)
                return;
            _logStreamWriter.Flush();
            _logStreamWriter.Close();
            _logStreamWriter = null;
        }
    }
}