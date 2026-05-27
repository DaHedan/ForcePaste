using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using NHotkey.Wpf;
using NHotkey;

namespace ForcePaste
{
    public partial class MainWindow : Window
    {
        private SettingsWindow _settingsWin;
        private bool _isTyping = false;
        private bool _isPinned = false;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public MainWindow()
        {
            InitializeComponent();
            _settingsWin = new SettingsWindow();

            // 初始化位置（屏幕右上角附近）
            this.Left = SystemParameters.WorkArea.Right - 100;
            this.Top = 100;

            RegisterHotkeys();

            // 点击外部时取消固定 SettingsWindow
            _settingsWin.Deactivated += SettingsWin_Deactivated;
        }

        private void RegisterHotkeys()
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("StartPaste", Key.V, ModifierKeys.Control | ModifierKeys.Alt, OnStartPaste);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册失败: {ex.Message}");
            }
        }

        #region --- 模拟输入逻辑 ---

        private async void OnStartPaste(object? sender, HotkeyEventArgs e)
        {
            if (_isTyping) return;
            e.Handled = true;

            string clipText = string.Empty;
            if (Clipboard.ContainsText()) clipText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipText)) return;

            int delay = _settingsWin.BaseDelay;
            int random = _settingsWin.RandomVariance;
            _isTyping = true;

            try
            {
                await WaitForModifiersReleaseAsync();
                await InputHelper.SimulateTextTypingAsync(clipText, delay, random);
            }
            finally
            {
                _isTyping = false;
            }
        }

        private async Task WaitForModifiersReleaseAsync()
        {
            int[] modifiers = { 0x10, 0x11, 0x12, 0x5B, 0x5C };
            bool isAnyKeyDown;
            do
            {
                isAnyKeyDown = false;
                foreach (var key in modifiers)
                {
                    if ((GetAsyncKeyState(key) & 0x8000) != 0)
                    {
                        isAnyKeyDown = true;
                        break;
                    }
                }
                if (isAnyKeyDown) await Task.Delay(20);
            } while (isAnyKeyDown);
        }

        #endregion

        #region --- 悬浮球交互逻辑 ---

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdateSettingsPosition();
            _settingsWin.AnimateShow();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            // 如果已经被固定，或者鼠标正处在面板上方，则不隐藏
            if (_isPinned || _settingsWin.IsMouseOver) return;
            _settingsWin.AnimateHide();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BallShape.Opacity = 0.8; // 点击反馈
            // 记下按下时的坐标，用于判断是否发生了真正的拖拽
            _mouseDownPoint = e.GetScreenPosition();
        }

        private Point _mouseDownPoint;

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPos = e.GetScreenPosition();
                if (Math.Abs(currentPos.X - _mouseDownPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPos.Y - _mouseDownPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // 拖动时隐藏设置窗口
                    if (!_isPinned)
                    {
                        _settingsWin.AnimateHide();
                    }
                    
                    // 只要位移超过了最小拖动距离，就移交系统接管拖拽
                    this.DragMove();
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BallShape.Opacity = 1.0;

            var currentPos = e.GetScreenPosition();
            bool isClick = Math.Abs(currentPos.X - _mouseDownPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                           Math.Abs(currentPos.Y - _mouseDownPoint.Y) <= SystemParameters.MinimumVerticalDragDistance;

            if (isClick)
            {
                // 执行点击事件：切换锁定状态
                _isPinned = !_isPinned;
                if (_isPinned)
                {
                    BallShape.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)); // 变红提示已固定
                    _settingsWin.AnimateShow();
                    _settingsWin.Activate(); // 激活窗口以侦听 Deactivated 事件
                }
                else
                {
                    BallShape.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226)); // 恢复蓝
                    if (!this.IsMouseOver) _settingsWin.AnimateHide();
                }
            }
        }

        private void SettingsWin_Deactivated(object? sender, EventArgs e)
        {
            // 点击外部屏幕导致面板失焦时自动取消固定
            _isPinned = false;
            BallShape.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226));
            if (!this.IsMouseOver) _settingsWin.AnimateHide();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // 移动悬浮球时联动 Settings 面板位置
            if (_settingsWin.IsVisible)
                UpdateSettingsPosition();
        }

        private void UpdateSettingsPosition()
        {
            // 自动停靠在悬浮球旁边，并且处理屏幕边缘情况
            double screenWidth = SystemParameters.WorkArea.Width;
            double left = this.Left - _settingsWin.Width; // 默认放左边

            if (left < 0) left = this.Left + this.Width;  // 左侧空间不足则放右边

            _settingsWin.Left = left;
            _settingsWin.Top = this.Top;
        }

        protected override void OnClosed(EventArgs e)
        {
            _settingsWin.Close();
            base.OnClosed(e);
        }

        #endregion
    }

    // 辅助扩展：获取相对屏幕的绝对坐标
    public static class MouseEventExtensions
    {
        public static Point GetScreenPosition(this MouseEventArgs e)
        {
            return e.GetPosition(null); // null表示相对于整个无界屏幕空间
        }
    }
}