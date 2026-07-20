using System.Runtime.InteropServices;

namespace OneWireHID;

internal static class KeyboardSimulator
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_RETURN = 0x0D;

    public static int LastError { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short VkKeyScanW(char ch);

    public static bool SendString(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        INPUT[] inputs = new INPUT[text.Length * 2];

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            ushort scan = c;

            inputs[i * 2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            inputs[i * 2 + 1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scan,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        return SendInputs(inputs);
    }

    public static bool SendEnter()
    {
        INPUT[] inputs = new INPUT[2];

        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_RETURN,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_RETURN,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        return SendInputs(inputs);
    }

    public static bool SendROM(string romHex)
    {
        string normalized = romHex.Replace("-", "").Replace(" ", "").Replace(":", "").ToUpper();
        bool textSent = SendString(normalized);
        Thread.Sleep(10);
        bool enterSent = SendEnter();
        return textSent && enterSent;
    }

    private static bool SendInputs(INPUT[] inputs)
    {
        LastError = 0;
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == inputs.Length)
            return true;

        LastError = Marshal.GetLastWin32Error();
        AppLogger.Warn($"SendInput failed. sent={sent}, expected={inputs.Length}, cbSize={Marshal.SizeOf<INPUT>()}, lastError={LastError}");
        return false;
    }
}
