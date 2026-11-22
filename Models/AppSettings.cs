using System.IO;
using System.Text.Json;

namespace SnowDesk.Models;

public class AppSettings
{
    public bool ShowTrees { get; set; } = true;
    public bool ShowStringLights { get; set; } = true;
    public bool ShowSanta { get; set; } = true;
    public bool AutoPauseFullscreen { get; set; } = true;
    public int TargetFlakeCount { get; set; } = 450;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnowDesk",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}