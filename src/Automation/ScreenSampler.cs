using System.Drawing;
using System.Runtime.InteropServices;

namespace WukongBenchmarkRunner.Automation;

// По яркости отличаем подсвеченный пункт меню от фона, не завися от языка интерфейса.
public static class ScreenSampler
{
    public static double AverageLuminance(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return 0;

        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var bmi = new BITMAPINFO
        {
            biSize = Marshal.SizeOf<BITMAPINFO>(),
            biWidth = area.Width,
            biHeight = -area.Height, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };
        var bitmap = CreateDIBSection(memDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        var old = SelectObject(memDc, bitmap);
        try
        {
            BitBlt(memDc, 0, 0, area.Width, area.Height, screenDc, area.X, area.Y, SRCCOPY);

            var pixels = new byte[area.Width * area.Height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            double sum = 0;
            for (var i = 0; i < pixels.Length; i += 4)
                sum += 0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2]; // BGRA
            return sum / (pixels.Length / 4);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        public int bmiColors;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
}
