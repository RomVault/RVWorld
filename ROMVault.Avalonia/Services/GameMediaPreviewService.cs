using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using RomVaultCore;
using RomVaultCore.RvDB;

namespace ROMVault.Avalonia.Services;

public sealed class MediaPreviewSlot : IDisposable
{
    private readonly MediaBitmapLease _lease;

    internal MediaPreviewSlot(MediaBitmapLease lease, RvFile container)
    {
        _lease = lease;
        Container = container;
    }

    public Bitmap Bitmap => _lease.Bitmap;
    public RvFile Container { get; }

    public void Dispose() => _lease.Dispose();
}

public sealed class GameMediaPreview : IDisposable
{
    public static GameMediaPreview Empty { get; } = new();

    public MediaPreviewSlot? Artwork { get; internal set; }
    public MediaPreviewSlot? Logo { get; internal set; }
    public MediaPreviewSlot? MediumFront { get; internal set; }
    public MediaPreviewSlot? MediumBack { get; internal set; }
    public MediaPreviewSlot? ScreenTitle { get; internal set; }
    public MediaPreviewSlot? Screenshot { get; internal set; }
    public string Info { get; internal set; } = string.Empty;
    public string Info2 { get; internal set; } = string.Empty;
    public RvFile? InfoContainer { get; internal set; }
    public RvFile? Info2Container { get; internal set; }
    public string InfoHeader { get; internal set; } = "Info";
    public string Info2Header { get; internal set; } = "Info 2";

    public bool HasAny =>
        Artwork is not null ||
        Logo is not null ||
        MediumFront is not null ||
        MediumBack is not null ||
        ScreenTitle is not null ||
        Screenshot is not null ||
        !string.IsNullOrEmpty(Info) ||
        !string.IsNullOrEmpty(Info2);

    public void Dispose()
    {
        Artwork?.Dispose();
        Logo?.Dispose();
        MediumFront?.Dispose();
        MediumBack?.Dispose();
        ScreenTitle?.Dispose();
        Screenshot?.Dispose();
    }
}

