using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RomVaultCore.Utils;

internal sealed class ChdmanVersionInfo
{
    public string Text { get; set; }
    public Version Version { get; set; }
}

internal static class ChdmanProcessTracker
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<int, Process> Procs = new Dictionary<int, Process>();
    static ChdmanProcessTracker()
    {
        try
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
        }
        catch
        {
        }
    }

    public static string FindExecutable()
    {
        return ChdToolchainRegistry.SelectDefault();
    }

    public static bool TryGetVersion(string executable, out ChdmanVersionInfo versionInfo, out string error)
    {
        versionInfo = null;
        if (!ChdmanService.TryGetIdentity(executable, ChdmanProbeLevel.Banner, out ChdmanIdentity identity, out error))
            return false;
        versionInfo = new ChdmanVersionInfo { Text = identity.VersionText, Version = identity.Version };
        return true;
    }

    public static void Register(Process p)
    {
        if (p == null)
            return;

        try
        {
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) =>
            {
                try
                {
                    lock (Gate)
                    {
                        Procs.Remove(p.Id);
                    }
                }
                catch
                {
                }
            };
        }
        catch
        {
        }

        try
        {
            lock (Gate)
            {
                if (!Procs.ContainsKey(p.Id))
                    Procs.Add(p.Id, p);
            }
        }
        catch
        {
        }
    }

    public static void Kill(Process p)
    {
        if (p == null)
            return;
        try
        {
            if (!p.HasExited)
                p.Kill();
        }
        catch
        {
        }
        try
        {
            lock (Gate)
            {
                Procs.Remove(p.Id);
            }
        }
        catch
        {
        }
        try
        {
            p.Dispose();
        }
        catch
        {
        }
    }

    public static void KillAll()
    {
        List<Process> list = new List<Process>();
        try
        {
            lock (Gate)
            {
                foreach (Process p in Procs.Values)
                    list.Add(p);
                Procs.Clear();
            }
        }
        catch
        {
        }

        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                if (!list[i].HasExited)
                    list[i].Kill();
            }
            catch
            {
            }
            try
            {
                list[i].Dispose();
            }
            catch
            {
            }
        }
    }
}
