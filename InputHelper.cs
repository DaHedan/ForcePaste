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
            // 必须声明全完整的联合体确保正确的内存对齐尺寸
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

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // ------------- 核心模拟方法 -------------

        /// <summary>
        /// 模拟输入一段文本
        /// </summary>
        public static async Task SimulateTextTypingAsync(string text, int baseDelayMs, int randomVarianceMs)
        {
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                {
                    SendKey(0x0D, 0); // VK_RETURN
                }
                else
                {
                    SendUnicodeChar(c);
                }

                // 计算当前字符的随机延迟
                int currentDelay = baseDelayMs;
                if (randomVarianceMs > 0)
                {
                    // 在基础延迟的 正负 randomVarianceMs 范围内随机
                    int variance = Random.Shared.Next(-randomVarianceMs, randomVarianceMs + 1);
                    currentDelay = baseDelayMs + variance;
                }

                // 保证最少有 1ms 以上极短暂的停顿，防止粘连
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
    }
}