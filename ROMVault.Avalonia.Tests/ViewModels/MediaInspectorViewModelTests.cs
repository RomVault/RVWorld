using Compress;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.Services;
using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class MediaInspectorViewModelTests
{
    [Fact]
    public async Task SelectGameAsync_CancelsThePreviousSelectionLoad()
    {
        var service = new ControllablePreviewService();
        using var viewModel = new MediaInspectorViewModel(service);
        var firstGame = new RvFile(FileType.Dir) { Name = "first" };
        var secondGame = new RvFile(FileType.Dir) { Name = "second" };

        Task firstLoad = viewModel.SelectGameAsync(firstGame);
        await service.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task secondLoad = viewModel.SelectGameAsync(secondGame);

        await Task.WhenAll(firstLoad, secondLoad).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(service.FirstLoadCancelled);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task SelectGameAsync_UsesSelectionSpecificEmptyMessage()
    {
        using var viewModel = new MediaInspectorViewModel(new EmptyPreviewService());
        Assert.Contains("Select a game", viewModel.EmptyMessage);

        await viewModel.SelectGameAsync(new RvFile(FileType.Dir) { Name = "empty" });

        Assert.Contains("No artwork", viewModel.EmptyMessage);
        Assert.True(viewModel.ShowEmpty);
    }

    private sealed class ControllablePreviewService : IGameMediaPreviewService
    {
        private int _callCount;

        public TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool FirstLoadCancelled { get; private set; }

        public async Task<GameMediaPreview> LoadAsync(
            RvFile game,
            CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _callCount);
            if (call != 1)
            {
                return new GameMediaPreview();
            }

            FirstLoadStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                FirstLoadCancelled = true;
                throw;
            }

            return new GameMediaPreview();
        }
    }

    private sealed class EmptyPreviewService : IGameMediaPreviewService
    {
        public Task<GameMediaPreview> LoadAsync(RvFile game, CancellationToken cancellationToken) =>
            Task.FromResult(new GameMediaPreview());
    }
}
