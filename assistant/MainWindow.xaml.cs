
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace assistant
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer waveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        private readonly Stopwatch waveClock = Stopwatch.StartNew();
        private readonly List<System.Windows.Shapes.Path> wavePaths = new();

        private enum UiLanguage
        {
            Russian,
            English,
            Estonian
        }

        private enum PageSection
        {
            None,
            Assistants,
            History,
            Settings,
            Profile,
            Jan
        }

        private static UiLanguage savedLanguage = UiLanguage.Russian;
        private static string savedAccent = "#5BCCFF";

        private UiLanguage language = savedLanguage;
        private PageSection currentSection = PageSection.None;

        private Popup? languagePopup;
        private Popup? themePopup;

        private string selectedAccent = savedAccent;

        private readonly Dictionary<
            (DependencyObject, DependencyProperty), Brush
        > originalBrushes = new();

        private readonly Dictionary<UIElement, Color>
            originalShadows = new();

        private readonly Dictionary<Image, BitmapSource>
            originalWaveImages = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private string T(
            string russian,
            string english,
            string estonian)
        {
            return language switch
            {
                UiLanguage.English => english,
                UiLanguage.Estonian => estonian,
                _ => russian
            };
        }

        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            var easing = new SineEase
            {
                EasingMode = EasingMode.EaseInOut
            };

            WaveShift.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(
                    -15, 15, TimeSpan.FromSeconds(30))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = easing
                });

            WaveShift.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(
                    -9, 9, TimeSpan.FromSeconds(37))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = easing
                });

            WaveSkew.BeginAnimation(
                SkewTransform.AngleYProperty,
                new DoubleAnimation(
                    -1.2, 1.2, TimeSpan.FromSeconds(44))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = easing
                });

            if (wavePaths.Count == 0)
            {
                for (int i = 0; i < 15; i++)
                {
                    var path = new System.Windows.Shapes.Path
                    {
                        Stroke = new SolidColorBrush(
                            Color.FromArgb(
                                (byte)(i % 4 == 0 ? 92 : 48),
                                35,
                                145,
                                255)),

                        StrokeThickness =
                            i % 4 == 0 ? 1.2 : 0.7,

                        StrokeLineJoin = PenLineJoin.Round,
                        IsHitTestVisible = false
                    };

                    WaveOverlay.Children.Add(path);
                    wavePaths.Add(path);
                }
            }

            AssistantTheme.CollectImages(
                this,
                originalWaveImages);

            waveTimer.Tick += WaveTimer_Tick;
            waveTimer.Start();

            UpdateLanguage();
            ApplyTheme(selectedAccent);
        }

        private void WaveTimer_Tick(
            object? sender,
            EventArgs e)
        {
            double width = WaveOverlay.ActualWidth;
            double height = WaveOverlay.ActualHeight;

            if (width < 1 || height < 1)
                return;

            double time = waveClock.Elapsed.TotalSeconds;

            for (int i = 0; i < wavePaths.Count; i++)
            {
                var geometry = new StreamGeometry();

                using (var ctx = geometry.Open())
                {
                    for (int step = 0; step <= 90; step++)
                    {
                        double t = step / 90.0;

                        double x =
                            width * (0.22 + t * 0.94);

                        double y =
                            height * (1.08 - 0.91 * t)
                            + height * (i - 7) * 0.009
                            + height * 0.075
                            * Math.Sin(t * 5.2 + i * 0.20)
                            + height * 0.014
                            * Math.Sin(
                                t * 12.0
                                - time * 0.22
                                + i * 0.44);

                        var point = new Point(x, y);

                        if (step == 0)
                        {
                            ctx.BeginFigure(
                                point,
                                false,
                                false);
                        }
                        else
                        {
                            ctx.LineTo(
                                point,
                                true,
                                false);
                        }
                    }
                }

                geometry.Freeze();
                wavePaths[i].Data = geometry;
            }
        }

        private void MainWindow_Closed(
            object? sender,
            EventArgs e)
        {
            waveTimer.Stop();
            waveTimer.Tick -= WaveTimer_Tick;

            if (languagePopup != null)
                languagePopup.IsOpen = false;

            if (themePopup != null)
                themePopup.IsOpen = false;
        }

        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed
                && WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximize()
        {
            WindowState =
                WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
        }

        private void ShowSection(PageSection section)
        {
            currentSection = section;

            AssistantChoices.Visibility =
                section == PageSection.Assistants
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            switch (section)
            {
                case PageSection.Assistants:
                    SectionTitle.Text = T(
                        "Ассистенты",
                        "Assistants",
                        "Assistendid");

                    SectionInfo.Text = T(
                        "Выбери Джарвиса или Яна.",
                        "Choose Jarvis or Jan.",
                        "Vali Jarvis või Jan.");
                    break;

                case PageSection.History:
                    SectionTitle.Text = T(
                        "История команд",
                        "Command history",
                        "Käskude ajalugu");

                    SectionInfo.Text = T(
                        "Здесь появятся прошлые команды после подключения ИИ.",
                        "Past commands will appear here after AI is connected.",
                        "Pärast tehisintellekti ühendamist kuvatakse siin varasemad käsud.");
                    break;

                case PageSection.Settings:
                    SectionTitle.Text = T(
                        "Настройки",
                        "Settings",
                        "Seaded");

                    SectionInfo.Text = T(
                        "Здесь будут память, доступ, микрофон и остальные настройки.",
                        "Memory, access, microphone and other settings will go here.",
                        "Siia tulevad mälu, ligipääsu, mikrofoni ja muud seaded.");
                    break;

                case PageSection.Profile:
                    SectionTitle.Text = T(
                        "Профиль",
                        "Profile",
                        "Profiil");

                    SectionInfo.Text = T(
                        "Аккаунт и вход добавим на следующем этапе.",
                        "Account and sign-in will be added later.",
                        "Konto ja sisselogimine lisatakse hiljem.");
                    break;

                case PageSection.Jan:
                    SectionTitle.Text = "Ян / Jan";

                    SectionInfo.Text = T(
                        "Голос и функции Яна подключим позднее.",
                        "Jan's voice and features will be connected later.",
                        "Jani hääl ja funktsioonid lisatakse hiljem.");
                    break;
            }

            SectionPanel.Visibility = Visibility.Visible;
        }

        private void Assistants_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection(PageSection.Assistants);
        }

        private void History_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection(PageSection.History);
        }

        private void Settings_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection(PageSection.Settings);
        }

        private void Profile_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection(PageSection.Profile);
        }

        private void HidePanel_Click(
            object sender,
            RoutedEventArgs e)
        {
            SectionPanel.Visibility = Visibility.Collapsed;
            currentSection = PageSection.None;
        }

        private void WaveArea_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenJarvis_Click(sender, e);
        }

        private void OpenJarvis_Click(
            object sender,
            RoutedEventArgs e)
        {
            var core = new CoreWindow();
            core.Show();
            Close();
        }

        private void ChooseJan_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSection(PageSection.Jan);
        }

        // ===== ВЫБОР ЯЗЫКА =====

        private void Language_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (themePopup != null)
                themePopup.IsOpen = false;

            bool wasOpen =
                languagePopup?.IsOpen == true;

            if (languagePopup != null)
                languagePopup.IsOpen = false;

            if (wasOpen)
                return;

            var panel = PopupPanel(230);
            var stack = (StackPanel)panel.Child;

            stack.Children.Add(
                PopupTitle(T(
                    "ЯЗЫК ИНТЕРФЕЙСА",
                    "INTERFACE LANGUAGE",
                    "LIIDESE KEEL")));

            stack.Children.Add(
                LanguageRow(
                    "Русский",
                    "RU",
                    UiLanguage.Russian));

            stack.Children.Add(
                LanguageRow(
                    "English",
                    "EN",
                    UiLanguage.English));

            stack.Children.Add(
                LanguageRow(
                    "Eesti",
                    "ET",
                    UiLanguage.Estonian));

            languagePopup = ShowPopup(
                LanguageButton,
                panel);
        }

        private Border LanguageRow(
            string label,
            string code,
            UiLanguage choice)
        {
            bool chosen = language == choice;

            var row = new Border
            {
                Background = Brush(
                    chosen ? "#173B6F" : "#0B1C35"),

                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(0, 5, 0, 0),
                Padding = new Thickness(10, 9, 10, 9),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(38)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition());

            var codeText = new TextBlock
            {
                Text = code,
                Foreground = Brush(selectedAccent),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = label,
                Foreground = Brush("#EDF5FF"),
                FontSize = 14,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            var marker = new TextBlock
            {
                Text = chosen ? "✓" : "",
                Foreground = Brush(selectedAccent),
                FontSize = 16,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

            Grid.SetColumn(codeText, 0);
            Grid.SetColumn(nameText, 1);
            Grid.SetColumn(marker, 2);

            grid.Children.Add(codeText);
            grid.Children.Add(nameText);
            grid.Children.Add(marker);

            row.Child = grid;

            row.MouseEnter += (_, _) =>
            {
                row.Background = Brush("#214778");
            };

            row.MouseLeave += (_, _) =>
            {
                row.Background = Brush(
                    chosen ? "#173B6F" : "#0B1C35");
            };

            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;

                SetLanguage(choice);

                if (languagePopup != null)
                    languagePopup.IsOpen = false;
            };

            return row;
        }

        private void SetLanguage(UiLanguage selected)
        {
            language = selected;
            savedLanguage = selected;

            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            AssistantsText.Text = T(
                "Ассистенты",
                "Assistants",
                "Assistendid");

            HistoryText.Text = T(
                "История команд",
                "Command history",
                "Käskude ajalugu");

            LanguageButton.ToolTip = T(
                "Язык",
                "Language",
                "Keel");

            ThemeButton.ToolTip = T(
                "Цветовая тема",
                "Color theme",
                "Värviteema");

            var choiceButtons =
                AssistantChoices.Children
                    .OfType<Button>()
                    .ToArray();

            if (choiceButtons.Length >= 2)
            {
                choiceButtons[0].Content = T(
                    "Джарвис — открыть ядро",
                    "Jarvis — open core",
                    "Jarvis — ava tuum");

                choiceButtons[1].Content = T(
                    "Ян — выбрать ассистента",
                    "Jan — choose assistant",
                    "Jan — vali assistent");
            }

            if (SectionPanel.Child is StackPanel content)
            {
                var close = content.Children
                    .OfType<Button>()
                    .FirstOrDefault();

                if (close != null)
                {
                    close.Content = T(
                        "Закрыть",
                        "Close",
                        "Sulge");
                }
            }

            if (currentSection != PageSection.None)
                ShowSection(currentSection);
        }

        // ===== ПАЛИТРА ТЕМ =====

        private void Theme_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (languagePopup != null)
                languagePopup.IsOpen = false;

            bool wasOpen =
                themePopup?.IsOpen == true;

            if (themePopup != null)
                themePopup.IsOpen = false;

            if (wasOpen)
                return;

            var panel = PopupPanel(232);
            var stack = (StackPanel)panel.Child;

            stack.Children.Add(
                PopupTitle(T(
                    "ЦВЕТОВАЯ ТЕМА",
                    "COLOR THEME",
                    "VÄRVITEEMA")));

            stack.Children.Add(
                new TextBlock
                {
                    Text = T(
                        "Выбери цвет оформления",
                        "Choose an interface color",
                        "Vali kujunduse värv"),

                    Foreground = Brush("#A7B9D3"),
                    FontSize = 11,
                    Margin =
                        new Thickness(0, 2, 0, 12)
                });

            string[] colors =
            {
                "#5BCCFF",
                "#AB88FF",
                "#4BDEDA",
                "#50D991",
                "#FF657F",
                "#FFB05C",
                "#E38AFF",
                "#E3C96D"
            };

            string[] names =
            {
                "Blue",
                "Violet",
                "Aqua",
                "Green",
                "Red",
                "Orange",
                "Pink",
                "Gold"
            };

            var grid = new Grid();

            for (int col = 0; col < 4; col++)
            {
                grid.ColumnDefinitions.Add(
                    new ColumnDefinition());
            }

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(48)
                });

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(48)
                });

            for (int i = 0; i < colors.Length; i++)
            {
                var item = ThemeSwatch(
                    colors[i],
                    names[i]);

                Grid.SetColumn(item, i % 4);
                Grid.SetRow(item, i / 4);

                grid.Children.Add(item);
            }

            stack.Children.Add(grid);

            themePopup = ShowPopup(
                ThemeButton,
                panel);
        }

        private Border ThemeSwatch(
            string color,
            string name)
        {
            bool chosen = selectedAccent.Equals(
                color,
                StringComparison.OrdinalIgnoreCase);

            var outer = new Border
            {
                Width = 39,
                Height = 39,
                CornerRadius = new CornerRadius(20),

                BorderThickness = new Thickness(
                    chosen ? 2 : 1),

                BorderBrush = Brush(
                    chosen ? "#FFFFFF" : "#435779"),

                Background = Brush("#071A34"),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Cursor = Cursors.Hand,
                ToolTip = name
            };

            outer.Child = new Ellipse
            {
                Width = 26,
                Height = 26,
                Fill = Brush(color),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

            outer.MouseEnter += (_, _) =>
            {
                outer.BorderBrush = Brush("#FFFFFF");
            };

            outer.MouseLeave += (_, _) =>
            {
                outer.BorderBrush = Brush(
                    chosen ? "#FFFFFF" : "#435779");
            };

            outer.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;

                ApplyTheme(color);

                if (themePopup != null)
                    themePopup.IsOpen = false;
            };

            return outer;
        }

        // ===== ОБЩАЯ ТЕМА ВСЕГО ДЖАРВИСА =====

        private void ApplyTheme(string accent)
        {
            selectedAccent = accent;
            savedAccent = accent;

            AssistantTheme.SelectColor(accent);

            AssistantTheme.ColorizeVisuals(
                this,
                originalBrushes,
                originalShadows);

            AssistantTheme.ColorizeImages(
                originalWaveImages);
        }

        // ===== ВСПЛЫВАЮЩИЕ МЕНЮ =====

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush(
                (Color)ColorConverter
                    .ConvertFromString(hex));
        }

        private Border PopupPanel(double width)
        {
            var panel = new Border
            {
                Width = width,
                Background = Brush("#F5081730"),
                BorderBrush = Brush(selectedAccent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(13),

                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.7,
                    BlurRadius = 22,
                    ShadowDepth = 5
                }
            };

            panel.Child = new StackPanel();

            return panel;
        }

        private static TextBlock PopupTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#F2F7FF"),
                Margin = new Thickness(3, 3, 0, 8)
            };
        }

        private static Popup ShowPopup(
            Button button,
            Border panel)
        {
            var popup = new Popup
            {
                PlacementTarget = button,
                Placement = PlacementMode.Left,

                HorizontalOffset = -12,
                VerticalOffset = -10,

                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = PopupAnimation.Fade,
                Child = panel
            };

            popup.IsOpen = true;

            return popup;
        }
    }
}
