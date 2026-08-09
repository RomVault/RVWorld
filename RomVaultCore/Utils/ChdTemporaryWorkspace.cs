using System;
using System.IO;

namespace RomVaultCore.Utils;

/// <summary>
/// Creates short-lived CHD workspaces on the same mapped storage as the source CHD.
/// </summary>
internal static class ChdTemporaryWorkspace
{
    public static bool TryCreateBesideSource(string sourcePath, string prefix, out string workspace, out string error)
    {
        workspace = "";
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("The source CHD path is empty.", nameof(sourcePath));

            string fullSourcePath = Path.GetFullPath(sourcePath);
            string sourceDirectory = Path.GetDirectoryName(fullSourcePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException("The source CHD directory was not found.");

            workspace = Path.Combine(sourceDirectory, prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            return true;
        }
        catch (Exception ex)
        {
            workspace = "";
            error = ex.Message;
            return false;
        }
    }

    public static bool RunSelfTest(out string error)
    {
        error = "";
        string root = Path.Combine(Path.GetTempPath(), "RomVault-chd-workspace-test-" + Guid.NewGuid().ToString("N"));
        string workspace = "";
        try
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "mapped-suite.chd");
            File.WriteAllBytes(source, new byte[] { 0 });

            if (!TryCreateBesideSource(source, "__RomVault.chdscan.", out workspace, out error))
                return false;
            if (!string.Equals(Path.GetDirectoryName(workspace), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The CHD workspace was not created beside its mapped source.");

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(workspace) && Directory.Exists(workspace))
                    Directory.Delete(workspace, true);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }
}
