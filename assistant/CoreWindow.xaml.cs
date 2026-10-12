using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace assistant
{
    // Это ЭКРАН, а не отдельное системное окно. Поэтому нет вспышек между окнами.
    public partial class CoreWindow : UserControl
    {
        public event EventHandler? ReturnRequested;
        private readonly Stopwatch animationClock = Stopwatch.StartNew();
        private const int PointCount = 40;
        private ImageBrush textureBrush = null!;
        private ScaleTransform textureScale = null!;
        private TranslateTransform textureShift = null!;
        private RotateTransform textureRotate = null!;
        private readonly Dictionary<(DependencyObject, DependencyProperty), Brush> originalBrushes = new();
        private readonly Dictionary<UIElement, Color> originalShadows = new();
        private string? activeAccent;
        private bool renderingSubscribed;

        public CoreWindow()
        {
            InitializeComponent();
            textureBrush = (ImageBrush)CoreShape.Fill.Clone();
            CoreShape.Fill = textureBrush;
            textureScale = new ScaleTransform(1.1, 1.1, 0.5, 0.5);
            textureRotate = new RotateTransform(0, 0.5, 0.5);
            textureShift = new TranslateTransform();
            var textureTransform = new TransformGroup();
            textureTransform.Children.Add(textureScale);
            textureTransform.Children.Add(textureRotate);
            textureTransform.Children.Add(textureShift);
            textureBrush.Transform = textureTransform;
        }

        private void CoreWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme(AssistantTheme.CurrentHex);
            UpdateCoreShape(0);
            if (!renderingSubscribed)
            {
                CompositionTarget.Rendering += CoreWindow_Rendering;
                renderingSubscribed = true;
            }
        }

        private void CoreWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (renderingSubscribed)
            {
                CompositionTarget.Rendering -= CoreWindow_Rendering;
                renderingSubscribed = false;
            }
        }

        public void ApplyTheme(string accent)
        {
            if (activeAccent == accent) return;
            AssistantTheme.SelectColor(accent);
            AssistantTheme.ColorizeVisuals(this, originalBrushes, originalShadows);
            var rings = ThemeAssets.Load(accent, "OrbitRings.png");
            OrbitImage1.Source = rings;
            OrbitImage2.Source = rings;
            OrbitImage3.Source = rings;
            textureBrush.ImageSource = ThemeAssets.Load(accent, "CoreIdle.png");
            activeAccent = accent;
        }

        private void CoreWindow_Rendering(object? sender, EventArgs e)
        {
            if (!IsVisible) return; // Не тратим ресурсы, когда открыта главная страница.
            double time = animationClock.Elapsed.TotalSeconds;
            UpdateCoreShape(time);
            UpdateTexture(time);
        }

        private void UpdateTexture(double time)
        {
            textureScale.ScaleX = 1.1 + 0.06 * Math.Sin(time * 0.32);
            textureScale.ScaleY = 1.1 + 0.06 * Math.Cos(time * 0.28);
            textureShift.X = 0.10 * Math.Sin(time * 0.45);
            textureShift.Y = 0.08 * Math.Cos(time * 0.37);
            textureRotate.Angle = 4 * Math.Sin(time * 0.23);
        }

        private void UpdateCoreShape(double time)
        {
            double centerX = CoreShape.Width / 2;
            double centerY = CoreShape.Height / 2;
            double baseRadius = 96;

            Point[] points = new Point[PointCount];

            for (int i = 0; i < PointCount; i++)
            {
                double angle = 2 * Math.PI * i / PointCount;
                double variation =
                    0.18 * Math.Sin(2 * angle + time * 0.45) +
                    0.12 * Math.Sin(3 * angle - time * 0.32 + 1.2) +
                    0.06 * Math.Sin(5 * angle + time * 0.21 + 2.1) +
                    0.03 * Math.Sin(7 * angle - time * 0.13);

                double radius = baseRadius * (1 + variation);
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
                Point previous = points[(i - 1 + PointCount) % PointCount];
                Point current = points[i];
                Point next = points[(i + 1) % PointCount];
                Point afterNext = points[(i + 2) % PointCount];

                Vector firstDirection = next - previous;
                Vector secondDirection = afterNext - current;

                Point controlPoint1 = current + firstDirection / 6;
                Point controlPoint2 = next - secondDirection / 6;

                figure.Segments.Add(new BezierSegment(
                    controlPoint1, controlPoint2, next, true));
            }

            PathGeometry shape = new PathGeometry();
            shape.Figures.Add(figure);
            shape.Freeze();

            CoreBaseShape.Data = shape;
            CoreShape.Data = shape;
        }

        private void Core_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ReturnRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // Один выбранный цвет для главного окна, ядра и орбит.
    // Тяжёлая перекраска PNG выполняется НЕ в графическом потоке.
    internal static class AssistantTheme
    {
        private const string DefaultHex = "#5BCCFF";
        private static string selectedHex = DefaultHex;
        private static double selectedHue = 195;

        // Кэш не даёт заново перекрашивать одни и те же картинки.
        private static readonly Dictionary<(BitmapSource, string),
            System.Threading.Tasks.Task<BitmapSource>> cache = new();

        // Один поток занимается перекраской, чтобы не тормозить анимацию.
        private static readonly System.Threading.SemaphoreSlim tintGate = new(1, 1);
        private static readonly Dictionary<BitmapSource, BitmapSource> readySources = new();

        public static string CurrentHex => selectedHex;

        public static void SelectColor(string hex)
        {
            selectedHex = hex;
            Color accent = (Color)ColorConverter.ConvertFromString(hex);
            ToHsv(accent, out selectedHue, out _, out _);
        }

        private static bool IsDefault(string hex) =>
            hex.Equals(DefaultHex, StringComparison.OrdinalIgnoreCase);

        private static bool IsOriginalBlue(Color color)
        {
            if (color.A == 0) return false;
            ToHsv(color, out double hue, out double sat, out _);
            return hue >= 174 && hue <= 267 && sat >= 0.14;
        }

        // Все темы получают ОДНУ насыщенность и одинаково мягкую подсветку.
        // Синий по умолчанию остаётся как в первоначальном макете.
        private static Color TintColor(Color color, double hue, bool image)
        {
            if (!IsOriginalBlue(color)) return color;
            ToHsv(color, out _, out double saturation, out double value);

            // Белые блики специально не перекрашиваются: остаются маленькими
            // естественными искрами. Яркие синие области приглушаем.
            if (image)
            {
                value = Math.Min(value * 0.70, 0.72);
                saturation = Math.Min(saturation * 0.78, 0.74);
            }
            else
            {
                value = Math.Min(value * 0.86, 0.88);
                saturation = Math.Min(saturation * 0.84, 0.78);
            }

            if (value < 0.28) saturation *= 0.78;
            return FromHsv(hue, saturation, value, color.A);
        }

        private static void ToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            v = max;
            s = max <= 0 ? 0 : d / max;
            if (d < 0.000001) { h = 0; return; }
            double hh = max == r ? ((g - b) / d) % 6
                      : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h = (hh * 60 + 360) % 360;
        }

        private static Color FromHsv(double h, double s, double v, byte alpha)
        {
            h = (h % 360 + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(alpha, Byte(r + m), Byte(g + m), Byte(b + m));
        }

        private static byte Byte(double n) =>
            (byte)Math.Max(0, Math.Min(255, Math.Round(n * 255)));

        // Маленькие элементы интерфейса красим сразу: это не блокирует окно.
        public static void ColorizeVisuals(
            DependencyObject root,
            Dictionary<(DependencyObject, DependencyProperty), Brush> originals,
            Dictionary<UIElement, Color> originalShadows)
        {
            if (root is Window window)
                PaintBrush(window, Window.BackgroundProperty, originals);
            if (root is Panel panel)
                PaintBrush(panel, Panel.BackgroundProperty, originals);
            if (root is Border border)
            {
                PaintBrush(border, Border.BackgroundProperty, originals);
                PaintBrush(border, Border.BorderBrushProperty, originals);
            }
            if (root is Control control)
            {
                PaintBrush(control, Control.BackgroundProperty, originals);
                PaintBrush(control, Control.BorderBrushProperty, originals);
                PaintBrush(control, Control.ForegroundProperty, originals);
            }
            if (root is TextBlock text)
                PaintBrush(text, TextBlock.ForegroundProperty, originals);
            if (root is Shape shape)
            {
                PaintBrush(shape, Shape.FillProperty, originals);
                PaintBrush(shape, Shape.StrokeProperty, originals);
            }
            if (root is UIElement element && element.Effect is DropShadowEffect shadow)
            {
                if (!originalShadows.TryGetValue(element, out Color original))
                {
                    original = shadow.Color;
                    originalShadows[element] = original;
                }
                DropShadowEffect changed = shadow.CloneCurrentValue();
                changed.Color = IsDefault(selectedHex)
                    ? original : TintColor(original, selectedHue, false);
                element.Effect = changed;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                ColorizeVisuals(VisualTreeHelper.GetChild(root, i), originals, originalShadows);
        }

        private static void PaintBrush(
            DependencyObject target,
            DependencyProperty property,
            Dictionary<(DependencyObject, DependencyProperty), Brush> originals)
        {
            if (target.GetValue(property) is not Brush brush) return;
            var key = (target, property);
            if (!originals.TryGetValue(key, out Brush? original))
            {
                original = brush;
                originals[key] = original;
            }

            if (original is SolidColorBrush solid)
            {
                if (!IsOriginalBlue(solid.Color)) return;
                Color newColor = IsDefault(selectedHex)
                    ? solid.Color : TintColor(solid.Color, selectedHue, false);
                target.SetValue(property, new SolidColorBrush(newColor)
                {
                    Opacity = solid.Opacity
                });
            }
            else if (original is GradientBrush gradient)
            {
                GradientBrush clone = (GradientBrush)gradient.CloneCurrentValue();
                foreach (GradientStop stop in clone.GradientStops)
                    if (!IsDefault(selectedHex))
                        stop.Color = TintColor(stop.Color, selectedHue, false);
                target.SetValue(property, clone);
            }
        }

        public static void CollectImages(
            DependencyObject root, Dictionary<Image, BitmapSource> originals)
        {
            if (root is Image image && image.Source is BitmapSource bitmap &&
                !originals.ContainsKey(image))
                originals[image] = bitmap;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                CollectImages(VisualTreeHelper.GetChild(root, i), originals);
        }

        // Запускаем перекраску на фоне и возвращаем готовые изображения.
        // Пока они считаются, старые изображения остаются видны без белой вспышки.
        public static async System.Threading.Tasks.Task<Dictionary<Image, BitmapSource>>
            PrepareImagesAsync(Dictionary<Image, BitmapSource> originals, string accent)
        {
            var tasks = new List<(Image image, System.Threading.Tasks.Task<BitmapSource> task)>();
            foreach (var pair in originals)
                tasks.Add((pair.Key, TintBitmapAsync(pair.Value, accent)));

            await System.Threading.Tasks.Task.WhenAll(tasks.ConvertAll(entry => entry.task));
            var result = new Dictionary<Image, BitmapSource>();
            foreach (var entry in tasks)
                result[entry.image] = await entry.task;
            return result;
        }

        // Меняем все готовые PNG подряд, без промежуточного пустого кадра.
        public static void ApplyImages(Dictionary<Image, BitmapSource> images)
        {
            foreach (var entry in images)
                entry.Key.Source = entry.Value;
        }

        public static System.Threading.Tasks.Task<BitmapSource> TintBitmapAsync(
            BitmapSource original, string accent)
        {
            if (IsDefault(accent))
                return System.Threading.Tasks.Task.FromResult(original);

            var key = (original, accent);
            if (cache.TryGetValue(key, out var existing))
                return existing;

            // Подготовка исходника только один раз, а не при каждой смене темы.
            if (!readySources.TryGetValue(original, out var ready))
            {
                var converted = new FormatConvertedBitmap(
                    original, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                ready = converted;
                readySources[original] = ready;
            }

            Color tint = (Color)ColorConverter.ConvertFromString(accent);
            ToHsv(tint, out double hue, out _, out _);

            var job = System.Threading.Tasks.Task.Run(async () =>
            {
                await tintGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    return TintBitmapInBackground(ready, hue);
                }
                finally
                {
                    tintGate.Release();
                }
            });

            // Оставляем обработанные изображения для повторных переключений.
            // Размер кэша ограничен: не держим бесконечно новые копии.
            if (cache.Count >= 64)
                cache.Clear();
            cache[key] = job;
            return job;
        }

        private static BitmapSource TintBitmapInBackground(
            BitmapSource rgba, double hue)
        {
            int stride = rgba.PixelWidth * 4;
            byte[] pixels = new byte[stride * rgba.PixelHeight];
            rgba.CopyPixels(pixels, stride, 0);

            // Таблица из 65536 готовых оттенков: считаем цвет один раз
            // для пары (максимальная, минимальная яркость), а не для
            // каждого из миллионов пикселей.
            uint[] colors = MakeTintTable(hue);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0) continue;
                int b = pixels[i];
                int g = pixels[i + 1];
                int r = pixels[i + 2];
                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));
                int delta = max - min;

                // Эквивалент проверок исходного голубого диапазона
                // в HSV, но без медленных преобразований HSV для
                // каждого пикселя.
                if (delta * 100 < max * 14) continue;
                bool bluePixel =
                    (b >= g && b > r && (r - g) * 20 <= 9 * delta) ||
                    (g > b && g >= r && (b - r) * 10 >= 9 * delta);
                if (!bluePixel) continue;

                uint rgb = colors[(max << 8) | min];
                pixels[i] = (byte)rgb;
                pixels[i + 1] = (byte)(rgb >> 8);
                pixels[i + 2] = (byte)(rgb >> 16);
            }

            BitmapSource result = BitmapSource.Create(
                rgba.PixelWidth, rgba.PixelHeight,
                rgba.DpiX, rgba.DpiY, PixelFormats.Bgra32,
                null, pixels, stride);
            result.Freeze();
            return result;
        }

        private static uint[] MakeTintTable(double hue)
        {
            uint[] table = new uint[65536];
            Color full = FromHsv(hue, 1, 1, 255);
            double hr = full.R / 255.0;
            double hg = full.G / 255.0;
            double hb = full.B / 255.0;

            for (int max = 1; max < 256; max++)
            {
                for (int min = 0; min <= max; min++)
                {
                    double value = Math.Min(max / 255.0 * 0.70, 0.72);
                    double saturation = Math.Min(
                        (max - min) / (double)max * 0.78, 0.74);
                    if (value < 0.28) saturation *= 0.78;

                    double dim = value * (1 - saturation);
                    double bright = value * saturation;
                    byte r = Byte(dim + bright * hr);
                    byte g = Byte(dim + bright * hg);
                    byte b = Byte(dim + bright * hb);
                    table[(max << 8) | min] =
                        (uint)(r << 16 | g << 8 | b);
                }
            }
            return table;
        }
    }

    // Отключает системный эффект полупрозрачного появления окон Windows.
    // Если ОС не поддерживает эту опцию, основная работа не нарушится.
    internal static class WindowTransitionHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int size);

        public static void DisableWindowsFade(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int disable = 1;
                // DWMWA_TRANSITIONS_FORCEDISABLED = 3
                DwmSetWindowAttribute(hwnd, 3, ref disable, sizeof(int));

                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 on recent Windows;
                // reduces the bright native caption flash of CoreWindow.
                int darkTitleBar = 1;
                DwmSetWindowAttribute(hwnd, 20, ref darkTitleBar, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
