using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private AppSettings _settings;
        private bool _loaded = false; // 标记是否已完成初始化加载

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // 主题按钮引用（不再用 Tag 存选中状态，避免覆盖原始 Tag 值）
        private Button[] _themeBtns;
        private AppTheme[] _themeBtnValues;

        public SettingsWindow()
        {
            InitializeComponent();

            // 加载持久化设置
            _settings = SettingsService.Load();
            ApplySettingsToUI();

            // 初始化主题按钮
            _themeBtns = new[] { ThemeBtnLight, ThemeBtnDark, ThemeBtnSystem };
            _themeBtnValues = new[] { AppTheme.Light, AppTheme.Dark, AppTheme.System };

            // 初始化 RandomSlider 最大值
            RandomSlider.Maximum = SpeedSlider.Value;

            // 注册热键
            RegisterHotkey(_hotkeyKey, _hotkeyModifiers);

            // 刷新剪贴板
            RefreshClipboard();

            // 设置当前主题选中状态
            UpdateThemeSelection();

            // 订阅主题变化
            ThemeManager.ThemeChanged += OnThemeChanged;

            _loaded = true;
        }

        /// <summary>
        /// 将持久化设置应用到 UI 控件
        /// </summary>
        private void ApplySettingsToUI()
        {
            // 快捷键
            _hotkeyKey = SettingsService.StringToKey(_settings.HotkeyKey);
            _hotkeyModifiers = SettingsService.StringToModifiers(_settings.HotkeyModifiers);

            // 粘贴设置滑块（先设 Speed，使 RandomSlider.Maximum 自动更新到正确值）
            SpeedSlider.Value = _settings.SpeedDelay;
            SpeedValue.Text = ((int)_settings.SpeedDelay).ToString();
            // Speed 已更新 Maximum，此时再赋 Random 不会被截断
            RandomSlider.Value = _settings.RandomVariance;
            RandomValue.Text = ((int)_settings.RandomVariance).ToString();

            // 字体大小
            FontSizeSlider.Value = _settings.FontSize;
            FontSizeValue.Text = ((int)_settings.FontSize).ToString();
            ApplyFontSize(_settings.FontSize);

            // 显示当前快捷键
            CurrentHotkeyDisplay.Text = GetHotkeyDisplayText(_hotkeyKey, _hotkeyModifiers);
        }

        /// <summary>
        /// 保存当前设置到持久化文件
        /// </summary>
        private void SaveSettings()
        {
            if (!_loaded) return; // 初始化期间不保存

            _settings.HotkeyKey = SettingsService.KeyToString(_hotkeyKey);
            _settings.HotkeyModifiers = SettingsService.ModifiersToString(_hotkeyModifiers);
            _settings.SpeedDelay = SpeedSlider.Value;
            _settings.RandomVariance = RandomSlider.Value;
            _settings.FontSize = FontSizeSlider.Value;
            _settings.Theme = ThemeManager.CurrentTheme.ToString();

            SettingsService.Save(_settings);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateThemeSelection();
                ApplyThemeToSidebar();
                SaveSettings(); // 主题切换时保存
            }));
        }

        /// <summary>
        /// 更新主题按钮的选中外观。
        /// 注意：绝不修改 ThemeBtn 的 Tag 属性——Tag 存的是 "Light"/"Dark"/"System"，
        /// 用于 ThemeBtn_Click 识别点了哪个主题。选中状态只改视觉样式。
        /// </summary>
        private void UpdateThemeSelection()
        {
            var current = ThemeManager.CurrentTheme;
            var selectedBg = TryFindResource("SidebarBtnSelectedBrush") as Brush ?? Brushes.Transparent;
            var accentBr = TryFindResource("AccentBrush") as Brush ?? Brushes.Blue;
            var cardBg = TryFindResource("ThemeCardBgBrush") as Brush ?? Brushes.Transparent;
            var textBr = TryFindResource("TextBrush") as Brush ?? Brushes.White;

            for (int i = 0; i < _themeBtns.Length; i++)
            {
                bool isSelected = _themeBtnValues[i] == current;
                // Tag 保持原始值不动！

                var border = FindVisualChild<Border>(_themeBtns[i]);
                if (isSelected)
                {
                    if (border != null)
                    {
                        border.Background = selectedBg;
                        border.BorderThickness = new Thickness(3, 0, 0, 0);
                        border.BorderBrush = accentBr;
                        border.CornerRadius = new CornerRadius(10);
                    }
                    _themeBtns[i].Foreground = accentBr;
                }
                else
                {
                    if (border != null)
                    {
                        border.Background = cardBg;
                        border.BorderThickness = new Thickness(0);
                        border.CornerRadius = new CornerRadius(10);
                    }
                    _themeBtns[i].Foreground = textBr;
                }
            }
        }

        private void ApplyThemeToSidebar()
        {
            var normalBrush = TryFindResource("SidebarBtnNormalBrush") as Brush ?? Brushes.Transparent;
            var selectedBrush = TryFindResource("SidebarBtnSelectedBrush") as Brush ?? Brushes.Transparent;
            var normalIcon = TryFindResource("SidebarIconNormalBrush") as Brush ?? Brushes.Gray;
            var selectedIcon = TryFindResource("SidebarIconSelectedBrush") as Brush ?? Brushes.White;

            UpdateSidebarButton(BtnClipboard, normalBrush, selectedBrush, normalIcon, selectedIcon);
            UpdateSidebarButton(BtnPaste, normalBrush, selectedBrush, normalIcon, selectedIcon);
            UpdateSidebarButton(BtnHotkey, normalBrush, selectedBrush, normalIcon, selectedIcon);
            UpdateSidebarButton(BtnTheme, normalBrush, selectedBrush, normalIcon, selectedIcon);
            UpdateSidebarButton(BtnAbout, normalBrush, selectedBrush, normalIcon, selectedIcon);
            UpdateSidebarButton(BtnExit, normalBrush, selectedBrush, normalIcon, selectedIcon);
        }

        private void UpdateSidebarButton(Button btn, Brush normal, Brush selected, Brush normalIcon, Brush selectedIcon)
        {
            bool isSelected = btn.Tag?.ToString() == "Selected";
            var border = FindVisualChild<Border>(btn);
            if (border != null)
            {
                border.Background = isSelected ? selected : normal;
            }
            btn.Foreground = isSelected ? selectedIcon : normalIcon;
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
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
            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }

        #endregion

        #region --- 模拟输入 ---

        private async void OnStartPaste(object? sender, HotkeyEventArgs e)
        {
            if (_isTyping) return;
            e.Handled = true;
            string clipText = string.Empty;
            if (Clipboard.ContainsText()) clipText = Clipboard.GetText();
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
                if (isAnyKeyDown) await Task.Delay(20);
            } while (isAnyKeyDown);
        }

        #endregion

        #region --- 页面导航 ---

        private void SwitchPage(int pageIndex)
        {
            PageClipboard.Visibility = Visibility.Collapsed;
            PagePasteSettings.Visibility = Visibility.Collapsed;
            PageHotkeySettings.Visibility = Visibility.Collapsed;
            PageThemeSettings.Visibility = Visibility.Collapsed;
            PageAbout.Visibility = Visibility.Collapsed;
            PageExit.Visibility = Visibility.Collapsed;

            BtnClipboard.Tag = "Unselected";
            BtnPaste.Tag = "Unselected";
            BtnHotkey.Tag = "Unselected";
            BtnTheme.Tag = "Unselected";
            BtnAbout.Tag = "Unselected";

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
                    BtnPaste.Tag = "Selected";
                    break;
                case 2:
                    PageHotkeySettings.Visibility = Visibility.Visible;
                    PageTitle.Text = "快捷键设置";
                    BtnHotkey.Tag = "Selected";
                    HotkeyCaptureBox.Text = "在此处按下快捷键...";
                    HotkeyHint.Text = "直接按下你想设置的组合键即可";
                    HotkeyHint.Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
                    BtnApplyHotkey.IsEnabled = false;
                    _pendingKey = Key.None;
                    _pendingModifiers = ModifierKeys.None;
                    break;
                case 3:
                    PageThemeSettings.Visibility = Visibility.Visible;
                    PageTitle.Text = "外观设置";
                    BtnTheme.Tag = "Selected";
                    UpdateThemeSelection();
                    break;
                case 4:
                    PageAbout.Visibility = Visibility.Visible;
                    PageTitle.Text = "关于";
                    BtnAbout.Tag = "Selected";
                    break;
                case 5:
                    PageExit.Visibility = Visibility.Visible;
                    PageTitle.Text = "退出";
                    break;
            }

            ApplyThemeToSidebar();
        }

        private void BtnClipboard_Click(object sender, RoutedEventArgs e) => SwitchPage(0);
        private void BtnPaste_Click(object sender, RoutedEventArgs e) => SwitchPage(1);
        private void BtnHotkey_Click(object sender, RoutedEventArgs e) => SwitchPage(2);
        private void BtnTheme_Click(object sender, RoutedEventArgs e) => SwitchPage(3);
        private void BtnAbout_Click(object sender, RoutedEventArgs e) => SwitchPage(4);
        private void BtnExit_Click(object sender, RoutedEventArgs e) => SwitchPage(5);

        /// <summary>
        /// 点击关于页中的 Github / Gitee 链接，用默认浏览器打开
        /// </summary>
        private void AboutLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is string url)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { /* 忽略打开失败 */ }
            }
        }

        #endregion

        #region --- 剪贴板 ---

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

        #region --- 粘贴设置 ---

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedValue == null || SpeedSlider == null || RandomSlider == null) return;
            SpeedValue.Text = ((int)SpeedSlider.Value).ToString();
            RandomSlider.Maximum = SpeedSlider.Value;
            if (RandomSlider.Value > RandomSlider.Maximum)
                RandomSlider.Value = RandomSlider.Maximum;
            SaveSettings(); // 保存
        }

        private void RandomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RandomValue == null || RandomSlider == null) return;
            RandomValue.Text = ((int)RandomSlider.Value).ToString();
            SaveSettings(); // 保存
        }

        #endregion

        #region --- 快捷键 ---

        private void HotkeyCaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
                return;

            ModifierKeys mods = Keyboard.Modifiers;
            if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
                mods |= ModifierKeys.Windows;
            if (mods == ModifierKeys.None)
            {
                HotkeyHint.Text = "请按住 Ctrl/Alt/Shift/Win 再按目标键";
                var dangerColor = TryFindResource("DangerBrush") as Brush;
                HotkeyHint.Foreground = dangerColor ?? new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                return;
            }

            _pendingKey = e.Key;
            _pendingModifiers = mods;

            HotkeyCaptureBox.Text = GetHotkeyDisplayText(_pendingKey, _pendingModifiers);
            HotkeyHint.Text = "点击「应用快捷键」生效";
            var accentColor = TryFindResource("AccentBrush") as Brush;
            HotkeyHint.Foreground = accentColor ?? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
            BtnApplyHotkey.IsEnabled = true;
        }

        private void BtnApplyHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingKey == Key.None) return;
            RegisterHotkey(_pendingKey, _pendingModifiers);
            CurrentHotkeyDisplay.Text = GetHotkeyDisplayText(_pendingKey, _pendingModifiers);
            HotkeyHint.Text = "快捷键已生效!";
            var successColor = TryFindResource("SuccessBrush") as Brush;
            HotkeyHint.Foreground = successColor ?? new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
            BtnApplyHotkey.IsEnabled = false;
            SaveSettings(); // 保存
        }

        private void BtnResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            RegisterHotkey(Key.V, ModifierKeys.Control | ModifierKeys.Alt);
            CurrentHotkeyDisplay.Text = "Ctrl + Alt + V";
            HotkeyCaptureBox.Text = "在此处按下快捷键...";
            HotkeyHint.Text = "已恢复默认快捷键";
            var successColor = TryFindResource("SuccessBrush") as Brush;
            HotkeyHint.Foreground = successColor ?? new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
            BtnApplyHotkey.IsEnabled = false;
            _pendingKey = Key.None;
            _pendingModifiers = ModifierKeys.None;
            SaveSettings(); // 保存
        }

        #endregion

        #region --- 主题选择 ---

        /// <summary>
        /// 通过按钮在数组中的位置来识别主题，而不是读 Tag。
        /// Tag 保持 "Light"/"Dark"/"System" 不变（用于调试），但切换逻辑不依赖它。
        /// </summary>
        private void ThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // 用按钮引用找对应的主题值，不依赖 Tag
                for (int i = 0; i < _themeBtns.Length; i++)
                {
                    if (_themeBtns[i] == btn)
                    {
                        ThemeManager.SetTheme(_themeBtnValues[i]);
                        // SaveSettings 会在 OnThemeChanged 回调中调用
                        return;
                    }
                }
            }
        }

        #endregion

        #region --- 字体大小 ---

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FontSizeValue == null || FontSizeSlider == null) return;

            double val = FontSizeSlider.Value;
            FontSizeValue.Text = ((int)val).ToString();

            ApplyFontSize(val);
            SaveSettings(); // 保存
        }

        /// <summary>
        /// 应用字体大小到全局资源
        /// </summary>
        private void ApplyFontSize(double val)
        {
            if (Application.Current?.Resources != null)
            {
                Application.Current.Resources["ContentFontSize"] = val;
                Application.Current.Resources["TitleFontSize"] = val * 1.6;
                Application.Current.Resources["SmallFontSize"] = val * 0.92;
            }
        }

        #endregion

        #region --- 退出 ---

        private void BtnExitCancel_Click(object sender, RoutedEventArgs e) => TrayManager.HideToTray();
        private void BtnExitConfirm_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        #endregion

        #region --- 窗口行为 ---

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public void ForceShow()
        {
            // 停止所有可能正在运行的动画
            var fadeOut = (Storyboard)Resources["FadeOut"];
            fadeOut.Stop(this);
            fadeOut.Completed -= FadeOut_Completed;
            var fadeIn = (Storyboard)Resources["FadeIn"];
            fadeIn.Stop(this);

            // 强制完全可见
            Visibility = Visibility.Visible;
            Opacity = 1;
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
            sb.Completed -= FadeOut_Completed;
            sb.Completed += FadeOut_Completed;
            sb.Begin(this);
        }

        private void FadeOut_Completed(object? sender, EventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            var sb = (Storyboard)Resources["FadeOut"];
            sb.Completed -= FadeOut_Completed;
        }

        #endregion
    }
}