public interface IGameMediaPreviewService
{
    Task<GameMediaPreview> LoadAsync(RvFile game, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves ROMVault's supported artwork conventions and creates a single preview snapshot.
/// </summary>
public sealed class GameMediaPreviewService : IGameMediaPreviewService, IDisposable
{
    private const int MaximumImageBytes = 64 * 1024 * 1024;
    private const int MaximumTextBytes = 4 * 1024 * 1024;
    private const int DecodeWidth = 1600;

    private readonly IMediaContentService _content;
    private readonly MediaBitmapCache _bitmapCache;

    public GameMediaPreviewService(
        IMediaContentService? content = null,
        MediaBitmapCache? bitmapCache = null)
    {
        _content = content ?? new MediaContentService();
        _bitmapCache = bitmapCache ?? new MediaBitmapCache();
    }

    public async Task<GameMediaPreview> LoadAsync(
        RvFile game,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Parent is null)
        {
            return new GameMediaPreview();
        }

        if (game.Game?.GetData(RvGame.GameData.EmuArc) == "yes")
        {
            return await LoadTruRipAsync(game, cancellationToken);
        }

        string treePath = game.Parent.DatTreeFullName;
        foreach (EmulatorInfo emulator in Settings.rvSettings.EInfo)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (treePath.Length <= 8 ||
                !string.Equals(treePath[8..], emulator.TreeDir, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(emulator.ExtraPath))
            {
                continue;
            }

            return emulator.ExtraPath.StartsWith('%')
                ? await LoadMameSoftwareListAsync(game, emulator.ExtraPath[1..], cancellationToken)
                : await LoadMameAsync(game, emulator.ExtraPath, cancellationToken);
        }

        GameMediaPreview nfo = await LoadNfoAsync(game, cancellationToken);
        if (nfo.HasAny)
        {
            return nfo;
        }

        nfo.Dispose();
        return await LoadC64Async(game, cancellationToken);
    }

    public void Dispose() => _bitmapCache.Dispose();

    private async Task<GameMediaPreview> LoadMameAsync(
        RvFile game,
        string extraPath,
        CancellationToken cancellationToken)
    {
        RvFile? root = ResolveExtraRoot(extraPath);
        if (root is null)
        {
            return new GameMediaPreview();
        }

        string name = System.IO.Path.GetFileNameWithoutExtension(game.Name);
        var preview = new GameMediaPreview
        {
            Artwork = await LoadFromNamedContainerAsync(root, "artpreview.zip", "artpreviewsnap", name, cancellationToken),
            Logo = await LoadFromNamedContainerAsync(root, "marquees.zip", "marquees", name, cancellationToken),
            Screenshot = await LoadFromNamedContainerAsync(root, "snap.zip", "snap", name, cancellationToken),
            ScreenTitle = await LoadFromNamedContainerAsync(root, "cabinets.zip", "cabinets", name, cancellationToken)
        };
        return preview;
    }

    private async Task<GameMediaPreview> LoadMameSoftwareListAsync(
        RvFile game,
        string extraPath,
        CancellationToken cancellationToken)
    {
        RvFile? root = ResolveExtraRoot(extraPath);
        if (root is null)
        {
            return new GameMediaPreview();
        }

        string name = game.Parent.Name + "/" + System.IO.Path.GetFileNameWithoutExtension(game.Name);
        var preview = new GameMediaPreview
        {
            Artwork = await LoadFromArchiveAsync(root, "covers_SL.zip", name, cancellationToken),
            Logo = await LoadFromArchiveAsync(root, "snap_SL.zip", name, cancellationToken),
            Screenshot = await LoadFromArchiveAsync(root, "titles_SL.zip", name, cancellationToken)
        };
        return preview;
    }

    private async Task<GameMediaPreview> LoadTruRipAsync(
        RvFile game,
        CancellationToken cancellationToken)
    {
        MediaPreviewSlot? logo = await LoadImageAsync(game, "Artwork/logo", cancellationToken);
        logo ??= await LoadImageAsync(game, "Artwork/artwork_back", cancellationToken);
        var preview = new GameMediaPreview
        {
            Artwork = await LoadImageAsync(game, "Artwork/artwork_front", cancellationToken),
            Logo = logo,
            MediumFront = await LoadImageAsync(game, "Artwork/medium_front*", cancellationToken),
            MediumBack = await LoadImageAsync(game, "Artwork/medium_back*", cancellationToken),
            ScreenTitle = await LoadImageAsync(game, "Artwork/screentitle", cancellationToken),
            Screenshot = await LoadImageAsync(game, "Artwork/screenshot", cancellationToken)
        };

        (preview.Info, preview.InfoContainer) = await LoadTextAsync(
            game,
            "Artwork/story.txt",
            Encoding.ASCII,
            cancellationToken);
        preview.InfoHeader = "Info";
        return preview;
    }

    private async Task<GameMediaPreview> LoadC64Async(
        RvFile game,
        CancellationToken cancellationToken) =>
        new()
        {
            Artwork = await LoadImageAsync(game, "Front", cancellationToken),
            Logo = await LoadImageAsync(game, "Extras/Cassette", cancellationToken),
            ScreenTitle = await LoadImageAsync(game, "Extras/Inlay", cancellationToken),
            Screenshot = await LoadImageAsync(game, "Extras/Inlay_back", cancellationToken)
        };

    private async Task<GameMediaPreview> LoadNfoAsync(
        RvFile game,
        CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding;
        try
        {
            encoding = Encoding.GetEncoding(437);
        }
        catch (ArgumentException)
        {
            encoding = Encoding.ASCII;
        }

        var preview = new GameMediaPreview { InfoHeader = "NFO", Info2Header = "DIZ" };
        (preview.Info, preview.InfoContainer) = await LoadTextAsync(game, "*.nfo", encoding, cancellationToken);
        (preview.Info2, preview.Info2Container) = await LoadTextAsync(game, "*.diz", encoding, cancellationToken);
        preview.Info = NormalizeLines(preview.Info);
        preview.Info2 = NormalizeLines(preview.Info2);
        return preview;
    }

    private async Task<MediaPreviewSlot?> LoadFromNamedContainerAsync(
        RvFile root,
        string archiveName,
        string directoryName,
        string baseName,
        CancellationToken cancellationToken)
    {
        RvFile? container = FindChild(root, FileType.Zip, archiveName) ??
                            FindChild(root, FileType.Dir, directoryName);
        return container is null
            ? null
            : await LoadImageAsync(container, baseName, cancellationToken);
    }

    private async Task<MediaPreviewSlot?> LoadFromArchiveAsync(
        RvFile root,
        string archiveName,
        string baseName,
        CancellationToken cancellationToken)
    {
        RvFile? container = FindChild(root, FileType.Zip, archiveName);
        return container is null
            ? null
            : await LoadImageAsync(container, baseName, cancellationToken);
    }

    private async Task<MediaPreviewSlot?> LoadImageAsync(
        RvFile container,
        string baseName,
        CancellationToken cancellationToken)
    {
        MediaContent? content = await _content.ReadAsync(
            container,
            baseName + ".png",
            MaximumImageBytes,
            cancellationToken);
        content ??= await _content.ReadAsync(
            container,
            baseName + ".jpg",
            MaximumImageBytes,
            cancellationToken);
        if (content is null)
        {
            return null;
        }

        MediaBitmapLease lease = await _bitmapCache.AcquireAsync(
            content.CacheKey + $"|w{DecodeWidth}",
            content.Bytes,
            DecodeWidth,
            cancellationToken);
        return new MediaPreviewSlot(lease, content.Container);
    }

    private async Task<(string Text, RvFile? Container)> LoadTextAsync(
        RvFile container,
        string pattern,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        MediaContent? content = await _content.ReadAsync(
            container,
            pattern,
            MaximumTextBytes,
            cancellationToken);
        return content is null
            ? (string.Empty, null)
            : (encoding.GetString(content.Bytes), content.Container);
    }

    private static RvFile? ResolveExtraRoot(string path)
    {
        if (DB.DirRoot is null || DB.DirRoot.ChildCount == 0)
        {
            return null;
        }

        RvFile current = DB.DirRoot.Child(0);
        foreach (string part in path.Split(
                     ['\\', '/'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            RvFile? next = FindChild(current, FileType.Dir, part);
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static RvFile? FindChild(RvFile parent, FileType type, string name) =>
        parent.ChildNameSearch(type, name, out int index) == 0
            ? parent.Child(index)
            : null;

    private static string NormalizeLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
