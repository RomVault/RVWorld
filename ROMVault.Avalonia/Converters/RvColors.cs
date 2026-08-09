using Avalonia.Media;
using RomVaultCore;

namespace ROMVault.Avalonia.Converters;

/// <summary>
/// Central status-color palette shared by the ROM and game grids.
/// </summary>
public static class RvColors
{
    private static readonly Color Blue = Color.FromRgb(214, 214, 255);
    private static readonly Color GreyBlue = Color.FromRgb(214, 224, 255);
    private static readonly Color Red = Color.FromRgb(255, 214, 214);
    private static readonly Color BrightRed = Color.FromRgb(255, 0, 0);
    private static readonly Color Green = Color.FromRgb(214, 255, 214);
    private static readonly Color NeonGreen = Color.FromRgb(100, 255, 100);
    private static readonly Color LightRed = Color.FromRgb(255, 235, 235);
    private static readonly Color SoftGreen = Color.FromRgb(150, 200, 150);
    private static readonly Color Grey = Color.FromRgb(214, 214, 214);
    private static readonly Color Cyan = Color.FromRgb(214, 255, 255);
    private static readonly Color CyanGrey = Color.FromRgb(214, 225, 225);
    private static readonly Color Magenta = Color.FromRgb(255, 214, 255);
    private static readonly Color Brown = Color.FromRgb(140, 80, 80);
    private static readonly Color Purple = Color.FromRgb(214, 140, 214);
    private static readonly Color Yellow = Color.FromRgb(255, 255, 214);
    private static readonly Color DarkYellow = Color.FromRgb(255, 255, 100);
    private static readonly Color Orange = Color.FromRgb(255, 214, 140);
    private static readonly Color White = Color.FromRgb(255, 255, 255);

    static RvColors()
    {
        DisplayColor = new Color[(int)RepStatus.EndValue];
        FontColor = new Color[(int)RepStatus.EndValue];

        DisplayColor[(int)RepStatus.UnScanned] = Blue;
        DisplayColor[(int)RepStatus.DirCorrect] = Green;
        DisplayColor[(int)RepStatus.DirMissing] = Red;
        DisplayColor[(int)RepStatus.DirCorrupt] = BrightRed;
        DisplayColor[(int)RepStatus.Missing] = Red;
        DisplayColor[(int)RepStatus.Correct] = Green;
        DisplayColor[(int)RepStatus.CorrectMIA] = NeonGreen;
        DisplayColor[(int)RepStatus.NotCollected] = Grey;
        DisplayColor[(int)RepStatus.UnNeeded] = CyanGrey;
        DisplayColor[(int)RepStatus.Unknown] = Cyan;
        DisplayColor[(int)RepStatus.InToSort] = Magenta;
        DisplayColor[(int)RepStatus.MissingMIA] = SoftGreen;
        DisplayColor[(int)RepStatus.Corrupt] = BrightRed;
        DisplayColor[(int)RepStatus.Ignore] = GreyBlue;
        DisplayColor[(int)RepStatus.CanBeFixed] = Yellow;
        DisplayColor[(int)RepStatus.CanBeFixedMIA] = DarkYellow;
        DisplayColor[(int)RepStatus.MoveToSort] = Purple;
        DisplayColor[(int)RepStatus.Delete] = Brown;
        DisplayColor[(int)RepStatus.NeededForFix] = Orange;
        DisplayColor[(int)RepStatus.Rename] = Orange;
        DisplayColor[(int)RepStatus.CorruptCanBeFixed] = Yellow;
        DisplayColor[(int)RepStatus.MoveToCorrupt] = Purple;
        DisplayColor[(int)RepStatus.Incomplete] = LightRed;
        DisplayColor[(int)RepStatus.Deleted] = White;

        for (int index = 0; index < (int)RepStatus.EndValue; index++)
        {
            FontColor[index] = index is (int)RepStatus.DirCorrupt or
                (int)RepStatus.Corrupt or
                (int)RepStatus.Delete
                ? Colors.White
                : Colors.Black;
        }
    }

    public static Color[] DisplayColor { get; }

    public static Color[] FontColor { get; }

    public static Color Down(Color color) => Settings.rvSettings.Darkness
        ? Color.FromRgb(
            (byte)(color.R * 0.8),
            (byte)(color.G * 0.8),
            (byte)(color.B * 0.8))
        : color;
}
