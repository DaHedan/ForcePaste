using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ForcePaste
{
    public partial class MainWindow : Window
    {
        private SettingsWindow _settingsWin;
        private bool _isPinned = false;

        public MainWindow()
        {
            InitializeComponent();
            _settingsWin = new SettingsWindow();

            // 恢复悬浮球位置
            var settings = SettingsService.Settings;
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;
            if (settings.BallLeft != -1 && settings.BallTop != -1 &&
                settings.BallLeft >= vLeft && settings.BallLeft < vRight &&
                settings.BallTop >= vTop && settings.BallTop < vBottom)
            {
                this.Left = settings.BallLeft;
                this.Top = settings.BallTop;
                ClampPositionToScreen();
            }
            else
            {
                this.Left = SystemParameters.WorkArea.Right - 90;
                this.Top = 100;
            }

            _settingsWin.Deactivated += SettingsWin_Deactivated;
            ThemeManager.ThemeChanged += OnThemeChanged;

            // 窗口句柄就绪后再初始化托盘
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            TrayManager.Initialize(this, _settingsWin);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 动态资源会自动更新 Ellipse.Fill 和 Path.Fill
                // PinRing.Stroke 也会通过 DynamicResource 自动更新
            }));
        }

        #region --- 悬浮球交互 ---

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdateSettingsPosition();
            _settingsWin.AnimateShow();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPinned || _settingsWin.IsMouseOver) return;
            _settingsWin.AnimateHide();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BallShape.Opacity = 0.8;
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
                    if (!_isPinned) _settingsWin.AnimateHide();
                    this.DragMove();
                    ClampPositionToScreen();
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BallShape.Opacity = 1.0;
            var currentPos = e.GetScreenPosition();
            bool isClick = Math.Abs(currentPos.X - _mouseDownPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                           Math.Abs(currentPos.Y - _mouseDownPoint.Y) <= SystemParameters.MinimumVerticalDragDistance;
            if (!isClick)
            {
                // 拖拽结束，保存位置
                SettingsService.Settings.BallLeft = this.Left;
                SettingsService.Settings.BallTop = this.Top;
                SettingsService.Save();
            }
            if (isClick)
            {
                _isPinned = !_isPinned;
                UpdatePinVisual();
                if (_isPinned)
                {
                    _settingsWin.ForceShow();
                    _settingsWin.Activate();
                }
                else
                {
                    if (!this.IsMouseOver) _settingsWin.AnimateHide();
                }
            }
        }

        /// <summary>
        /// 将悬浮球位置钳制在虚拟屏幕范围内，保证至少一半可见
        /// </summary>
        private void ClampPositionToScreen()
        {
            double minVisible = Width / 2;
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            double maxLeft = vRight - minVisible;
            double maxTop = vBottom - minVisible;
            double minLeft = vLeft - Width + minVisible;
            double minTop = vTop - Height + minVisible;

            double newLeft = this.Left, newTop = this.Top;

            if (newLeft < minLeft) newLeft = minLeft;
            else if (newLeft > maxLeft) newLeft = maxLeft;
            if (newTop < minTop) newTop = minTop;
            else if (newTop > maxTop) newTop = maxTop;

            this.Left = newLeft;
            this.Top = newTop;
        }

        private void UpdatePinVisual()
        {
            // 隐藏固定环（蓝圈）
            PinRing.Opacity = 0;
        }

        private void SettingsWin_Deactivated(object? sender, EventArgs e)
        {
            _isPinned = false;
            UpdatePinVisual();
            if (!this.IsMouseOver) _settingsWin.AnimateHide();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (_settingsWin.IsVisible) UpdateSettingsPosition();
        }

        private void UpdateSettingsPosition()
        {
            double left = this.Left - _settingsWin.Width;
            if (left < 0) left = this.Left + this.Width;
            _settingsWin.Left = left;
            _settingsWin.Top = this.Top;
        }

        protected override void OnClosed(EventArgs e)
        {
            // 保存悬浮球位置
            SettingsService.Settings.BallLeft = this.Left;
            SettingsService.Settings.BallTop = this.Top;
            SettingsService.Save();

            ThemeManager.ThemeChanged -= OnThemeChanged;
            TrayManager.Cleanup();
            _settingsWin.Close();
            base.OnClosed(e);
        }

        #endregion
    }

    public static class MouseEventExtensions
    {
        public static Point GetScreenPosition(this MouseEventArgs e)
        {
            return e.GetPosition(null);
        }
    }
}
