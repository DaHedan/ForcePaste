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
        private bool _isTyping = false;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public MainWindow()
        {
            InitializeComponent();
            RegisterHotkeys();
        }

        private void RegisterHotkeys()
        {
            try
            {
                // 将热键改为 Ctrl + Alt + V
                HotkeyManager.Current.AddOrReplace("StartPaste", Key.V, ModifierKeys.Control | ModifierKeys.Alt, OnStartPaste);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            // 同时获取基本速度和随机波动范围
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
                // 注意修饰键等待也包含了 Alt 键 (VK_MENU)
                await WaitForModifiersReleaseAsync();

                // 传递基础延迟和波动范围
                await InputHelper.SimulateTextTypingAsync(clipText, baseDelay, randomVariance);
            }
            finally
            {
                _isTyping = false;
            }
        }

        private async Task WaitForModifiersReleaseAsync()
        {
            // VK_SHIFT=0x10, VK_CONTROL=0x11, VK_MENU(Alt)=0x12, VK_LWIN=0x5B, VK_RWIN=0x5C
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
    }
}