using System.Runtime.InteropServices;

namespace OneWireHID;

internal static class KeyboardSimulator
{
    private const uint INPUT_KEYBOARD = 1;
    private const ushort KEYEVENTF_KEYUP = 0x0002;
    private const ushort KEYEVENTF_UNICODE = 0x0004;
    private const int VK_RETURN = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short VkKeyScanW(char ch);

    public static void SendString(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        INPUT[] inputs = new INPUT[text.Length * 2];

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            ushort scan = c;

            inputs[i * 2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                wVk = 0,
                wScan = scan,
                dwFlags = KEYEVENTF_UNICODE,
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            };

            inputs[i * 2 + 1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                wVk = 0,
                wScan = scan,
                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            };
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void SendEnter()
    {
        INPUT[] inputs = new INPUT[2];

        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            wVk = VK_RETURN,
            wScan = 0,
            dwFlags = 0,
            time = 0,
            dwExtraInfo = UIntPtr.Zero
        };

        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            wVk = VK_RETURN,
            wScan = 0,
            dwFlags = KEYEVENTF_KEYUP,
            time = 0,
            dwExtraInfo = UIntPtr.Zero
        };

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void SendROM(string romHex)
    {
        string normalized = romHex.Replace("-", "").Replace(" ", "").Replace(":", "").ToUpper();
        SendString(normalized);
        Thread.Sleep(10);
        SendEnter();
    }
}