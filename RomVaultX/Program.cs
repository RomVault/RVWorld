using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using RomVaultX.DB;

namespace RomVaultX
{
    public static class Program
    {
        public static DBSqlite db;
        public static readonly Encoding Enc = Encoding.GetEncoding(28591);
        public static SynchronizationContext SyncCont;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            //db = new DBSqlServer();
            db = new DBSqlite();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}