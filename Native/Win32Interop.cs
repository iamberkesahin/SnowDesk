using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SnowDesk.Native;

public static class Win32Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    private static bool IsProtectedSystemProcess(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return true;
            using var proc = Process.GetProcessById((int)pid);
            string name = proc.ProcessName.ToLowerInvariant();
            return name is "snippingtool" or "screenclippinghost" or "explorer"
                or "shellexperiencehost" or "searchhost" or "startmenuexperiencehost"
                or "textinputhost" or "lockapp" or "dwm";
        }
        catch
        {
            return true;
        }
    }

    public static void MakeWindowClickThrough(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        EnsureAlwaysOnTop(hwnd);
    }

    public static void EnsureAlwaysOnTop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static bool IsForegroundFullscreen(IntPtr overlayHwnd, int screenWidth, int screenHeight)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == overlayHwnd || IsProtectedSystemProcess(fg)) return false;

        if (DwmGetWindowAttribute(fg, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0)
            return r.Width >= screenWidth && r.Height >= screenHeight;

        return false;
    }

    public static void ForceGameOverlayCompatibility(IntPtr overlayHwnd, int screenWidth, int screenHeight)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == overlayHwnd) return;

        // Win+Shift+S (SnippingTool) veya sistem araçları aktifken hiçbir pencereye dokunma
        if (IsProtectedSystemProcess(fg)) return;

        if (DwmGetWindowAttribute(fg, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0)
        {
            if (r.Width >= screenWidth && r.Height >= screenHeight)
            {
                int style = GetWindowLong(fg, GWL_STYLE);
                if ((style & WS_CAPTION) != 0 || (style & WS_THICKFRAME) != 0)
                {
                    SetWindowLong(fg, GWL_STYLE, style & ~(WS_CAPTION | WS_THICKFRAME));
                    SetWindowPos(fg, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }
            }
        }
        EnsureAlwaysOnTop(overlayHwnd);
    }

    public static List<(IntPtr Hwnd, RECT Bounds)> GetVisibleWindows(IntPtr overlayHwnd)
    {
        var results = new List<(IntPtr, RECT)>();
        var sb = new StringBuilder(64);

        EnumWindows((hwnd, _) =>
        {
            if (hwnd == overlayHwnd || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                return true;

            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            sb.Clear();
            GetClassName(hwnd, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow"
                or "ScreenClippingHost" or "Microsoft-Windows-SnipperToolbar")
                return true;

            if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) == 0)
            {
                if (rect.Width > 140 && rect.Height > 80 && rect.Top > 12 && rect.Left > -2000)
                    results.Add((hwnd, rect));
            }
            return true;
        }, IntPtr.Zero);

        return results;
    }
}
