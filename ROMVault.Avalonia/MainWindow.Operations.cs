using System.Collections.Generic;
using Avalonia.Input;
using RomVaultCore;
using RomVaultCore.ReadDat;
using RomVaultCore.RvDB;
using RomVaultCore.Scanner;

namespace ROMVault.Avalonia;

public partial class MainWindow
{
    private void Start(string activity)
    {
        _working = true;
        _viewModel.IsBusy = true;
        _viewModel.Activity = activity;
        Cursor = new Cursor(StandardCursorType.Wait);
        RvTreeControl.Working = true;
        MainMenu.IsEnabled = false;
    }

    private void Finish()
    {
        _working = false;
        _viewModel.IsBusy = false;
        _viewModel.Activity = string.Empty;
        Cursor = global::Avalonia.Input.Cursor.Default;
        RvTreeControl.Working = false;
        DatSetSelected(RvTreeControl.Selected);
        MainMenu.IsEnabled = true;
    }

    public void ScanRoms(EScanLevel scanLevel, RvFile? startAt = null)
    {
        FileScanning.StartAt = startAt;
        FileScanning.EScanLevel = scanLevel;

        _operationRunner.Run(
            "Scanning ROMs...",
            "Scanning ROMs",
            new ThreadWorker(FileScanning.ScanFiles));
    }

    public void UpdateDats()
    {
        RvFile? selected = RvTreeControl.Selected;
        var parents = new List<RvFile>();
        while (selected != null)
        {
            parents.Add(selected);
            selected = selected.Parent;
        }

        _operationRunner.Run(
            "Updating DATs...",
            "Updating DATs",
            new ThreadWorker(DatUpdate.UpdateDat),
            () =>
            {
                RvTreeControl.Setup(DB.DirRoot);

                while (parents.Count > 1 && parents[0].Parent == null)
                    parents.RemoveAt(0);

                selected = parents.Count > 0 ? parents[0] : null;
                RvTreeControl.SetSelected(selected);
                DatSetSelected(selected);
            });
    }

    public void FindFixes()
    {
        _operationRunner.Run(
            "Finding fixes...",
            "Finding Fixes",
            new ThreadWorker(RomVaultCore.FindFix.FindFixes.ScanFiles));
    }

    public void FixFiles()
    {
        _operationRunner.Run(
            "Fixing files...",
            "Fixing Files",
            new ThreadWorker(RomVaultCore.FixFile.Fix.PerformFixes));
    }
}
