using System.Runtime.InteropServices;
using System.Text;

namespace SnowDesk.Native;

public static class Win32Interop
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public static void MakeWindowClickThrough(IntPtr hwnd)
    {
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    public static List<(IntPtr Handle, RECT Bounds)> GetVisibleWindows(IntPtr ignoreHwnd)
    {
        var windows = new List<(IntPtr, RECT)>();

        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == ignoreHwnd || !IsWindowVisible(hWnd) || IsIconic(hWnd))
                return true;

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title) ||
                title == "Program Manager" ||
                title == "Windows Input Experience" ||
                title == "Settings")
                return true;

            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) == 0)
            {
                // Tam ekran olmayan veya ekranın üstünden biraz boşluğu olan tüm pencereleri yakala
                if (rect.Width > 150 && rect.Height > 80 && rect.Top >= -10 && rect.Top < 1200)
                {
                    // Tam ekran kaplayan pencereleri (Top <= 0 ve Height >= 1000) masaüstü gibi görüp kar biriktirme,
                    // ama pencere modundaki tüm sekmelerin/uygulamaların üstüne kar yağsın!
                    if (rect.Top > 8)
                    {
                        windows.Add((hWnd, rect));
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public static bool IsForegroundFullscreen(IntPtr ignoreHwnd, int screenWidth, int screenHeight)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ignoreHwnd) return false;

        var sb = new StringBuilder(256);
        GetWindowText(fg, sb, sb.Capacity);
        string title = sb.ToString();
        if (title == "Program Manager" || string.IsNullOrWhiteSpace(title)) return false;

        if (GetWindowRect(fg, out RECT r))
        {
            return r.Left <= 0 && r.Top <= 0 && r.Width >= screenWidth && r.Height >= screenHeight;
        }
        return false;
    }
}