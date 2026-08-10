using System.IO;

namespace RomVaultCore.Utils;

internal enum ChdFaultPoint
{
    None,
    JournalCreated,
    StageCreated,
    ProfileMetadataWritten,
    ManifestMetadataWritten,
    FinalCreated,
    BackupCreated,
    DestinationInstalled,
    DestinationVerified
}

internal sealed class ChdInjectedCrashException : IOException
{
    public ChdFaultPoint Point { get; }

    public ChdInjectedCrashException(ChdFaultPoint point)
        : base("Injected CHD transaction interruption at " + point + ".")
    {
        Point = point;
    }
}

/// <summary>Disabled production hook used by transaction recovery tests.</summary>
internal static class ChdFaultInjection
{
    private static readonly object Gate = new object();
    private static ChdFaultPoint _point;

    public static void Arm(ChdFaultPoint point)
    {
        lock (Gate)
            _point = point;
    }

    public static void Clear()
    {
        Arm(ChdFaultPoint.None);
    }

    public static void Check(ChdFaultPoint point)
    {
        lock (Gate)
        {
            if (_point != point || point == ChdFaultPoint.None)
                return;
            _point = ChdFaultPoint.None;
        }
        throw new ChdInjectedCrashException(point);
    }
}
