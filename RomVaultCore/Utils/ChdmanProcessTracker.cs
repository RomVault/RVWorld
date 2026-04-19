using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RomVaultCore.Utils;

/// <summary>
/// Tracks spawned <c>chdman</c> processes so they can be safely terminated on shutdown or cancellation.
/// </summary>
/// <remarks>
/// CHD workflows may spawn long-running external processes (create/extract/verify).
/// This helper centralizes lifecycle management to reduce orphaned background processes.
/// </remarks>
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

    /// <summary>
    /// Terminates a single process and removes it from the tracker.
    /// </summary>
    public static void Kill(Process p)
    {
        if (p == null)
            return;
        try
        {
            if (!p.HasExited)
                p.Kill(true);
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

    /// <summary>
    /// Terminates all tracked processes.
    /// </summary>
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
                    list[i].Kill(true);
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
