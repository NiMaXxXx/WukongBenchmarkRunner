using System.Runtime.InteropServices;

namespace WukongBenchmarkRunner.Automation;

public static class Input
{
    public enum Key : ushort
    {
        E = 0x45, // «Подтвердить» в меню бенчмарка
    }

    public static void Press(Key key, int holdMs = 80)
    {
        SendKey((ushort)key, keyUp: false);
        Thread.Sleep(holdMs);
        SendKey((ushort)key, keyUp: true);
    }

    // Пункт меню подсвечивается только по событию движения мыши.
    public static void MoveMouse(int x, int y)
    {
        SetCursorPos(x - 2, y - 2);
        Thread.Sleep(50);
        SetCursorPos(x, y);
        Send(new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dx = 0, dy = 0, dwFlags = MOUSEEVENTF_MOVE } } });
    }

    internal static void TapAlt()
    {
        SendKey(0x12, keyUp: false);
        SendKey(0x12, keyUp: true);
    }

    private static void SendKey(ushort vk, bool keyUp)
    {
        var scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        uint flags = KEYEVENTF_SCANCODE;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        Send(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wScan = scan, dwFlags = flags } } });
    }

    private static void Send(INPUT input) => SendInput(1, [input], Marshal.SizeOf<INPUT>());

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    // Размер union должен совпадать с MOUSEINPUT, иначе SendInput отклонит структуру.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
}
