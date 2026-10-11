
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace assistant
{
    public partial class CoreWindow : Window
    {
        private readonly Stopwatch animationClock =
            Stopwatch.StartNew();

        private const int PointCount = 40;

        private ImageBrush textureBrush = null!;
        private ScaleTransform textureScale = null!;
        private TranslateTransform textureShift = null!;
        private RotateTransform textureRotate = null!;

        private readonly Dictionary<
            (DependencyObject, DependencyProperty), Brush
        > originalBrushes = new();

        private readonly Dictionary<UIElement, Color>
            originalShadows = new();

        private readonly Dictionary<Image, BitmapSource>
            originalImages = new();

        private BitmapSource? originalCoreTexture;

        public CoreWindow()
        {
            InitializeComponent();

            textureBrush =
                (ImageBrush)CoreShape.Fill.Clone();

            CoreShape.Fill = textureBrush;

            originalCoreTexture =
                textureBrush.ImageSource as BitmapSource;

            textureScale =
                new ScaleTransform(1.1, 1.1, 0.5, 0.5);

            textureRotate =
                new RotateTransform(0, 0.5, 0.5);

            textureShift = new TranslateTransform();

            TransformGroup textureTransform =
                new TransformGroup();

            textureTransform.Children.Add(textureScale);
            textureTransform.Children.Add(textureRotate);
            textureTransform.Children.Add(textureShift);

            textureBrush.Transform = textureTransform;
        }

        private void CoreWindow_Loaded(
            object? sender,
            RoutedEventArgs e)
        {
            AssistantTheme.CollectImages(
                this,
                originalImages);

            ApplyCurrentTheme();

            UpdateCoreShape(0);

            CompositionTarget.Rendering +=
                CoreWindow_Rendering;
        }

        private void ApplyCurrentTheme()
        {
            AssistantTheme.ColorizeVisuals(
                this,
                originalBrushes,
                originalShadows);

            AssistantTheme.ColorizeImages(
                originalImages);

            if (originalCoreTexture != null)
            {
                textureBrush.ImageSource =
                    AssistantTheme.TintBitmap(
                        originalCoreTexture);
            }
        }

        private void CoreWindow_Closed(
            object? sender,
            EventArgs e)
        {
            CompositionTarget.Rendering -=
                CoreWindow_Rendering;
        }

        private void CoreWindow_Rendering(
            object? sender,
            EventArgs e)
        {
            double time =
                animationClock.Elapsed.TotalSeconds;

            UpdateCoreShape(time);
            UpdateTexture(time);
        }

        // ===== ДВИЖЕНИЕ ТЕКСТУРЫ ЯДРА =====

        private void UpdateTexture(double time)
        {
            textureScale.ScaleX =
                1.1 + 0.06 * Math.Sin(time * 0.32);

            textureScale.ScaleY =
                1.1 + 0.06 * Math.Cos(time * 0.28);

            textureShift.X =
                0.10 * Math.Sin(time * 0.45);

            textureShift.Y =
                0.08 * Math.Cos(time * 0.37);

            textureRotate.Angle =
                4 * Math.Sin(time * 0.23);
        }

        // ===== ОРИГИНАЛЬНАЯ ФОРМА ЯДРА =====

        private void UpdateCoreShape(double time)
        {
            double centerX = CoreShape.Width / 2;
            double centerY = CoreShape.Height / 2;
            double baseRadius = 96;

            Point[] points = new Point[PointCount];

            for (int i = 0; i < PointCount; i++)
            {
                double angle =
                    2 * Math.PI * i / PointCount;

                double variation =
                    0.18 * Math.Sin(
                        2 * angle + time * 0.45)
                    + 0.12 * Math.Sin(
                        3 * angle - time * 0.32 + 1.2)
                    + 0.06 * Math.Sin(
                        5 * angle + time * 0.21 + 2.1)
                    + 0.03 * Math.Sin(
                        7 * angle - time * 0.13);

                double radius =
                    baseRadius * (1 + variation);

                points[i] = new Point(
                    centerX + Math.Cos(angle) * radius,
                    centerY + Math.Sin(angle) * radius);
            }

            PathFigure figure = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = true
            };

            for (int i = 0; i < PointCount; i++)
            {
                Point previous =
                    points[(i - 1 + PointCount) % PointCount];

                Point current = points[i];

                Point next =
                    points[(i + 1) % PointCount];

                Point afterNext =
                    points[(i + 2) % PointCount];

                Vector firstDirection =
                    next - previous;

                Vector secondDirection =
                    afterNext - current;

                Point controlPoint1 =
                    current + firstDirection / 6;

                Point controlPoint2 =
                    next - secondDirection / 6;

                figure.Segments.Add(
                    new BezierSegment(
                        controlPoint1,
                        controlPoint2,
                        next,
                        true));
            }

            PathGeometry shape = new PathGeometry();
            shape.Figures.Add(figure);
            shape.Freeze();

            CoreBaseShape.Data = shape;
            CoreShape.Data = shape;
        }

        // ===== ПЕРЕХОД В ГЛАВНОЕ ОКНО =====

        private void Core_MouseLeftButtonUp(
            object? sender,
            MouseButtonEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();

            mainWindow.Show();
            Close();
        }
    }

    // =================================================
    // ОБЩАЯ ЦВЕТОВАЯ СИСТЕМА MAINWINDOW И COREWINDOW
    // =================================================

    internal static class AssistantTheme
    {
        private const string DefaultHex = "#5BCCFF";

        private static string selectedHex = DefaultHex;
        private static double selectedHue = 195.0;

        public static void SelectColor(string hex)
        {
            selectedHex = hex;

            Color accent = (Color)
                ColorConverter.ConvertFromString(hex);

            ToHsv(
                accent,
                out selectedHue,
                out _,
                out _);
        }

        private static bool IsDefault =>
            selectedHex.Equals(
                DefaultHex,
                StringComparison.OrdinalIgnoreCase);

        // Все исходные синие свечения переводим
        // в один цвет выбранной темы.

        private static Color TintColor(Color color)
        {
            if (IsDefault || !IsOriginalBlue(color))
                return color;

            ToHsv(
                color,
                out _,
                out double saturation,
                out double value);

            if (value < 0.34)
            {
                saturation *= 0.75;
            }
            else if (saturation > 0.82)
            {
                saturation =
                    0.82 + (saturation - 0.82) * 0.35;
            }

            return FromHsv(
                selectedHue,
                saturation,
                value,
                color.A);
        }

        private static bool IsOriginalBlue(Color color)
        {
            if (color.A == 0)
                return false;

            ToHsv(
                color,
                out double hue,
                out double saturation,
                out _);

            return hue >= 174
                && hue <= 267
                && saturation >= 0.14;
        }

        // ===== ПЕРЕВОД RGB В HSV =====

        private static void ToHsv(
            Color c,
            out double hue,
            out double sat,
            out double val)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max =
                Math.Max(r, Math.Max(g, b));

            double min =
                Math.Min(r, Math.Min(g, b));

            double d = max - min;

            val = max;

            sat =
                max < 0.000001 ? 0 : d / max;

            if (d < 0.000001)
            {
                hue = 0;
                return;
            }

            if (max == r)
            {
                hue = ((g - b) / d) % 6;
            }
            else if (max == g)
            {
                hue = (b - r) / d + 2;
            }
            else
            {
                hue = (r - g) / d + 4;
            }

            hue = (hue * 60 + 360) % 360;
        }

        // ===== ПЕРЕВОД HSV В RGB =====

        private static Color FromHsv(
            double h,
            double s,
            double v,
            byte a)
        {
            h = (h % 360 + 360) % 360;

            double chroma = v * s;

            double x =
                chroma * (1 - Math.Abs(
                    (h / 60.0) % 2 - 1));

            double m = v - chroma;

            double r;
            double g;
            double b;

            if (h < 60)
            {
                r = chroma;
                g = x;
                b = 0;
            }
            else if (h < 120)
            {
                r = x;
                g = chroma;
                b = 0;
            }
            else if (h < 180)
            {
                r = 0;
                g = chroma;
                b = x;
            }
            else if (h < 240)
            {
                r = 0;
                g = x;
                b = chroma;
            }
            else if (h < 300)
            {
                r = x;
                g = 0;
                b = chroma;
            }
            else
            {
                r = chroma;
                g = 0;
                b = x;
            }

            return Color.FromArgb(
                a,
                Byte(r + m),
                Byte(g + m),
                Byte(b + m));
        }

        private static byte Byte(double value)
        {
            return (byte)Math.Max(
                0,
                Math.Min(
                    255,
                    Math.Round(value * 255)));
        }

        // ===== ПЕРЕКРАСКА ИНТЕРФЕЙСА =====

        public static void ColorizeVisuals(
            DependencyObject root,

            Dictionary<
                (DependencyObject, DependencyProperty),
                Brush
            > originals,

            Dictionary<UIElement, Color> originalShadows)
        {
            if (root is Window window)
            {
                PaintBrush(
                    window,
                    Control.BackgroundProperty,
                    originals);
            }

            if (root is Panel panel)
            {
                PaintBrush(
                    panel,
                    Panel.BackgroundProperty,
                    originals);
            }

            if (root is Border border)
            {
                PaintBrush(
                    border,
                    Border.BackgroundProperty,
                    originals);

                PaintBrush(
                    border,
                    Border.BorderBrushProperty,
                    originals);
            }

            if (root is Control control)
            {
                PaintBrush(
                    control,
                    Control.BackgroundProperty,
                    originals);

                PaintBrush(
                    control,
                    Control.BorderBrushProperty,
                    originals);

                PaintBrush(
                    control,
                    Control.ForegroundProperty,
                    originals);
            }

            if (root is TextBlock text)
            {
                PaintBrush(
                    text,
                    TextBlock.ForegroundProperty,
                    originals);
            }

            if (root is Shape shape)
            {
                PaintBrush(
                    shape,
                    Shape.FillProperty,
                    originals);

                PaintBrush(
                    shape,
                    Shape.StrokeProperty,
                    originals);
            }

            if (root is UIElement element
                && element.Effect is DropShadowEffect shadow)
            {
                if (!originalShadows.TryGetValue(
                    element,
                    out Color original))
                {
                    original = shadow.Color;

                    originalShadows[element] =
                        original;
                }

                DropShadowEffect changed =
                    shadow.CloneCurrentValue();

                changed.Color =
                    TintColor(original);

                element.Effect = changed;
            }

            for (int i = 0;
                 i < VisualTreeHelper.GetChildrenCount(root);
                 i++)
            {
                ColorizeVisuals(
                    VisualTreeHelper.GetChild(root, i),
                    originals,
                    originalShadows);
            }
        }

        // ===== ПЕРЕКРАСКА КИСТЕЙ =====

        private static void PaintBrush(
            DependencyObject target,
            DependencyProperty property,

            Dictionary<
                (DependencyObject, DependencyProperty),
                Brush
            > originals)
        {
            if (target.GetValue(property) is not Brush brush)
                return;

            var key = (target, property);

            if (!originals.TryGetValue(
                key,
                out Brush? original))
            {
                originals[key] = brush;
                original = brush;
            }

            if (original is SolidColorBrush solid)
            {
                Color updated =
                    TintColor(solid.Color);

                if (IsOriginalBlue(solid.Color))
                {
                    target.SetValue(
                        property,
                        new SolidColorBrush(updated)
                        {
                            Opacity = solid.Opacity
                        });
                }
            }
            else if (original is GradientBrush gradient)
            {
                GradientBrush clone =
                    (GradientBrush)gradient.CloneCurrentValue();

                foreach (GradientStop stop
                         in clone.GradientStops)
                {
                    stop.Color = TintColor(stop.Color);
                }

                target.SetValue(property, clone);
            }
        }

        // ===== СОХРАНЕНИЕ ИСХОДНЫХ PNG =====

        public static void CollectImages(
            DependencyObject root,
            Dictionary<Image, BitmapSource> originals)
        {
            if (root is Image image
                && image.Source is BitmapSource bitmap
                && !originals.ContainsKey(image))
            {
                originals.Add(image, bitmap);
            }

            for (int i = 0;
                 i < VisualTreeHelper.GetChildrenCount(root);
                 i++)
            {
                CollectImages(
                    VisualTreeHelper.GetChild(root, i),
                    originals);
            }
        }

        // ===== ПЕРЕКРАСКА PNG =====

        public static void ColorizeImages(
            Dictionary<Image, BitmapSource> originals)
        {
            foreach (var pair in originals)
            {
                pair.Key.Source =
                    TintBitmap(pair.Value);
            }
        }

        public static BitmapSource TintBitmap(
            BitmapSource original)
        {
            if (IsDefault)
                return original;

            BitmapSource rgba =
                new FormatConvertedBitmap(
                    original,
                    PixelFormats.Bgra32,
                    null,
                    0);

            int stride = rgba.PixelWidth * 4;

            byte[] data =
                new byte[stride * rgba.PixelHeight];

            rgba.CopyPixels(
                data,
                stride,
                0);

            for (int i = 0;
                 i < data.Length;
                 i += 4)
            {
                if (data[i + 3] == 0)
                    continue;

                Color pixel = Color.FromArgb(
                    data[i + 3],
                    data[i + 2],
                    data[i + 1],
                    data[i]);

                if (!IsOriginalBlue(pixel))
                    continue;

                Color tinted =
                    TintColor(pixel);

                data[i] = tinted.B;
                data[i + 1] = tinted.G;
                data[i + 2] = tinted.R;
            }

            BitmapSource result =
                BitmapSource.Create(
                    rgba.PixelWidth,
                    rgba.PixelHeight,
                    rgba.DpiX,
                    rgba.DpiY,
                    PixelFormats.Bgra32,
                    null,
                    data,
                    stride);

            result.Freeze();

            return result;
        }
    }
}
