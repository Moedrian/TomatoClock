using System;
using System.IO;
using System.Xml.Serialization;

namespace Tomato
{
    [Serializable]
    public sealed class TomatoConfig
    {
        private static XmlSerializer _cfgSerializer = new XmlSerializer(typeof(TomatoConfig));

        public int Interval { get; set; } = 45;
        public int OffTimeHour { get; set; } = 18;
        public int OffTimeMinute { get; set; } = 0;

        private static string GetUserConfigFile()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tomato.config.xml");

        public static void Create()
        {
            var f = GetUserConfigFile();
            if (!File.Exists(f)) Serialize(new TomatoConfig());
        }

        public static void Serialize(TomatoConfig cfg)
        {
            var file = GetUserConfigFile();
            using (var sw = new StreamWriter(file))
                _cfgSerializer.Serialize(sw, cfg);
        }

        public static TomatoConfig Deserialize()
        {
            var file = GetUserConfigFile();
            using (var reader = new StreamReader(file))
                return (TomatoConfig)_cfgSerializer.Deserialize(reader);
        }
    }
}
