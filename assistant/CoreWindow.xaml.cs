using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace assistant
{
    public partial class CoreWindow : Window
    {
        private readonly Stopwatch animationClock = Stopwatch.StartNew();
        private const int PointCount = 40;

        private ImageBrush textureBrush = null!;
        private ScaleTransform textureScale = null!;
        private TranslateTransform textureShift = null!;
        private RotateTransform textureRotate = null!;

        public CoreWindow()
        {
            InitializeComponent();

            textureBrush = (ImageBrush)CoreShape.Fill.Clone();
            CoreShape.Fill = textureBrush;

            textureScale = new ScaleTransform(1.1, 1.1, 0.5, 0.5);
            textureRotate = new RotateTransform(0, 0.5, 0.5);
            textureShift = new TranslateTransform();

            TransformGroup textureTransform = new TransformGroup();
            textureTransform.Children.Add(textureScale);
            textureTransform.Children.Add(textureRotate);
            textureTransform.Children.Add(textureShift);

            textureBrush.Transform = textureTransform;
        }

        private void CoreWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            UpdateCoreShape(0);
            CompositionTarget.Rendering += CoreWindow_Rendering;
        }

        private void CoreWindow_Closed(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= CoreWindow_Rendering;
        }

        private void CoreWindow_Rendering(object? sender, EventArgs e)
        {
            double time = animationClock.Elapsed.TotalSeconds;

            UpdateCoreShape(time);
            UpdateTexture(time);
        }

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

        private void Core_MouseLeftButtonUp(
            object? sender,
            MouseButtonEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}