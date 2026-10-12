using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace assistant
{
    internal static class ThemeAssets
    {
        private static readonly Dictionary<string, BitmapSource> Cache = new();

        public static BitmapSource Load(string color, string file)
        {
            string folder = color.ToUpperInvariant() switch
            {
                "#A88BD9" => "Violet",
                "#65BFC0" => "Aqua",
                "#72C39B" => "Green",
                "#D88393" => "Red",
                "#D6A17C" => "Orange",
                "#CA98C5" => "Pink",
                "#C7B279" => "Gold",
                _ => ""
            };
            string path = string.IsNullOrEmpty(folder) ? "/" + file : "/Themes/" + folder + "/" + file;
            if (Cache.TryGetValue(path, out var saved)) return saved;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri("pack://application:,,," + path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            Cache.Add(path, bitmap);
            return bitmap;
        }
    }
}
