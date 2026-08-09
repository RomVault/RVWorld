using System;
using DATReader.DatStore;
using RomVaultCore;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.ViewModels;

/// <summary>
/// Read-only projection of the selected DAT tree node for the shell header.
/// </summary>
public sealed class DatDetailsViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;
    private string _version = string.Empty;
    private string _author = string.Empty;
    private string _date = string.Empty;
    private string _romsGot = "0";
    private string _romsMissing = "0";
    private string _romsFixable = "0";
    private string _romsUnknown = "0";
    private string _effectiveRule = string.Empty;
    private bool _hasEffectiveRule;

    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string Description { get => _description; private set => SetProperty(ref _description, value); }
    public string Category { get => _category; private set => SetProperty(ref _category, value); }
    public string Version { get => _version; private set => SetProperty(ref _version, value); }
    public string Author { get => _author; private set => SetProperty(ref _author, value); }
    public string Date { get => _date; private set => SetProperty(ref _date, value); }
    public string RomsGot { get => _romsGot; private set => SetProperty(ref _romsGot, value); }
    public string RomsMissing { get => _romsMissing; private set => SetProperty(ref _romsMissing, value); }
    public string RomsFixable { get => _romsFixable; private set => SetProperty(ref _romsFixable, value); }
    public string RomsUnknown { get => _romsUnknown; private set => SetProperty(ref _romsUnknown, value); }
    public string EffectiveRule { get => _effectiveRule; private set => SetProperty(ref _effectiveRule, value); }
    public bool HasEffectiveRule { get => _hasEffectiveRule; private set => SetProperty(ref _hasEffectiveRule, value); }

    public void SetSelection(RvFile? selected)
    {
        if (selected is null)
        {
            Clear();
            return;
        }

        string name = selected.Name ?? string.Empty;
        RvDat? dat = selected.Dat ?? (selected.DirDatCount == 1 ? selected.DirDat(0) : null);
        if (dat is not null)
        {
            string datName = Normalize(dat.GetData(RvDat.DatData.DatName));
            if (!string.IsNullOrWhiteSpace(datName) && !string.Equals(name, datName, StringComparison.Ordinal))
            {
                name = $"{name}:  {datName}";
            }

            string id = Normalize(dat.GetData(RvDat.DatData.Id));
            if (!string.IsNullOrWhiteSpace(id))
            {
                name += $" (ID:{id})";
            }

            string header = Normalize(dat.GetData(RvDat.DatData.Header));
            if (!string.IsNullOrWhiteSpace(header))
            {
                name += $" ({header})";
            }

            Description = Normalize(dat.GetData(RvDat.DatData.Description));
            Category = Normalize(dat.GetData(RvDat.DatData.Category));
            Version = Normalize(dat.GetData(RvDat.DatData.Version));
            Author = Normalize(dat.GetData(RvDat.DatData.Author));
            Date = Normalize(dat.GetData(RvDat.DatData.Date));
        }
        else
        {
            Description = Category = Version = Author = Date = string.Empty;
        }

        Name = name;
        RomsGot = selected.DirStatus.CountCorrect().ToString();
        RomsMissing = selected.DirStatus.CountMissing().ToString();
        RomsFixable = selected.DirStatus.CountCanBeFixed().ToString();
        RomsUnknown = selected.DirStatus.CountUnknown().ToString();

        DatRule? rule = ResolveEffectiveRule(selected.TreeFullName);
        HasEffectiveRule = rule is not null;
        if (rule is null)
        {
            EffectiveRule = string.Empty;
            return;
        }

        string inherited = string.Equals(rule.DirKey, selected.TreeFullName, StringComparison.Ordinal)
            ? string.Empty
            : $" (from {rule.DirKey})";
        EffectiveRule = FormatRule(rule) + inherited;
    }

    private void Clear()
    {
        Name = Description = Category = Version = Author = Date = string.Empty;
        RomsGot = RomsMissing = RomsFixable = RomsUnknown = "0";
        EffectiveRule = string.Empty;
        HasEffectiveRule = false;
    }

    private static DatRule? ResolveEffectiveRule(string treePath)
    {
        if (string.IsNullOrWhiteSpace(treePath))
        {
            return null;
        }

        DatRule? best = null;
        foreach (DatRule rule in Settings.rvSettings.DatRules)
        {
            if (string.IsNullOrWhiteSpace(rule.DirKey))
            {
                continue;
            }

            if ((treePath.Equals(rule.DirKey, StringComparison.Ordinal) ||
                 treePath.StartsWith(rule.DirKey + "\\", StringComparison.Ordinal)) &&
                (best is null || rule.DirKey.Length > best.DirKey.Length))
            {
                best = rule;
            }
        }

        return best;
    }

    private static string FormatRule(DatRule rule)
    {
        string summary = $"Archive {rule.Compression}";
        if (rule.Compression == FileType.Zip)
        {
            summary += $", Compression {rule.CompressionSub}";
        }

        summary += $", Merge {rule.Merge}, Header {rule.HeaderType}";
        return rule.SingleArchive ? summary + ", Single" : summary;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrEmpty(value) || value == "¤" ? string.Empty : value;
}
