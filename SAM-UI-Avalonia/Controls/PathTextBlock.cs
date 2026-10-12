using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;

namespace SAM_UI_Avalonia.Controls
{
    /// <summary>
    /// Displays a full file path, trimming the start of the path when it is too
    /// wide to fit. The trimmed text is prefixed with ".." so that the filename
    /// at the end of the path always stays visible.
    /// </summary>
    public class PathTextBlock : Control
    {
        private const string Ellipsis = "..";

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<PathTextBlock, string?>(nameof(Text));

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextBlock.ForegroundProperty.AddOwner<PathTextBlock>();

        public static readonly StyledProperty<double> FontSizeProperty =
            TextBlock.FontSizeProperty.AddOwner<PathTextBlock>();

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<PathTextBlock>();

        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            TextBlock.FontStyleProperty.AddOwner<PathTextBlock>();

        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            TextBlock.FontWeightProperty.AddOwner<PathTextBlock>();

        static PathTextBlock()
        {
            AffectsRender<PathTextBlock>(TextProperty, ForegroundProperty, FontSizeProperty, FontFamilyProperty, FontStyleProperty, FontWeightProperty);
            AffectsMeasure<PathTextBlock>(TextProperty, FontSizeProperty, FontFamilyProperty, FontStyleProperty, FontWeightProperty);
        }

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        private FormattedText Format(string text)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight),
                FontSize,
                Foreground);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            string text = Text ?? string.Empty;

            if (text.Length == 0)
                return new Size(0, 0);

            FormattedText formatted = Format(text);

            double width = double.IsInfinity(availableSize.Width)
                ? formatted.Width
                : System.Math.Min(formatted.Width, availableSize.Width);

            return new Size(width, formatted.Height);
        }

        public override void Render(DrawingContext context)
        {
            string text = Text ?? string.Empty;

            if (text.Length == 0)
                return;

            double available = Bounds.Width;
            FormattedText formatted = Format(text);

            if (formatted.Width > available)
                formatted = Format(TrimStart(text, available));

            context.DrawText(formatted, new Point(0, 0));
        }

        /// <summary>
        /// Finds the longest tail of <paramref name="text"/> that, once prefixed
        /// with "..", still fits within <paramref name="available"/>.
        /// </summary>
        private string TrimStart(string text, double available)
        {
            // Nothing sensible can be shown in this little space.
            if (available <= 0)
                return string.Empty;

            // Binary search for the largest number of trailing characters that fit.
            int low = 0;
            int high = text.Length;
            string best = Ellipsis;

            while (low <= high)
            {
                int take = (low + high) / 2;
                string candidate = Ellipsis + text.Substring(text.Length - take);

                if (Format(candidate).Width <= available)
                {
                    best = candidate;
                    low = take + 1;
                }
                else
                {
                    high = take - 1;
                }
            }

            return best;
        }
    }
}
