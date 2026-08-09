using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using RomVaultCore.RvDB;
using ROMVault.Avalonia.Services;

namespace ROMVault.Avalonia.ViewModels;

public sealed class MediaInspectorViewModel : ObservableObject, IDisposable
{
    private readonly IGameMediaPreviewService _service;
    private CancellationTokenSource? _loadCancellation;
    private GameMediaPreview _preview = new();
    private bool _isLoading;
    private bool _hasSelection;
    private string _errorMessage = string.Empty;
    private int _selectionVersion;

    public MediaInspectorViewModel(IGameMediaPreviewService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public Bitmap? Artwork => _preview.Artwork?.Bitmap;
    public Bitmap? Logo => _preview.Logo?.Bitmap;
    public Bitmap? MediumFront => _preview.MediumFront?.Bitmap;
    public Bitmap? MediumBack => _preview.MediumBack?.Bitmap;
    public Bitmap? ScreenTitle => _preview.ScreenTitle?.Bitmap;
    public Bitmap? Screenshot => _preview.Screenshot?.Bitmap;
    public string Info => _preview.Info;
    public string Info2 => _preview.Info2;
    public string InfoHeader => _preview.InfoHeader;
    public string Info2Header => _preview.Info2Header;
    public RvFile? ArtworkContainer => _preview.Artwork?.Container;
    public RvFile? LogoContainer => _preview.Logo?.Container;
    public RvFile? MediumFrontContainer => _preview.MediumFront?.Container;
    public RvFile? MediumBackContainer => _preview.MediumBack?.Container;
    public RvFile? ScreenTitleContainer => _preview.ScreenTitle?.Container;
    public RvFile? ScreenshotContainer => _preview.Screenshot?.Container;
    public RvFile? InfoContainer => _preview.InfoContainer;
    public RvFile? Info2Container => _preview.Info2Container;

    public bool HasArtwork => Artwork is not null || Logo is not null;
    public bool HasMedium => MediumFront is not null || MediumBack is not null;
    public bool HasScreens => ScreenTitle is not null || Screenshot is not null;
    public bool HasInfo => !string.IsNullOrEmpty(Info);
    public bool HasInfo2 => !string.IsNullOrEmpty(Info2);
    public bool HasAny => _preview.HasAny;
    public bool ShowEmpty => !HasAny && !IsLoading && !HasError;
    public bool ShowError => HasError && !IsLoading;
    public string EmptyMessage => _hasSelection
        ? "No artwork or text preview is available for this game."
        : "Select a game to view its artwork and metadata.";
    public string ArtworkHeader => $"Artwork ({Count(Artwork, Logo)})";
    public string MediumHeader => $"Medium ({Count(MediumFront, MediumBack)})";
    public string ScreensHeader => $"Screens ({Count(ScreenTitle, Screenshot)})";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowEmpty));
                OnPropertyChanged(nameof(ShowError));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShowEmpty));
                OnPropertyChanged(nameof(ShowError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public async Task SelectGameAsync(RvFile? game)
    {
        if (_hasSelection != (game is not null))
        {
            _hasSelection = game is not null;
            OnPropertyChanged(nameof(EmptyMessage));
        }

        int version = Interlocked.Increment(ref _selectionVersion);
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref _loadCancellation,
            game is null ? null : new CancellationTokenSource());
        previous?.Cancel();
        previous?.Dispose();

        ReplacePreview(new GameMediaPreview());
        ErrorMessage = string.Empty;
        if (game is null)
        {
            IsLoading = false;
            return;
        }

        CancellationTokenSource cancellation = _loadCancellation!;
        IsLoading = true;
        try
        {
            GameMediaPreview loaded = await _service.LoadAsync(game, cancellation.Token);
            if (version != _selectionVersion || cancellation.IsCancellationRequested)
            {
                loaded.Dispose();
                return;
            }

            ReplacePreview(loaded);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (version == _selectionVersion)
            {
                ErrorMessage = $"Preview unavailable: {exception.Message}";
            }
        }
        finally
        {
            if (version == _selectionVersion)
            {
                IsLoading = false;
            }
        }
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _selectionVersion);
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        _preview.Dispose();
        if (_service is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ReplacePreview(GameMediaPreview preview)
    {
        GameMediaPreview previous = _preview;
        _preview = preview;
        previous.Dispose();
        NotifyPreviewChanged();
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(Artwork));
        OnPropertyChanged(nameof(Logo));
        OnPropertyChanged(nameof(MediumFront));
        OnPropertyChanged(nameof(MediumBack));
        OnPropertyChanged(nameof(ScreenTitle));
        OnPropertyChanged(nameof(Screenshot));
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Info2));
        OnPropertyChanged(nameof(InfoHeader));
        OnPropertyChanged(nameof(Info2Header));
        OnPropertyChanged(nameof(ArtworkContainer));
        OnPropertyChanged(nameof(LogoContainer));
        OnPropertyChanged(nameof(MediumFrontContainer));
        OnPropertyChanged(nameof(MediumBackContainer));
        OnPropertyChanged(nameof(ScreenTitleContainer));
        OnPropertyChanged(nameof(ScreenshotContainer));
        OnPropertyChanged(nameof(InfoContainer));
        OnPropertyChanged(nameof(Info2Container));
        OnPropertyChanged(nameof(HasArtwork));
        OnPropertyChanged(nameof(HasMedium));
        OnPropertyChanged(nameof(HasScreens));
        OnPropertyChanged(nameof(HasInfo));
        OnPropertyChanged(nameof(HasInfo2));
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(ArtworkHeader));
        OnPropertyChanged(nameof(MediumHeader));
        OnPropertyChanged(nameof(ScreensHeader));
    }

    private static int Count(object? first, object? second) =>
        (first is null ? 0 : 1) + (second is null ? 0 : 1);
}
