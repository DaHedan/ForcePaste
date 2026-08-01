using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace ForcePaste
{
    /// <summary>
    /// 应用设置持久化服务。
    /// 设置保存为 JSON 文件：%APPDATA%/ForcePaste/settings.json
    /// </summary>
    public class AppSettings
    {
        // 快捷键
        public string HotkeyKey { get; set; } = "V";
        public string HotkeyModifiers { get; set; } = "Control, Alt";

        // 粘贴设置
        public double SpeedDelay { get; set; } = 30;
        public double RandomVariance { get; set; } = 10;

        // 字体大小
        public double FontSize { get; set; } = 13;

        // 主题
        public string Theme { get; set; } = "Dark";
    }

    public static class SettingsService
    {
        private static readonly string SettingsDir;
        private static readonly string SettingsFilePath;
        private static AppSettings _cached;

        static SettingsService()
        {
            SettingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ForcePaste");
            SettingsFilePath = Path.Combine(SettingsDir, "settings.json");
        }

        /// <summary>
        /// 加载设置。文件不存在或解析失败时返回默认值。
        /// </summary>
        public static AppSettings Load()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        _cached = settings;
                        return _cached;
                    }
                }
            }
            catch
            {
                // 解析失败，使用默认值
            }

            _cached = new AppSettings();
            return _cached;
        }

        /// <summary>
        /// 保存设置到 JSON 文件。
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
                _cached = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前设置（已缓存），如果未加载则先 Load。
        /// </summary>
        public static AppSettings Current => _cached ?? Load();

        /// <summary>
        /// 将 Key 枚举名转为可存储的字符串。
        /// </summary>
        public static string KeyToString(Key key) => key.ToString();

        /// <summary>
        /// 将存储的字符串还原为 Key 枚举。
        /// </summary>
        public static Key StringToKey(string s)
        {
            if (Enum.TryParse<Key>(s, out var key)) return key;
            return Key.V;
        }

        /// <summary>
        /// 将 ModifierKeys 转为逗号分隔的字符串存储。
        /// </summary>
        public static string ModifiersToString(ModifierKeys modifiers)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Control");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Windows");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 将逗号分隔的字符串还原为 ModifierKeys。
        /// </summary>
        public static ModifierKeys StringToModifiers(string s)
        {
            if (string.IsNullOrEmpty(s)) return ModifierKeys.None;
            var result = ModifierKeys.None;
            foreach (var part in s.Split(','))
            {
                var trimmed = part.Trim();
                if (Enum.TryParse<ModifierKeys>(trimmed, out var mod))
                    result |= mod;
            }
            return result;
        }
    }
}
