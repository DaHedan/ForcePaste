using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ForcePaste
{
    public partial class FloatBallWindow : Window
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

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

        private PastePanelWindow? _sidePanelWindow;
        private bool _isPinned = false;
        private bool _isDragging = false;
        private bool _isMouseDown = false;
        private Point _dragStartPoint;
        private Point _windowStartPosition;
        private DateTime _mouseDownTime;
        private DispatcherTimer? _mouseLeaveTimer;
        private DispatcherTimer? _clickOutsideTimer;
        private Storyboard? _hoverEnterStoryboard;
        private Storyboard? _hoverLeaveStoryboard;
        private Storyboard? _pressedStoryboard;
        private Storyboard? _releasedStoryboard;

        private const int DragThreshold = 5;
        private const int ClickMaxDurationMs = 300;

        public event EventHandler? BallClicked;
        public event EventHandler? SpeedSettingsRequested;
        public event EventHandler? RandomSettingsRequested;
        public event EventHandler? CloseRequested;

        #endregion

        #region Properties

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
                _sidePanelWindow?.SetPinned(value);
                UpdateVisualState();
            }
        }

        #endregion

        public FloatBallWindow()
        {
            InitializeComponent();
            InitializeWindow();
            InitializeTimers();
            InitializeAnimations();
            AttachEventHandlers();
        }

        #region Initialization

        private void InitializeWindow()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);

            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            var screenWidth = workArea.Right - workArea.Left;
            var screenHeight = workArea.Bottom - workArea.Top;

            Left = workArea.Left + screenWidth - Width - 20;
            Top = workArea.Top + screenHeight / 2 - Height / 2;
            UseLayoutRounding = true;
        }

        private void InitializeTimers()
        {
            _mouseLeaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _mouseLeaveTimer.Tick += OnMouseLeaveTimerTick;

            _clickOutsideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _clickOutsideTimer.Tick += OnClickOutsideTimerTick;
        }

        private void InitializeAnimations()
        {
            _hoverEnterStoryboard = (Storyboard)FindResource("HoverEnterStoryboard");
            _hoverLeaveStoryboard = (Storyboard)FindResource("HoverLeaveStoryboard");
            _pressedStoryboard = (Storyboard)FindResource("PressedStoryboard");
            _releasedStoryboard = (Storyboard)FindResource("ReleasedStoryboard");

            Storyboard.SetTarget(_hoverEnterStoryboard, FloatBall);
            Storyboard.SetTarget(_hoverLeaveStoryboard, FloatBall);
            Storyboard.SetTarget(_pressedStoryboard, FloatBall);
            Storyboard.SetTarget(_releasedStoryboard, FloatBall);
        }

        private void AttachEventHandlers()
        {
            Loaded += OnLoaded;
            FloatBall.MouseEnter += OnBallMouseEnter;
            FloatBall.MouseLeave += OnBallMouseLeave;
            FloatBall.MouseLeftButtonDown += OnBallMouseLeftButtonDown;
            FloatBall.MouseLeftButtonUp += OnBallMouseLeftButtonUp;
            FloatBall.MouseMove += OnBallMouseMove;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureTopMost();
        }

        private void OnBallMouseEnter(object sender, MouseEventArgs e)
        {
            if (_isDragging) return;

            _hoverEnterStoryboard?.Begin();

            if (!IsPinned)
            {
                ShowSidePanel();
            }

            _mouseLeaveTimer?.Start();
        }

        private void OnBallMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging) return;

            _hoverLeaveStoryboard?.Begin();
        }

        private void OnMouseLeaveTimerTick(object? sender, EventArgs e)
        {
            if (_isDragging || IsPinned) return;

            if (!IsMouseOverElement(FloatBall) && !IsMouseOverSidePanel())
            {
                HideSidePanel();
                _mouseLeaveTimer?.Stop();
            }
        }

        private void OnBallMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _isDragging = false;
            _mouseDownTime = DateTime.Now;
            _dragStartPoint = e.GetPosition(this);
            _windowStartPosition = new Point(Left, Top);

            _pressedStoryboard?.Begin();
            FloatBall.CaptureMouse();
        }

        private void OnBallMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isMouseDown) return;

            _isMouseDown = false;
            FloatBall.ReleaseMouseCapture();

            _releasedStoryboard?.Begin();

            var mouseUpTime = DateTime.Now;
            var duration = (mouseUpTime - _mouseDownTime).TotalMilliseconds;
            var currentPosition = e.GetPosition(this);
            var totalOffset = currentPosition - _dragStartPoint;
            var distance = Math.Sqrt(totalOffset.X * totalOffset.X + totalOffset.Y * totalOffset.Y);

            if (!_isDragging && duration < ClickMaxDurationMs && distance < DragThreshold)
            {
                HandleBallClick();
            }

            _isDragging = false;
        }

        private void OnBallMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed)
            {
                _isDragging = false;
                _isMouseDown = false;
                return;
            }

            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;
            var distance = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y);

            if (!_isDragging && distance > DragThreshold)
            {
                _isDragging = true;
                if (IsPinned)
                {
                    IsPinned = false;
                    HideSidePanel();
                }
            }

            if (_isDragging)
            {
                Left = _windowStartPosition.X + offset.X;
                Top = _windowStartPosition.Y + offset.Y;
                ConstrainToScreen();

                if (_sidePanelWindow != null && _sidePanelWindow.IsVisible)
                {
                    PositionSidePanel();
                }
            }
        }

        private void OnClickOutsideTimerTick(object? sender, EventArgs e)
        {
            if (!IsPinned || _sidePanelWindow == null) return;

            if (_sidePanelWindow.IsVisible && !IsMouseOverSidePanel() && !IsMouseOverElement(FloatBall))
            {
                var foregroundWindow = GetForegroundWindow();
                var sidePanelHandle = new WindowInteropHelper(_sidePanelWindow).Handle;
                var floatBallHandle = new WindowInteropHelper(this).Handle;

                if (foregroundWindow != sidePanelHandle && foregroundWindow != floatBallHandle)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (!IsMouseOverSidePanel() && !IsMouseOverElement(FloatBall))
                        {
                            IsPinned = false;
                            HideSidePanel();
                        }
                    }, DispatcherPriority.Background);
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetSidePanelWindow(PastePanelWindow sidePanelWindow)
        {
            _sidePanelWindow = sidePanelWindow;
            _sidePanelWindow.SpeedSettingsRequested += (s, e) => SpeedSettingsRequested?.Invoke(this, EventArgs.Empty);
            _sidePanelWindow.RandomSettingsRequested += (s, e) => RandomSettingsRequested?.Invoke(this, EventArgs.Empty);
            _sidePanelWindow.CloseRequested += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ShowBall()
        {
            Show();
            EnsureTopMost();
            _clickOutsideTimer?.Start();
        }

        public void HideBall()
        {
            _clickOutsideTimer?.Stop();
            _mouseLeaveTimer?.Stop();
            Hide();
        }

        #endregion

        #region Private Methods

        private void HandleBallClick()
        {
            BallClicked?.Invoke(this, EventArgs.Empty);

            if (IsPinned)
            {
                IsPinned = false;
                HideSidePanel();
            }
            else
            {
                IsPinned = true;
                ShowSidePanel();
            }
        }

        private void ShowSidePanel()
        {
            if (_sidePanelWindow == null) return;

            Dispatcher.BeginInvoke(() =>
            {
                PositionSidePanel();
                _sidePanelWindow.ShowPanel();

            }, DispatcherPriority.Background);
        }

        private void HideSidePanel()
        {
            if (_sidePanelWindow == null) return;

            Dispatcher.BeginInvoke(() =>
            {
                _sidePanelWindow.HidePanel();
            }, DispatcherPriority.Background);
        }

        public void PositionSidePanel()
        {
            _sidePanelWindow?.PositionRelativeToBall();
        }

        private void UpdateVisualState()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (IsPinned)
                {
                    FloatBall.Background = (SolidColorBrush)FindResource("BallPinnedColor");
                    PinIndicator.Visibility = Visibility.Visible;
                    IconText.Text = "\xE141";
                }
                else
                {
                    FloatBall.Background = (SolidColorBrush)FindResource("BallNormalColor");
                    PinIndicator.Visibility = Visibility.Collapsed;
                    IconText.Text = "\xE16D";
                }
            }, DispatcherPriority.Render);
        }

        private void EnsureTopMost()
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private void ConstrainToScreen()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);

            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;

            Left = Math.Max(workArea.Left - Width + 20, Math.Min(Left, workArea.Right - 20));
            Top = Math.Max(workArea.Top - Height + 20, Math.Min(Top, workArea.Bottom - 20));
        }

        private bool IsMouseOverElement(FrameworkElement element)
        {
            var mousePos = Mouse.GetPosition(element);
            return mousePos.X >= 0 && mousePos.X <= element.ActualWidth &&
                   mousePos.Y >= 0 && mousePos.Y <= element.ActualHeight;
        }

        private bool IsMouseOverSidePanel()
        {
            if (_sidePanelWindow == null || !_sidePanelWindow.IsVisible) return false;

            GetCursorPos(out var cursorPos);
            var sidePanelRect = new Rect(_sidePanelWindow.Left, _sidePanelWindow.Top, _sidePanelWindow.Width, _sidePanelWindow.Height);
            return sidePanelRect.Contains(cursorPos.X, cursorPos.Y);
        }

        #endregion

        #region Overrides

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _mouseLeaveTimer?.Stop();
            _clickOutsideTimer?.Stop();
            base.OnClosing(e);
        }

        #endregion
    }
}
