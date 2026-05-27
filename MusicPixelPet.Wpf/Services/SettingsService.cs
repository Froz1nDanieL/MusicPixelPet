using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicPixelPet",
        "settings.json");

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return Save(AppSettings.CreateDefault());
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _jsonOptions)
                ?? AppSettings.CreateDefault();
            settings.WindowBounds.Width = 300;
            settings.WindowBounds.Height = 360;
            return settings;
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    public AppSettings Save(AppSettings settings)
    {
        settings.WindowBounds.Width = 300;
        settings.WindowBounds.Height = 360;

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
        ApplyAutoLaunch(settings.AutoLaunch);
        SettingsChanged?.Invoke(this, settings);
        return settings;
    }

    public AppSettings UpdateWindowBounds(WindowBounds bounds)
    {
        var settings = Load();
        settings.WindowBounds.X = bounds.X;
        settings.WindowBounds.Y = bounds.Y;
        settings.WindowBounds.Width = 300;
        settings.WindowBounds.Height = 360;
        return Save(settings);
    }

    private static void ApplyAutoLaunch(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                return;
            }

            const string valueName = "MusicPixelPet";
            if (!enabled)
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
                return;
            }

            key.SetValue(valueName, $"\"{Environment.ProcessPath}\"");
        }
        catch
        {
        }
    }
}
