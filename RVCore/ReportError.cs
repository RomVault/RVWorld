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

        private static string _logfilename;

        public static ShowError ErrorForm;
        public static MessageDialog Dialog;

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

            s.SendErrorMessageV(Settings.Username + " : " + Settings.EMail + " : " + Settings.IsUnix, 40, message);
            s.Close();
        }

        private static void OpenLogFile(out string dir, out string now)
        {
            dir = Path.Combine(Environment.CurrentDirectory, "Logs");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            now = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            now = now.Replace("\\", "-");
            now = now.Replace("/", "-");
            now = now.Replace(":", "-");

            _logfilename = Path.Combine(dir, now + " UpdateLog.txt");
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

            if (_logfilename == null)
            {
                OpenLogFile(out string dir, out string now);
            }

            TextWriter sw = new StreamWriter(_logfilename, true);
            for (int i = 0; i < files.Count; i++)
            {
                RvFile f = files[i];
                ReportFile(sw, f);
            }

            sw.Flush();
            sw.Close();
        }

        private static StreamWriter _logStreamWriter;
        private static DateTime _lastLogEntry = DateTime.Now;

        public static void LogOut(string s)
        {
            if (!Settings.rvSettings.DebugLogsEnabled)
            {
                return;
            }

            if ((_lastLogEntry.Day != DateTime.Now.Day) || _logStreamWriter == null)
            {
                _lastLogEntry = DateTime.Now;
                OpenLogFile(out string dir, out string now);
                if (_logStreamWriter != null)
                {
                    _logStreamWriter.Flush();
                    _logStreamWriter.Close();
                }

                _logStreamWriter = new StreamWriter(_logfilename, true);

            }
            _logStreamWriter.WriteLine(s);
            _logStreamWriter.Flush();
        }

        public static void LogOut(RvFile f)
        {
            if (!Settings.rvSettings.DebugLogsEnabled)
            {
                return;
            }

            if ((_lastLogEntry.Day != DateTime.Now.Day) || _logStreamWriter == null)
            {
                _lastLogEntry = DateTime.Now;
                OpenLogFile(out string dir, out string now);
                if (_logStreamWriter != null)
                {
                    _logStreamWriter.Flush();
                    _logStreamWriter.Close();
                }

                _logStreamWriter = new StreamWriter(_logfilename, true);

            }
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