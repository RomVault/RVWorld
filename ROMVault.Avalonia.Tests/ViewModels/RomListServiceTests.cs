using Compress;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class RomListServiceTests
{
    [Fact]
    public void Create_ProjectsDisplayPathWithoutMutatingDomainObject()
    {
        RvFile game = CreateGameWithFile("game", "rom.bin");
        RvFile file = game.Child(0);
        file.UiDisplayName = "legacy value";

        RomListProjection projection = RomListService.Create(game, includeMerged: true);

        RomRowViewModel row = Assert.Single(projection.Rows);
        Assert.Equal("rom.bin", row.DisplayName);
        Assert.Same(file, row.Source);
        Assert.Equal("legacy value", file.UiDisplayName);
    }

    [Fact]
    public void Create_DetectsOptionalColumnsInOneProjectionPass()
    {
        RvFile game = CreateGameWithFile("game", "rom.bin");
        RvFile file = game.Child(0);
        file.Merge = "parent.bin";
        file.AltSize = 42;
        file.Status = "verified";
        file.FileModTimeStamp = DateTime.UtcNow.Ticks;

        RomListProjection projection = RomListService.Create(game, includeMerged: true);

        Assert.True(projection.Columns.Merge);
        Assert.True(projection.Columns.AlternateHashes);
        Assert.True(projection.Columns.Status);
        Assert.True(projection.Columns.Modified);
    }

    [Fact]
    public void Sort_UsesTypedProjectedValues()
    {
        RvFile game = new(FileType.Dir) { Name = "game" };
        game.ChildAdd(new RvFile(FileType.File) { Name = "z.bin", Size = 20 }, 0);
        game.ChildAdd(new RvFile(FileType.File) { Name = "a.bin", Size = 10 }, 1);
        IReadOnlyList<RomRowViewModel> rows = RomListService.Create(game, includeMerged: true).Rows;

        IReadOnlyList<RomRowViewModel> sorted = RomListService.Sort(rows, "Size", ascending: true);

        Assert.Equal(["a.bin", "z.bin"], sorted.Select(row => row.DisplayName));
    }

    private static RvFile CreateGameWithFile(string gameName, string fileName)
    {
        RvFile game = new(FileType.Dir) { Name = gameName };
        game.ChildAdd(new RvFile(FileType.File) { Name = fileName }, 0);
        return game;
    }
}
