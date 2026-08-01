using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ForcePaste
{
    public enum AppTheme { Light, Dark, System }

    public static class ThemeManager
    {
        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
        public static event EventHandler? ThemeChanged;

        private static ResourceDictionary? _themeDict;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, uint options, int sam, out IntPtr phkResult);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string valueName, IntPtr reserved, out uint type, IntPtr data, ref uint dataSize);
        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private const int KEY_READ = 0x20019;

        public static void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            var effective = theme == AppTheme.System ? GetSystemTheme() : theme;
            ApplyTheme(effective);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Initialize()
        {
            var effective = CurrentTheme == AppTheme.System ? GetSystemTheme() : CurrentTheme;
            ApplyTheme(effective);
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
    }
}
