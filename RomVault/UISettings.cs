using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ROMVault
{
    public static class UISettings
    {
        public static string EMail
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("Email", "").ToString();
            }

            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("Email", value);
            }
        }

        public static string Username
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("UserName", "").ToString();
            }
            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("UserName", value);
            }
        }



        public static bool OptOut
        {
            get
            {
                RegistryKey regKey1 = Registry.CurrentUser;
                regKey1 = regKey1.CreateSubKey("Software\\RomVault3\\User");
                return regKey1.GetValue("OptOut", "").ToString() == "True";
            }
            set
            {
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.CreateSubKey("Software\\RomVault3\\User");
                regKey.SetValue("OptOut", value.ToString());
            }
        }
    }
}