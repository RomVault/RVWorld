using Compress;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class GameDetailsViewModelTests
{
    [Fact]
    public void SetGame_ProjectsSentinelDescriptionAndClearsCleanly()
    {
        var game = new RvFile(FileType.Dir)
        {
            Name = "sample.zip",
            Game = new RvGame("¤")
        };
        var viewModel = new GameDetailsViewModel();

        viewModel.SetGame(game);

        Assert.Equal("sample.zip", viewModel.Name);
        Assert.Equal("sample", viewModel.Description);
        Assert.True(viewModel.HasDescription);

        viewModel.SetGame(null);

        Assert.Empty(viewModel.Name);
        Assert.Empty(viewModel.Description);
        Assert.False(viewModel.HasDescription);
    }
}
