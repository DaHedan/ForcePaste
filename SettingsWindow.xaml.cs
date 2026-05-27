using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using NHotkey.Wpf;
using NHotkey;
using System.Windows.Media.Animation;

namespace ForcePaste
{
    public partial class SettingsWindow : Window
    {
        private bool _isTyping = false;

        public enum SettingsTab
        {
            General,
            Speed,
            Random
        }

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public SettingsWindow()
        {
            InitializeComponent();
            RegisterHotkeys();
        }

        private void RegisterHotkeys()
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("StartPaste", Key.V, ModifierKeys.Control | ModifierKeys.Alt, OnStartPaste);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowSettings(SettingsTab tab)
        {
            Show();
            Activate();
            WindowState = WindowState.Normal;

            // 可根据tab参数切换到不同设置项
            switch (tab)
            {
                case SettingsTab.Speed:
                    SpeedSlider.Focus();
                    break;
                case SettingsTab.Random:
                    RandomSlider.Focus();
                    break;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 关闭时隐藏而不是退出
            e.Cancel = true;
            Hide();
        }

        private async void OnStartPaste(object? sender, HotkeyEventArgs e)
        {
            if (_isTyping) return;
            e.Handled = true;

            string clipText = string.Empty;
            if (Clipboard.ContainsText())
            {
                clipText = Clipboard.GetText();
            }

            if (string.IsNullOrEmpty(clipText)) return;

            int baseDelay = 0;
            int randomVariance = 0;
            Dispatcher.Invoke(() =>
            {
                baseDelay = (int)SpeedSlider.Value;
                randomVariance = (int)RandomSlider.Value;
            });

            _isTyping = true;

            try
            {
                await WaitForModifiersReleaseAsync();
                await InputHelper.SimulateTextTypingAsync(clipText, baseDelay, randomVariance);
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

                if (isAnyKeyDown)
                {
                    await Task.Delay(20);
                }
            } while (isAnyKeyDown);
        }

        // 公共属性供外部访问设置值
        public int BaseDelay => (int)Dispatcher.Invoke(() => SpeedSlider.Value);
        public int RandomVariance => (int)Dispatcher.Invoke(() => RandomSlider.Value);

        public void AnimateShow()
        {
            if (Opacity >= 1) return;
            this.Visibility = Visibility.Visible;
            ((Storyboard)Resources["FadeIn"]).Begin(this);
        }

        public void AnimateHide()
        {
            if (Opacity <= 0) return;
            var sb = (Storyboard)Resources["FadeOut"];
            sb.Completed += (s, e) => this.Visibility = Visibility.Collapsed;
            sb.Begin(this);
        }

        // 新增的完全退出程序的事件逻辑
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
