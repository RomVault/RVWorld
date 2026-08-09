using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class TrrntZipViewModelTests
{
    [Fact]
    public void Apply_MergesOutOfOrderUpdatesWithoutReplacingRows()
    {
        var viewModel = new TrrntZipViewModel();

        viewModel.Apply(new TrrntZipItemUpdate(2, "third.zip", "Processing"));
        TrrntZipItemViewModel row = viewModel.Items[2];
        viewModel.Apply(new TrrntZipItemUpdate(2, null, "Valid Archive"));

        Assert.Equal(3, viewModel.Items.Count);
        Assert.Same(row, viewModel.Items[2]);
        Assert.Equal("third.zip", row.FileName);
        Assert.Equal("Valid Archive", row.Status);
    }

    [Fact]
    public void Reset_ClearsRowsAndProgress()
    {
        var viewModel = new TrrntZipViewModel();
        viewModel.Apply(new TrrntZipItemUpdate(0, "one.zip", "Done"));
        viewModel.SetProgress(1, 4);

        viewModel.Reset();

        Assert.Empty(viewModel.Items);
        Assert.Equal("( 0 / 0 )", viewModel.TotalStatus);
    }
}
