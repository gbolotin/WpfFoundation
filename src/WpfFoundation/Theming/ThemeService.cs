using System.Windows;

namespace WpfFoundation.Theming;

/// <summary>The color theme an application uses.</summary>
public enum ThemePreference
{
    /// <summary>Follows the Windows light or dark setting.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark
}

/// <summary>Applies the application's Fluent color theme.</summary>
public interface IThemeService
{
    /// <summary>The theme applied last.</summary>
    ThemePreference CurrentTheme { get; }

    /// <summary>Applies a theme to every window of the application immediately.</summary>
    void ApplyTheme(ThemePreference theme);
}

/// <summary>
/// Applies themes through WPF's Fluent <see cref="Application.ThemeMode"/>, which also follows Windows high contrast.
/// </summary>
public sealed class ThemeService(Application application) : IThemeService
{
    private ThemePreference currentTheme = ThemePreference.System;

    /// <inheritdoc />
    public ThemePreference CurrentTheme => currentTheme;

    /// <inheritdoc />
    public void ApplyTheme(ThemePreference theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown theme.");
        }

        currentTheme = theme;
        if (application.Dispatcher.CheckAccess())
        {
            application.ThemeMode = ToThemeMode(theme);
        }
        else
        {
            application.Dispatcher.Invoke(() => application.ThemeMode = ToThemeMode(theme));
        }
    }

    private static ThemeMode ToThemeMode(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => ThemeMode.Light,
        ThemePreference.Dark => ThemeMode.Dark,
        _ => ThemeMode.System
    };
}
