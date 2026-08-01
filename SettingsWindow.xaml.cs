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
        private Key _hotkeyKey = Key.V;
        private ModifierKeys _hotkeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
        private Key _pendingKey = Key.None;
        private ModifierKeys _pendingModifiers = ModifierKeys.None;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public SettingsWindow()
        {
            InitializeComponent();
            // 初始化 RandomSlider 的 Max 绑定到 SpeedSlider.Value
            RandomSlider.Maximum = SpeedSlider.Value;
            RegisterHotkey(_hotkeyKey, _hotkeyModifiers);
            RefreshClipboard();
        }

        #region --- 热键管理 ---

        private void RegisterHotkey(Key key, ModifierKeys modifiers)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace("StartPaste", key, modifiers, OnStartPaste);
                _hotkeyKey = key;
                _hotkeyModifiers = modifiers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetHotkeyDisplayText(Key key, ModifierKeys modifiers)
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }

        #endregion

        #region --- 模拟输入逻辑 ---

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

            int baseDelay = (int)SpeedSlider.Value;
            int randomVariance = (int)RandomSlider.Value;

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

        #endregion

        #region --- 页面导航 ---

        private void SwitchPage(int pageIndex)
        {
            // 隐藏所有页面
            PageClipboard.Visibility = Visibility.Collapsed;
            PagePasteSettings.Visibility = Visibility.Collapsed;
            PageHotkeySettings.Visibility = Visibility.Collapsed;
            PageExit.Visibility = Visibility.Collapsed;

            // 重置所有按钮选中状态
            BtnClipboard.Tag = "Unselected";
            BtnPasteSettings.Tag = "Unselected";
            BtnHotkeySettings.Tag = "Unselected";

            switch (pageIndex)
            {
                case 0:
                    PageClipboard.Visibility = Visibility.Visible;
                    PageTitle.Text = "剪贴板";
                    BtnClipboard.Tag = "Selected";
                    RefreshClipboard();
                    break;
                case 1:
                    PagePasteSettings.Visibility = Visibility.Visible;
                    PageTitle.Text = "粘贴设置";
                    BtnPasteSettings.Tag = "Selected";
                    break;
                case 2:
                    PageHotkeySettings.Visibility = Visibility.Visible;
                    PageTitle.Text = "快捷键设置";
                    BtnHotkeySettings.Tag = "Selected";
                    // 清空捕获框
                    HotkeyCaptureBox.Text = "在此处按下快捷键...";
                    HotkeyHint.Text = "直接按下你想设置的组合键即可";
                    HotkeyHint.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                    BtnApplyHotkey.IsEnabled = false;
                    _pendingKey = Key.None;
                    _pendingModifiers = ModifierKeys.None;
                    break;
                case 3:
                    PageExit.Visibility = Visibility.Visible;
                    PageTitle.Text = "退出";
                    break;
            }
        }

        private void BtnClipboard_Click(object sender, RoutedEventArgs e) => SwitchPage(0);
        private void BtnPasteSettings_Click(object sender, RoutedEventArgs e) => SwitchPage(1);
        private void BtnHotkeySettings_Click(object sender, RoutedEventArgs e) => SwitchPage(2);
        private void BtnExit_Click(object sender, RoutedEventArgs e) => SwitchPage(3);

        #endregion

        #region --- 剪贴板页面 ---

        private void RefreshClipboard()
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                ClipboardText.Text = string.IsNullOrEmpty(text) ? "暂无剪贴板内容" : text;
            }
            else
            {
                ClipboardText.Text = "暂无剪贴板内容";
            }
        }

        private void BtnRefreshClipboard_Click(object sender, RoutedEventArgs e) => RefreshClipboard();

        #endregion

        #region --- 粘贴设置页面 ---

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedValue == null || SpeedSlider == null || RandomSlider == null) return;
            SpeedValue.Text = ((int)SpeedSlider.Value).ToString();
            // 波动范围最大值跟随基础速度
            RandomSlider.Maximum = SpeedSlider.Value;
            if (RandomSlider.Value > RandomSlider.Maximum)
                RandomSlider.Value = RandomSlider.Maximum;
        }

        private void RandomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RandomValue == null || RandomSlider == null) return;
            RandomValue.Text = ((int)RandomSlider.Value).ToString();
        }

        #endregion

        #region --- 快捷键设置页面 ---

        private void HotkeyCaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // 忽略单独的修饰键
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
                return;

            // 不允许只按字母/数字键（必须有修饰键）
            ModifierKeys mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.None)
            {
                HotkeyHint.Text = "请按住 Ctrl/Alt/Shift 再按目标键";
                HotkeyHint.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
                return;
            }

            _pendingKey = e.Key;
            _pendingModifiers = mods;

            HotkeyCaptureBox.Text = GetHotkeyDisplayText(_pendingKey, _pendingModifiers);
            HotkeyHint.Text = "点击「应用快捷键」生效";
            HotkeyHint.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4A, 0x90, 0xE2));
            BtnApplyHotkey.IsEnabled = true;
        }

        private void BtnApplyHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingKey == Key.None) return;
            RegisterHotkey(_pendingKey, _pendingModifiers);
            CurrentHotkeyDisplay.Text = GetHotkeyDisplayText(_pendingKey, _pendingModifiers);
            HotkeyHint.Text = "快捷键已生效!";
            HotkeyHint.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            BtnApplyHotkey.IsEnabled = false;
        }

        private void BtnResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            RegisterHotkey(Key.V, ModifierKeys.Control | ModifierKeys.Alt);
            CurrentHotkeyDisplay.Text = "Ctrl + Alt + V";
            HotkeyCaptureBox.Text = "在此处按下快捷键...";
            HotkeyHint.Text = "已恢复默认快捷键";
            HotkeyHint.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            BtnApplyHotkey.IsEnabled = false;
            _pendingKey = Key.None;
            _pendingModifiers = ModifierKeys.None;
        }

        #endregion

        #region --- 退出页面 ---

        private void BtnExitCancel_Click(object sender, RoutedEventArgs e) => SwitchPage(0);
        private void BtnExitConfirm_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        #endregion

        #region --- 窗口行为 ---

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 关闭时隐藏而不是退出
            e.Cancel = true;
            Hide();
        }

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
            // Bug fix: 移除旧 handler 避免内存泄漏
            sb.Completed -= FadeOut_Completed;
            sb.Completed += FadeOut_Completed;
            sb.Begin(this);
        }

        private void FadeOut_Completed(object? sender, EventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            // 移除 handler 避免累积
            var sb = (Storyboard)Resources["FadeOut"];
            sb.Completed -= FadeOut_Completed;
        }

        #endregion
    }
}
