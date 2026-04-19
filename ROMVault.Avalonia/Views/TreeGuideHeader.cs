using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ROMVault.Avalonia.Views;

/// <summary>
/// Thin decorative control used by the tree header to draw a vertical guide line matching the tree indent.
/// </summary>
public class TreeGuideHeader : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (Bounds.Width < 10)
            return;

        IBrush lineBrush = Brushes.Gray;
        if (TryGetResource("SurfaceBorderBrush", null, out var lineRes) && lineRes is IBrush lb)
            lineBrush = lb;

        var pen = new Pen(lineBrush, 1, new DashStyle(new double[] { 1, 1 }, 0));

        double x = 9;
        using (context.PushClip(new Rect(0, 0, Bounds.Width, Bounds.Height)))
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }
}
