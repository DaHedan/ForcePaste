using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ForcePaste
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Global\\ForcePaste_SingleInstance";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // 已有实例在运行，尝试激活已有窗口后退出
                TryActivateExistingInstance();
                Shutdown();
                return;
            }

            // 先加载主题资源，再创建窗口（这样 DynamicResource 能正确解析）
            ThemeManager.Initialize();
            base.OnStartup(e);
        }

        private static void TryActivateExistingInstance()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_SHOW);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            catch
            {
                // 激活失败时静默忽略，直接退出即可
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ThemeManager.Cleanup();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
