using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ForcePaste
{
    public static class TrayManager
    {
        #region --- Win32 API ---

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const int NIM_ADD = 0;
        private const int NIM_MODIFY = 1;
        private const int NIM_DELETE = 2;
        private const int NIM_SETVERSION = 4;
        private const int NOTIFYICON_VERSION_4 = 4;

        private const int NIF_MESSAGE = 0x01;
        private const int NIF_ICON = 0x02;
        private const int NIF_TIP = 0x04;
        private const int NIF_STATE = 0x08;

        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 100;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private const int TPM_RETURNCMD = 0x0100;
        private const int TPM_LEFTBUTTON = 0x0000;
        private const int IDI_APPLICATION = 32512;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, int uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, int lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_DEFAULTSIZE = 0x0040;
        private const uint LR_SHARED = 0x8000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        private const int GCL_HICON = -14;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int uMsg, IntPtr wParam, IntPtr lParam);


        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        private const int DI_NORMAL = 0x0040;

        [DllImport("shell32.dll")]
        private static extern int ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, ref int lpiIcon);

        #endregion

        private static MainWindow? _mainWindow;
        private static SettingsWindow? _settingsWindow;
        private static IntPtr _mainHwnd;
        private static HwndSource? _hwndSource;
        private static bool _isInTray = false;

        // 初始化托盘功能（传入 MainWindow 和 SettingsWindow 的引用）
        public static void Initialize(MainWindow mainWindow, SettingsWindow settingsWindow)
        {
            _mainWindow = mainWindow;
            _settingsWindow = settingsWindow;

            // 获取 MainWindow 的 HWND
            var helper = new WindowInteropHelper(mainWindow);
            _mainHwnd = helper.Handle;

            // 用 HwndSource 监听消息
            _hwndSource = HwndSource.FromHwnd(_mainHwnd);
            _hwndSource?.AddHook(WndProc);
        }

        // 隐藏到托盘
        public static void HideToTray()
        {
            if (_isInTray) return;

            _mainWindow?.Hide();
            _settingsWindow?.Hide();

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _mainHwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = GetApplicationIcon(),
                szTip = "ForcePaste",
                uVersion = NOTIFYICON_VERSION_4
            };

            Shell_NotifyIcon(NIM_ADD, ref nid);
            Shell_NotifyIcon(NIM_SETVERSION, ref nid);

            _isInTray = true;
        }

        // 从托盘恢复
        public static void ShowFromTray()
        {
            if (!_isInTray) return;

            RemoveTrayIcon();
            _isInTray = false;

            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        }

        // 从程序自身提取图标（WPF 窗口图标通过 WPF 方式设置，GetClassLongPtr 可能返回 0）
        private static IntPtr GetApplicationIcon()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath))
            {
                IntPtr[] hIconLarge = new IntPtr[1];
                IntPtr[] hIconSmall = new IntPtr[1];
                uint count = ExtractIconEx(exePath, 0, hIconLarge, hIconSmall, 1);
                if (count > 0 && hIconSmall[0] != IntPtr.Zero)
                    return hIconSmall[0];
                if (count > 0 && hIconLarge[0] != IntPtr.Zero)
                    return hIconLarge[0];
            }
            return LoadIcon(IntPtr.Zero, IDI_APPLICATION);
        }

        // 清理托盘资源
        public static void Cleanup()
        {
            if (_isInTray)
            {
                RemoveTrayIcon();
                _isInTray = false;
            }
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }

        private static void RemoveTrayIcon()
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _mainHwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
        }

        // WndProc 钩子，拦截托盘消息
        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_TRAYICON)
                return IntPtr.Zero;

            int eventId = (int)(lParam.ToInt64() & 0xFFFF);

            switch (eventId)
            {
                case WM_LBUTTONDBLCLK:
                    ShowFromTray();
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                    ShowContextMenu();
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        // 显示右键菜单
        private static void ShowContextMenu()
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, 0x0000, 100, "恢复");
            AppendMenu(menu, 0x0800, 0, ""); // separator
            AppendMenu(menu, 0x0000, 101, "退出");

            GetCursorPos(out POINT pt);

            // 必须先 SetForegroundWindow，否则菜单不会自动关闭
            SetForegroundWindow(_mainHwnd);
            int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_LEFTBUTTON, pt.X, pt.Y, _mainHwnd, IntPtr.Zero);
            DestroyMenu(menu);

            switch (cmd)
            {
                case 100:
                    ShowFromTray();
                    break;
                case 101:
                    Application.Current.Shutdown();
                    break;
            }
        }
    }
}
