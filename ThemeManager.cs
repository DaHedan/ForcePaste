using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace ForcePaste
{
    public enum AppTheme { Light, Dark, System }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Dark;
        public static AppTheme CurrentTheme
        {
            get => _currentTheme;
            private set => _currentTheme = value;
        }

        public static event EventHandler? ThemeChanged;

        private static ResourceDictionary? _themeDict;

        // 持久化文件路径
        private static readonly string ThemeFilePath;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, uint options, int sam, out IntPtr phkResult);
        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private const int KEY_READ = 0x20019;

        static ThemeManager()
        {
            // 使用程序可执行文件所在目录，实现便携化
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = Path.Combine(appDir, "config");
            ThemeFilePath = Path.Combine(dir, "theme.json");
        }

        /// <summary>
        /// 从持久化文件加载保存的主题，若无则返回默认 Dark。
        /// </summary>
        public static AppTheme LoadSavedTheme()
        {
            try
            {
                if (File.Exists(ThemeFilePath))
                {
                    var json = File.ReadAllText(ThemeFilePath);
                    var saved = JsonSerializer.Deserialize<ThemeSaveData>(json);
                    if (saved != null && Enum.TryParse<AppTheme>(saved.Theme, out var theme))
                        return theme;
                }
            }
            catch { }
            return AppTheme.Dark;
        }

        public static void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            var effective = theme == AppTheme.System ? GetSystemTheme() : theme;
            ApplyTheme(effective);
            SaveTheme(theme);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Initialize()
        {
            // 从持久化文件加载主题
            var savedTheme = LoadSavedTheme();
            CurrentTheme = savedTheme;
            var effective = savedTheme == AppTheme.System ? GetSystemTheme() : savedTheme;
            ApplyTheme(effective);
        }

        private static void SaveTheme(AppTheme theme)
        {
            try
            {
                var dir = Path.GetDirectoryName(ThemeFilePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new ThemeSaveData { Theme = theme.ToString() };
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(ThemeFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存主题失败: {ex.Message}");
            }
        }

        private static AppTheme GetSystemTheme()
        {
            try
            {
                IntPtr hKey;
                if (RegOpenKeyEx(HKEY_CURRENT_USER,
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    0, KEY_READ, out hKey) == 0)
                {
                    try
                    {
                        uint type = 0;
                        uint size = 4;
                        IntPtr data = Marshal.AllocHGlobal(4);
                        try
                        {
                            if (RegQueryValueEx(hKey, "AppsUseLightTheme", IntPtr.Zero, out type, data, ref size) == 0)
                            {
                                int val = Marshal.ReadInt32(data);
                                return val == 0 ? AppTheme.Dark : AppTheme.Light;
                            }
                        }
                        finally { Marshal.FreeHGlobal(data); }
                    }
                    finally { RegCloseKey(hKey); }
                }
            }
            catch { }
            return AppTheme.Light;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, IntPtr lpReserved,
            out uint lpType, IntPtr lpData, ref uint lpcbData);

        private static void ApplyTheme(AppTheme effective)
        {
            if (Application.Current == null) return;
            string uri = effective == AppTheme.Dark
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";
            var res = Application.Current.Resources;
            // 移除旧主题字典
            if (_themeDict != null)
            {
                res.MergedDictionaries.Remove(_themeDict);
            }
            // 加载新主题字典
            _themeDict = new ResourceDictionary { Source = new Uri(uri) };
            res.MergedDictionaries.Add(_themeDict);
        }

        public static void Cleanup() { }

        private class ThemeSaveData
        {
            public string Theme { get; set; } = "Dark";
        }
    }
}
