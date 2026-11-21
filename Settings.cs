using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopSnow
{
    internal class Settings
    {
        public bool SnowEnabled { get; set; } = true;
        public int FlakeCount { get; set; } = 450;
        public double FallSpeed { get; set; } = 1.0;
        public double FlakeScale { get; set; } = 1.0;
        public double WindStrength { get; set; } = 25;
        public bool Gusts { get; set; } = true;
        public bool Accumulate { get; set; } = true;
        public double AccumRate { get; set; } = 1.4;
        public double MaxWindowSnow { get; set; } = 30;
        public double MaxTaskbarSnow { get; set; } = 60;
        public bool BottomSnow { get; set; } = true;
        public bool Melt { get; set; } = false;

        public bool PlowEnabled { get; set; } = true;
        public double PlowThreshold { get; set; } = 22;
        public double PlowCooldown { get; set; } = 90;
        public double PlowSpeed { get; set; } = 1.0;
        public bool PlowFromLeft { get; set; } = true;

        public bool BallsEnabled { get; set; } = true;
        public double MinSnowForBall { get; set; } = 6;
        public int BallModifier { get; set; } = 0;   // 0 нет, 1 Alt, 2 Ctrl, 3 Shift
        public bool BallDrag { get; set; } = true;
        public int MaxBalls { get; set; } = 30;
        public bool AutoSnowman { get; set; } = true;
        public int HatStyle { get; set; } = 2;       // 0 Санта, 1 эльф, 2 случайная

        public bool GarlandEnabled { get; set; } = true;
        public bool GarlandWindows { get; set; } = true;
        public bool GarlandTaskbar { get; set; } = true;
        public double BulbSpacing { get; set; } = 26;
        public int GarlandMode { get; set; } = 0;    // 0 волна, 1 мерцание, 2 мигание, 3 статично
        public double GarlandSpeed { get; set; } = 1.0;
        public double GarlandBrightness { get; set; } = 1.0;

        public int Fps { get; set; } = 60;
        public int WindowPollHz { get; set; } = 30;
        public bool PauseOnMaximized { get; set; } = true;
        public bool PauseFullscreen { get; set; } = true;
        public bool PauseOnBattery { get; set; } = false;
        public bool Autostart { get; set; } = false;
        public bool Paused { get; set; } = false;

        [JsonIgnore] public bool Dirty;
        public void Touch() { Dirty = true; }

        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopSnow");
        static string FilePath => Path.Combine(Dir, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                    if (s != null) { s.Paused = false; return s; }
                }
            }
            catch { }
            return new Settings();
        }

        public void Flush()
        {
            if (!Dirty) return;
            Dirty = false;
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void Reset()
        {
            var fresh = new Settings();
            foreach (var p in typeof(Settings).GetProperties())
                if (p.CanWrite && p.Name != "Dirty") p.SetValue(this, p.GetValue(fresh));
            Touch();
        }
    }
}