using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace ForcePaste
{
    public class AppSettings
    {
        public string HotkeyKey { get; set; } = "V";
        public string HotkeyModifiers { get; set; } = "Control, Alt";
        public double SpeedDelay { get; set; } = 5;
        public double RandomVariance { get; set; } = 0;
        public double FontSize { get; set; } = 13;
        public string Theme { get; set; } = "System";
        public double BallLeft { get; set; } = -1;
        public double BallTop { get; set; } = -1;
    }

    public static class SettingsService
    {
        private static readonly string ConfigDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config");
        private static readonly string FilePath = Path.Combine(ConfigDir, "settings.json");

        private static AppSettings _settings = new AppSettings();

        public static AppSettings Settings => _settings;

        /// <summary>
        /// 从文件加载设置，返回 AppSettings 实例
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
            return _settings;
        }

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                _settings = settings;
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        /// <summary>
        /// 无参保存（使用当前内存中的设置）
        /// </summary>
        public static void Save()
        {
            Save(_settings);
        }

        #region --- 快捷键字符串转换 ---

        /// <summary>
        /// Key 枚举转字符串（如 Key.V -> "V"）
        /// </summary>
        public static string KeyToString(Key key)
        {
            return key.ToString();
        }

        /// <summary>
        /// 字符串转 Key 枚举（如 "V" -> Key.V）
        /// </summary>
        public static Key StringToKey(string keyStr)
        {
            if (Enum.TryParse<Key>(keyStr, out var key))
                return key;
            return Key.V;
        }

        /// <summary>
        /// ModifierKeys 转字符串（如 "Control, Alt"）
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
        /// 字符串转 ModifierKeys（如 "Control, Alt" -> Control|Alt）
        /// </summary>
        public static ModifierKeys StringToModifiers(string modifiersStr)
        {
            if (string.IsNullOrEmpty(modifiersStr))
                return ModifierKeys.None;

            ModifierKeys result = ModifierKeys.None;
            var parts = modifiersStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (Enum.TryParse<ModifierKeys>(part.Trim(), out var mod))
                    result |= mod;
            }
            return result;
        }

        #endregion
    }
}
