using System.IO;
using System.Text.Json;
using Application = System.Windows.Application;

namespace Tomato;

[Serializable]
public sealed class TomatoConfig
{
    private static readonly JsonSerializerOptions CfgJso = new() { WriteIndented = true };

    public int Interval { get; set; } = 45;
    public int OffTimeHour { get; set; } = 18;
    public int OffTimeMinute { get; set; } = 0;

    private static string GetUserDirectory()
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string GetUserConfigFile()
        => Path.Combine(GetUserDirectory(), ".tomato.config.json");

    private const string TomatoPicture = "Tomato_je.jpg";

    public static string GetTomatoPicture()
        => Path.Combine(GetUserDirectory(), "Tomato_je.jpg");

    public static void Create()
    {
        var f = GetUserConfigFile();
        if (!File.Exists(f))
            Serialize(new TomatoConfig());

        if (!File.Exists(GetTomatoPicture()))
        {
            var uri = new Uri($"pack://application:,,,/{TomatoPicture}", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo is not null)
            {
                using var ms = new MemoryStream();
                streamInfo.Stream.CopyTo(ms);
                File.WriteAllBytes(GetTomatoPicture(), ms.ToArray());
            }
        }
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