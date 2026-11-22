using System.IO;
using System.Text.Json;

namespace SnowDesk.Models;

public class AppSettings
{
    public int TargetFlakeCount { get; set; } = 450;
    public bool ShowStringLights { get; set; } = true;
    public bool ShowTrees { get; set; } = true;
    public bool ShowSanta { get; set; } = true;
    public bool ShowIcicles { get; set; } = true;
    public bool ShowCursorTrail { get; set; } = true;
    public bool AutoPauseFullscreen { get; set; } = false;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnowDesk", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
