using System.IO;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.ViewModels;

/// <summary>
/// Read-only metadata projection for the selected game.
/// </summary>
public sealed class GameDetailsViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _manufacturer = string.Empty;
    private string _cloneOf = string.Empty;
    private string _romOf = string.Empty;
    private string _year = string.Empty;
    private string _category = string.Empty;
    private bool _hasDescription;
    private bool _hasManufacturer;
    private bool _hasCloneOf;
    private bool _hasRomOf;
    private bool _hasYear;
    private bool _hasCategory;

    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string Description { get => _description; private set => SetProperty(ref _description, value); }
    public string Manufacturer { get => _manufacturer; private set => SetProperty(ref _manufacturer, value); }
    public string CloneOf { get => _cloneOf; private set => SetProperty(ref _cloneOf, value); }
    public string RomOf { get => _romOf; private set => SetProperty(ref _romOf, value); }
    public string Year { get => _year; private set => SetProperty(ref _year, value); }
    public string Category { get => _category; private set => SetProperty(ref _category, value); }
    public bool HasDescription { get => _hasDescription; private set => SetProperty(ref _hasDescription, value); }
    public bool HasManufacturer { get => _hasManufacturer; private set => SetProperty(ref _hasManufacturer, value); }
    public bool HasCloneOf { get => _hasCloneOf; private set => SetProperty(ref _hasCloneOf, value); }
    public bool HasRomOf { get => _hasRomOf; private set => SetProperty(ref _hasRomOf, value); }
    public bool HasYear { get => _hasYear; private set => SetProperty(ref _hasYear, value); }
    public bool HasCategory { get => _hasCategory; private set => SetProperty(ref _hasCategory, value); }

    public void SetGame(RvFile? gameFile)
    {
        if (gameFile is null)
        {
            Clear();
            return;
        }

        string id = gameFile.Game?.GetData(RvGame.GameData.Id) ?? string.Empty;
        Name = gameFile.Name + (!string.IsNullOrWhiteSpace(id) ? $" (ID:{id})" : string.Empty);
        if (gameFile.Game is null)
        {
            ClearMetadata();
            return;
        }

        bool isEmuArc = gameFile.Game.GetData(RvGame.GameData.EmuArc) == "yes";
        string description = gameFile.Game.GetData(RvGame.GameData.Description);
        Description = description == "¤" ? Path.GetFileNameWithoutExtension(gameFile.Name) : description;
        HasDescription = true;

        Manufacturer = Normalize(gameFile.Game.GetData(RvGame.GameData.Manufacturer));
        CloneOf = Normalize(gameFile.Game.GetData(RvGame.GameData.CloneOf));
        RomOf = Normalize(gameFile.Game.GetData(RvGame.GameData.RomOf));
        Year = Normalize(gameFile.Game.GetData(RvGame.GameData.Year));
        Category = Normalize(gameFile.Game.GetData(RvGame.GameData.Category));
        if (string.IsNullOrWhiteSpace(Category) && isEmuArc)
        {
            string genre = Normalize(gameFile.Game.GetData(RvGame.GameData.Genre));
            string subGenre = Normalize(gameFile.Game.GetData(RvGame.GameData.SubGenre));
            Category = !string.IsNullOrWhiteSpace(genre) && !string.IsNullOrWhiteSpace(subGenre)
                ? $"{genre} | {subGenre}"
                : genre;
        }

        HasManufacturer = !isEmuArc || !string.IsNullOrWhiteSpace(Manufacturer);
        HasCloneOf = !isEmuArc || !string.IsNullOrWhiteSpace(CloneOf);
        HasRomOf = !isEmuArc || !string.IsNullOrWhiteSpace(RomOf);
        HasYear = !isEmuArc || !string.IsNullOrWhiteSpace(Year);
        HasCategory = !isEmuArc || !string.IsNullOrWhiteSpace(Category);
    }

    private void Clear()
    {
        Name = string.Empty;
        ClearMetadata();
    }

    private void ClearMetadata()
    {
        Description = Manufacturer = CloneOf = RomOf = Year = Category = string.Empty;
        HasDescription = HasManufacturer = HasCloneOf = HasRomOf = HasYear = HasCategory = false;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrEmpty(value) || value == "¤" ? string.Empty : value;
}
