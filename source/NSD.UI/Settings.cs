using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSD.UI
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Settings))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }

    public class Settings
    {
        public string? ProcessWorkingFolder { get; set; }
        public string? AcquisitionTime { get; set; }
        public string? AcquisitionTimeUnit { get; set; }
        public string? DataRate { get; set; }
        public string? DataRateUnit { get; set; }

        public static Settings Default()
        {
            return new Settings()
            {
                ProcessWorkingFolder = Directory.GetCurrentDirectory(),
                AcquisitionTime = "1",
                AcquisitionTimeUnit = "NPLC",
                DataRate = "50",
                DataRateUnit = "Samples per second"
            };
        }

        public static Settings Load()
        {
            if (!File.Exists("settings.json"))
                return Default();
            var json = File.ReadAllText("settings.json");
            if (json.Contains("SampleRate"))
                return Default();   // Ignore old settings file
            if (string.IsNullOrWhiteSpace(json))
                return Default();
            var settings = JsonSerializer.Deserialize<Settings>(json, SourceGenerationContext.Default.Settings);
            if (settings != null)
                return settings;
            else
                return Default();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, SourceGenerationContext.Default.Settings);
            File.WriteAllText("settings.json", json);
        }
    }
}
