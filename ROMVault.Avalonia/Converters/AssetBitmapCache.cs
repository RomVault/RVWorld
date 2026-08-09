using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ROMVault.Avalonia.Converters;

/// <summary>
/// Loads each packaged bitmap at most once for the lifetime of the application.
/// </summary>
internal static class AssetBitmapCache
{
    private const string AssetRoot = "avares://ROMVault.Avalonia/Assets/";
    private static readonly Dictionary<string, Bitmap?> Bitmaps = new(StringComparer.Ordinal);
    private static readonly object SyncRoot = new();

    public static Bitmap? Get(string assetName)
    {
        lock (SyncRoot)
        {
            if (Bitmaps.TryGetValue(assetName, out Bitmap? bitmap))
            {
                return bitmap;
            }

            bitmap = Load(assetName);
            Bitmaps.Add(assetName, bitmap);
            return bitmap;
        }
    }

    private static Bitmap? Load(string assetName)
    {
        try
        {
            var uri = new Uri($"{AssetRoot}{assetName}.png");
            if (!AssetLoader.Exists(uri))
            {
                return null;
            }

            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
