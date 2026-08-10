using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace RomVaultCore.Utils;

/// <summary>
/// Queries the volume that actually owns a CHD workspace.  Windows uses the
/// native API so mapped drives and UNC shares are handled without converting
/// them into a local DriveInfo-only assumption.
/// </summary>
internal static class ChdFreeSpace
{
    public static bool TryGetAvailableBytes(string path, out long availableBytes, out string error)
    {
        availableBytes = 0;
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The CHD workspace path is empty.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            if (Path.DirectorySeparatorChar == '\\')
            {
                string nativePath = fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? fullPath
                    : fullPath + Path.DirectorySeparatorChar;
                if (!GetDiskFreeSpaceEx(nativePath, out ulong available, out _, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                availableBytes = available > long.MaxValue ? long.MaxValue : (long)available;
                return true;
            }

            string root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
                throw new IOException("The CHD workspace volume could not be identified.");
            availableBytes = new DriveInfo(root).AvailableFreeSpace;
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not determine free space on the selected CHD workspace volume: " + ex.Message;
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailable,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);
}
