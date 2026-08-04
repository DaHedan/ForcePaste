using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ForcePaste
{
    public static class InputHelper
    {
        // ------------- Win32 API 结构体定义 -------------
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            // 使用 FieldOffset 确保联合体内存尺寸正确
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const uint KEYEVENTF_KEYUP = 0x0002;

        const ushort VK_SHIFT = 0x10;
        const ushort VK_CONTROL = 0x11;
        const ushort VK_MENU = 0x12; // Alt
        const ushort VK_RETURN = 0x0D;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // ------------- 按键模拟方法 -------------

        // 模拟输入一段文本
        public static Task SimulateTextTypingAsync(string text, int baseDelayMs, int randomVarianceMs)
        {
            return SimulateTextTypingAsync(text, baseDelayMs, randomVarianceMs, "Enter");
        }

        // 模拟输入一段文本，支持自定义换行符处理方式
        public static async Task SimulateTextTypingAsync(string text, int baseDelayMs, int randomVarianceMs, string newlineMode)
        {
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                {
                    switch (newlineMode)
                    {
                        case "ShiftEnter":
                            SendKeyWithModifiers(VK_RETURN, VK_SHIFT);
                            break;
                        case "CtrlEnter":
                            SendKeyWithModifiers(VK_RETURN, VK_CONTROL);
                            break;
                        case "AltEnter":
                            SendKeyWithModifiers(VK_RETURN, VK_MENU);
                            break;
                        default: // "Enter"
                            SendKey(VK_RETURN, 0);
                            break;
                    }
                }
                else
                {
                    SendUnicodeChar(c);
                }

                // 计算当前字符的延迟映射
                int currentDelay = baseDelayMs;
                if (randomVarianceMs > 0)
                {
                    // 在基础延迟的基础上增加 randomVarianceMs 范围内的随机值
                    int variance = Random.Shared.Next(-randomVarianceMs, randomVarianceMs + 1);
                    currentDelay = baseDelayMs + variance;
                }

                // 保证至少有 1ms 以上的暂停，防止粘滞
                currentDelay = Math.Max(1, currentDelay);

                await Task.Delay(currentDelay);
            }
        }

        private static void SendUnicodeChar(char c)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 0;
            inputs[1].U.ki.wScan = c;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        private static void SendKey(ushort wVk, ushort wScan)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = wVk;
            inputs[0].U.ki.wScan = wScan;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = wVk;
            inputs[1].U.ki.wScan = wScan;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        // 模拟按下修饰键 + 目标键 + 释放修饰键
        private static void SendKeyWithModifiers(ushort wVk, params ushort[] modifiers)
        {
            // 按下所有修饰键
            foreach (var mod in modifiers)
            {
                INPUT[] press = new INPUT[1];
                press[0].type = INPUT_KEYBOARD;
                press[0].U.ki.wVk = mod;
                press[0].U.ki.wScan = 0;
                press[0].U.ki.dwFlags = 0;
                press[0].U.ki.time = 0;
                press[0].U.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, press, INPUT.Size);
            }

            // 按下+释放目标键
            SendKey(wVk, 0);

            // 释放所有修饰键（逆序）
            for (int i = modifiers.Length - 1; i >= 0; i--)
            {
                INPUT[] release = new INPUT[1];
                release[0].type = INPUT_KEYBOARD;
                release[0].U.ki.wVk = modifiers[i];
                release[0].U.ki.wScan = 0;
                release[0].U.ki.dwFlags = KEYEVENTF_KEYUP;
                release[0].U.ki.time = 0;
                release[0].U.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, release, INPUT.Size);
            }
        }
    }
}
