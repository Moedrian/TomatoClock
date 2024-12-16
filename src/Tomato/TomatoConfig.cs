using System.IO;
using System.Text.Json;

namespace Tomato;

[Serializable]
public sealed class TomatoConfig
{
    private static readonly JsonSerializerOptions CfgJso = new() { WriteIndented = true };

    public int Interval { get; set; } = 45;
    public int OffTimeHour { get; set; } = 18;
    public int OffTimeMinute { get; set; } = 0;

    private static string GetUserConfigFile()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tomato.config.json");

    public static void Create()
    {
        var f = GetUserConfigFile();
        if (!File.Exists(f)) Serialize(new TomatoConfig());
    }

    public static void Serialize(TomatoConfig cfg)
    {
        var file = GetUserConfigFile();
        var json = JsonSerializer.Serialize(cfg, CfgJso);
        File.WriteAllText(file, json);
    }

    public static TomatoConfig Deserialize()
    {
        var file = GetUserConfigFile();
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<TomatoConfig>(json) ??
               throw new Exception("null content error in the config file.");
    }
}