using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RomVaultCore.Utils;

public enum ChdErrorCode
{
    None,
    UnsupportedDialect,
    ToolUnavailable,
    CapabilityFailed,
    InvalidDescriptor,
    AmbiguousMapping,
    PayloadMismatch,
    MetadataMismatch,
    InsufficientSpace,
    ToolFailed,
    Timeout,
    Cancelled,
    RecoveryRequired,
    ParentMissing,
    ParentCycle,
    UnsafePath,
    InvalidContainer,
    IoError
}

/// <summary>
/// A stable, UI-independent description of a CHD operation.  Details are
/// intentionally separate from the user-facing message so diagnostics can be
/// opt-in and redacted without losing a machine-readable failure category.
/// </summary>
public sealed class ChdOperationResult
{
    public bool Success { get; private set; }
    public ChdErrorCode Code { get; private set; }
    public string Phase { get; private set; }
    public string Message { get; private set; }
    public string Details { get; private set; }

    public static ChdOperationResult Ok(string phase, string message = "")
    {
        return new ChdOperationResult
        {
            Success = true,
            Code = ChdErrorCode.None,
            Phase = phase ?? "",
            Message = message ?? "",
            Details = ""
        };
    }

    public static ChdOperationResult Fail(ChdErrorCode code, string phase, string message, string details = "")
    {
        if (code == ChdErrorCode.None)
            code = ChdErrorCode.IoError;
        return new ChdOperationResult
        {
            Success = false,
            Code = code,
            Phase = phase ?? "",
            Message = message ?? "",
            Details = details ?? ""
        };
    }

    public override string ToString()
    {
        string prefix = Success ? "OK" : Code.ToString();
        return string.IsNullOrWhiteSpace(Phase)
            ? prefix + ": " + Message
            : prefix + " [" + Phase + "]: " + Message;
    }
}

public static class ChdDiagnosticFormatter
{
    public static string RedactPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "<none>";

        string name;
        try
        {
            name = Path.GetFileName(path);
        }
        catch
        {
            name = "<invalid>";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(path);
        byte[] hash;
        using (SHA256 sha = SHA256.Create())
            hash = sha.ComputeHash(bytes);
        return (string.IsNullOrWhiteSpace(name) ? "<root>" : name) + " [path:" + ToHex(hash, 6) + "]";
    }

    public static string RedactTextPaths(string text, params string[] paths)
    {
        string value = text ?? "";
        if (paths == null)
            return value;
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (!string.IsNullOrWhiteSpace(path))
                value = value.Replace(path, RedactPath(path));
        }
        return value;
    }

    private static string ToHex(byte[] data, int byteCount)
    {
        StringBuilder builder = new StringBuilder(byteCount * 2);
        int count = Math.Min(data?.Length ?? 0, byteCount);
        for (int i = 0; i < count; i++)
            builder.Append(data[i].ToString("x2"));
        return builder.ToString();
    }
}
