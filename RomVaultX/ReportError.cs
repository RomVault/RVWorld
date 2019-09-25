using System;

namespace RomVaultX
{
    public static class ReportError
    {
        public static void UnhandledExceptionHandler(Exception e)
        {
            try
            {
                // Create Error Message
                string message = string.Format("An Application Error has occurred.\r\n\r\nEXCEPTION:\r\nSource: {0}\r\nMessage: {1}\r\n", e.Source, e.Message);
                if (e.InnerException != null)
                {
                    message += string.Format("\r\nINNER EXCEPTION:\r\nSource: {0}\r\nMessage: {1}\r\n", e.InnerException.Source, e.InnerException.Message);
                }
                message += string.Format("\r\nSTACK TRACE:\r\n{0}", e.StackTrace);

                frmShowError fshow = new frmShowError();
                fshow.settype(message);
                fshow.ShowDialog();
            }
            catch
            {
            }
        }
    }
}