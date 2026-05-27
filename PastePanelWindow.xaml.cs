using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ForcePaste
{
    public partial class PastePanelWindow : Window
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion

        #region Fields

        private Window? _floatBallWindow;
        private SettingsWindow _settingsWindow;
        private Storyboard? _showStoryboard;
        private Storyboard? _hideStoryboard;
        private DispatcherTimer? _mouseLeaveTimer;
        private DispatcherTimer? _hideDelayTimer;
        private bool _isHiding = false;
        private bool _isPinned = false;

        public event EventHandler? CloseRequested;
        public event EventHandler? SpeedSettingsRequested;
        public event EventHandler? RandomSettingsRequested;
        public event EventHandler? PasteRequested;

        #endregion

        public PastePanelWindow(SettingsWindow settingsWindow)
        {
            _settingsWindow = settingsWindow;
            InitializeComponent();
            InitializeWindow();
            InitializeAnimations();
            InitializeTimers();
            AttachEventHandlers();
        }

        #region Initialization

        private void InitializeWindow()
        {
            UseLayoutRounding = true;
        }

        private void InitializeAnimations()
        {
            _showStoryboard = (Storyboard)FindResource("ShowStoryboard");
            _hideStoryboard = (Storyboard)FindResource("HideStoryboard");

            if (_hideStoryboard != null)
            {
                _hideStoryboard.Completed += OnHideStoryboardCompleted;
            }

            Storyboard.SetTarget(_showStoryboard, PanelBorder);
            Storyboard.SetTarget(_hideStoryboard, PanelBorder);
        }

        private void InitializeTimers()
        {
            _mouseLeaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _mouseLeaveTimer.Tick += OnMouseLeaveTimerTick;

            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _hideDelayTimer.Tick += OnHideDelayTimerTick;
        }

        private void AttachEventHandlers()
        {
            Loaded += OnLoaded;
            MouseEnter += OnPanelMouseEnter;
            MouseLeave += OnPanelMouseLeave;

            PasteButton.Click += (s, e) => PasteRequested?.Invoke(this, EventArgs.Empty);
            SpeedButton.Click += (s, e) => SpeedSettingsRequested?.Invoke(this, EventArgs.Empty);
            RandomButton.Click += (s, e) => RandomSettingsRequested?.Invoke(this, EventArgs.Empty);
            HotkeyButton.Click += (s, e) => ShowHotkeyInfo();
            HelpButton.Click += (s, e) => ShowHelp();
            CloseButton.Click += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureTopMost();
        }

        private void OnPanelMouseEnter(object sender, MouseEventArgs e)
        {
            _mouseLeaveTimer?.Stop();
            if (_hideDelayTimer != null && _hideDelayTimer.IsEnabled)
            {
                _hideDelayTimer.Stop();
            }
        }

        private void OnPanelMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isPinned)
            {
                _mouseLeaveTimer?.Start();
            }
        }

        private void OnMouseLeaveTimerTick(object? sender, EventArgs e)
        {
            if (!IsMouseOver && !_isPinned)
            {
                _mouseLeaveTimer?.Stop();
                HidePanel();
            }
        }

        private void OnHideStoryboardCompleted(object? sender, EventArgs e)
        {
            if (_isHiding)
            {
                Hide();
                _isHiding = false;
            }
        }

        private void OnHideDelayTimerTick(object? sender, EventArgs e)
        {
            _hideDelayTimer?.Stop();
            HidePanelInternal();
        }

        #endregion

        #region Public Methods

        public void SetFloatBallWindow(Window floatBallWindow)
        {
            _floatBallWindow = floatBallWindow;
        }

        public void SetPinned(bool pinned)
        {
            _isPinned = pinned;
            if (!pinned)
            {
                _mouseLeaveTimer?.Start();
            }
        }

        public void ShowPanel()
        {
            _isHiding = false;

            Dispatcher.BeginInvoke(() =>
            {
                EnsureTopMost();
                Visibility = Visibility.Visible;
                Opacity = 0;

                _showStoryboard?.Begin();

                _mouseLeaveTimer?.Start();
            }, DispatcherPriority.Render);
        }

        public void HidePanel()
        {
            _mouseLeaveTimer?.Stop();

            if (_hideDelayTimer != null && !_hideDelayTimer.IsEnabled)
            {
                _hideDelayTimer.Start();
            }
        }

        private void HidePanelInternal()
        {
            _mouseLeaveTimer?.Stop();
            _isHiding = true;

            _hideStoryboard?.Begin();
        }

        public void PositionRelativeToBall()
        {
            if (_floatBallWindow == null) return;

            var ballHandle = new WindowInteropHelper(_floatBallWindow).Handle;
            var monitor = MonitorFromWindow(ballHandle, MONITOR_DEFAULTTONEAREST);

            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            var screenWidth = workArea.Right - workArea.Left;
            var screenHeight = workArea.Bottom - workArea.Top;
            var screenLeft = workArea.Left;
            var screenTop = workArea.Top;

            var ballCenterY = _floatBallWindow.Top + _floatBallWindow.Height / 2;
            var ballRight = _floatBallWindow.Left + _floatBallWindow.Width;
            var ballLeft = _floatBallWindow.Left;

            var panelWidth = Width;
            var panelHeight = Height;

            double panelLeft;
            double panelTop;

            // 判断悬浮球在屏幕工作区左侧还是右侧，面板显示在相反方向
            if (ballLeft + _floatBallWindow.Width / 2 > screenLeft + screenWidth / 2)
            {
                // 悬浮球在屏幕右侧，面板显示在左侧
                panelLeft = ballLeft - panelWidth - 8;
            }
            else
            {
                // 悬浮球在屏幕左侧，面板显示在右侧
                panelLeft = ballRight + 8;
            }

            // 边界检查：如果左侧空间不足，显示在右侧
            if (panelLeft < screenLeft + 10)
            {
                panelLeft = ballRight + 8;
            }
            // 边界检查：如果右侧超出屏幕工作区，显示在左侧
            if (panelLeft + panelWidth > screenLeft + screenWidth - 10)
            {
                panelLeft = ballLeft - panelWidth - 8;
            }

            panelTop = ballCenterY - panelHeight / 2;

            if (panelTop < screenTop + 10)
                panelTop = screenTop + 10;
            if (panelTop + panelHeight > screenTop + screenHeight - 10)
                panelTop = screenTop + screenHeight - panelHeight - 10;

            Left = panelLeft;
            Top = panelTop;
        }

        #endregion

        #region Private Methods

        public void EnsureTopMost()
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private void ShowHotkeyInfo()
        {
            MessageBox.Show("全局热键: Ctrl + Alt + V\n\n在任何窗口复制文本后，按下此热键即可触发模拟输入。",
                "热键提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowHelp()
        {
            MessageBox.Show("ForcePaste 使用帮助:\n\n" +
                "1. 复制需要粘贴的文本\n" +
                "2. 将光标定位到目标输入框\n" +
                "3. 按下 Ctrl + Alt + V 触发模拟输入\n\n" +
                "提示: 可通过悬浮球或侧边栏调整输入速度和随机波动范围。",
                "使用帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Overrides

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _mouseLeaveTimer?.Stop();
            _hideDelayTimer?.Stop();
            base.OnClosing(e);
        }

        #endregion
    }
}
