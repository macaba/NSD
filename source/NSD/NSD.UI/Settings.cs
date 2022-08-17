using System;
using System.IO;
using System.Text.Json;

namespace NSD.UI
{
    public class Settings
    {
        public string? ProcessWorkingFolder { get; set; }
        //public string? CollateWorkingFolder { get; set; }
        public string? SampleRate { get; set; }

        public static Settings Default()
        {
            return new Settings()
            {
                ProcessWorkingFolder = Directory.GetCurrentDirectory(),
                //CollateWorkingFolder = Directory.GetCurrentDirectory(),
                SampleRate = "50"
            };
        }

        public static Settings Load()
        {
            if (!File.Exists("settings.json"))
                return Default();
            var json = File.ReadAllText("settings.json");
            if (string.IsNullOrWhiteSpace(json))
                return Default();
            var settings = JsonSerializer.Deserialize<Settings>(json);
            if (settings != null)
                return settings;
            else
                return Default();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText("settings.json", json);
        }
    }
}
