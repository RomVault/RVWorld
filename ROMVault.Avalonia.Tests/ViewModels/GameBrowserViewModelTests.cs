using Compress;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class GameBrowserViewModelTests
{
    private static readonly GameVisibilityOptions AllExceptionalRows =
        new(false, false, false, false, false, false);

    [Fact]
    public void Apply_FiltersSortsAndUpdatesPresentationState()
    {
        var browser = new GameBrowserViewModel();
        browser.SetRows(
        [
            CreateRow("Zelda"),
            CreateRow("Mario Bros"),
            CreateRow("Metroid")
        ]);

        browser.Apply(
            AllExceptionalRows,
            GameFilter.Parse("m"),
            "Game (Directory / Zip)",
            true,
            true);

        Assert.Equal(["Mario Bros", "Metroid"], browser.Items.Select(row => row.Name));
        Assert.Equal("2/3", browser.CountText);
        Assert.Equal("Sort: Game (Directory / Zip) ↑", browser.SortText);
        Assert.False(browser.IsEmpty);
    }

    [Fact]
    public void Apply_ProvidesHelpfulEmptySearchState()
    {
        var browser = new GameBrowserViewModel();
        browser.SetRows([CreateRow("Zelda")]);

        browser.Apply(AllExceptionalRows, GameFilter.Parse("mario"), null, true, true);

        Assert.Empty(browser.Items);
        Assert.True(browser.IsEmpty);
        Assert.Contains("No games match", browser.EmptyMessage);
    }

    [Fact]
    public void Clear_ResetsToDirectoryPrompt()
    {
        var browser = new GameBrowserViewModel();
        browser.SetRows([CreateRow("Zelda")]);

        browser.Clear();

        Assert.Empty(browser.AllRows);
        Assert.Equal("0/0", browser.CountText);
        Assert.Equal("Select a directory to browse its games.", browser.EmptyMessage);
    }

    private static GameRowViewModel CreateRow(string name) =>
        new(new RvFile(FileType.Dir) { Name = name });
}
