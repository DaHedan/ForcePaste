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
            this.Left = SystemParameters.WorkArea.Right - 90;
            this.Top = 100;
            _settingsWin.Deactivated += SettingsWin_Deactivated;
            ThemeManager.ThemeChanged += OnThemeChanged;
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
                _isPinned = !_isPinned;
                UpdatePinVisual();
                if (_isPinned)
                {
                    _settingsWin.AnimateShow();
                    _settingsWin.Activate();
                }
                else
                {
                    if (!this.IsMouseOver) _settingsWin.AnimateHide();
                }
            }
        }

        private void UpdatePinVisual()
        {
            // 用简单的淡入淡出动画切换固定环
            if (_isPinned)
            {
                PinRing.Opacity = 1;
            }
            else
            {
                PinRing.Opacity = 0;
            }
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
            ThemeManager.ThemeChanged -= OnThemeChanged;
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
