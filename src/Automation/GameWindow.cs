using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace WukongBenchmarkRunner.Automation;

public sealed class GameWindow(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;

    public static GameWindow? Find(int processId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var pid);
            if (pid != processId || !IsWindowVisible(h))
                return true;
            var cls = new StringBuilder(64);
            GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() != "UnrealWindow")
                return true;
            found = h;
            return false;
        }, IntPtr.Zero);

        return found == IntPtr.Zero ? null : new GameWindow(found);
    }

    public bool IsAlive => IsWindow(Handle);

    public Rectangle ClientBounds
    {
        get
        {
            GetClientRect(Handle, out var r);
            var origin = new POINT { X = 0, Y = 0 };
            ClientToScreen(Handle, ref origin);
            return new Rectangle(origin.X, origin.Y, r.Right - r.Left, r.Bottom - r.Top);
        }
    }

    public bool IsForeground => GetForegroundWindow() == Handle;

    public bool Focus()
    {
        if (IsForeground)
            return true;

        if (IsIconic(Handle))
            ShowWindow(Handle, SW_RESTORE);

        // Без этого Windows не даст фоновому процессу переключить фокус.
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            Input.TapAlt();
            BringWindowToTop(Handle);
            SetForegroundWindow(Handle);
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, foregroundThread, false);
        }

        Thread.Sleep(300);
        return IsForeground;
    }

    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
}
