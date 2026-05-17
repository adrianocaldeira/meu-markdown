using System;
using System.Linq;
using System.Windows;

namespace MeuMarkdown.Themes;

public enum AppTheme { Dark, Light }

public static class ThemeManager
{
    private const string DarkUri = "/Themes/Theme.Dark.xaml";
    private const string LightUri = "/Themes/Theme.Light.xaml";

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void Apply(AppTheme theme)
    {
        var uri = theme == AppTheme.Dark ? DarkUri : LightUri;
        var newDict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove o dicionário de tema anterior (Theme.Dark.xaml ou Theme.Light.xaml).
        // Mantém Theme.Common.xaml e Icons.xaml.
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString;
            if (src != null && (src.EndsWith("Theme.Dark.xaml", StringComparison.OrdinalIgnoreCase)
                             || src.EndsWith("Theme.Light.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Add(newDict);
        Current = theme;
    }
}
