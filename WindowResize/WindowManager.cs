using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowResize;

public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string ProcessName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Icon? AppIcon { get; set; }
}

public static class WindowManager
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    private const uint WM_GETICON = 0x007F;
    private const IntPtr ICON_SMALL = 0;
    private const IntPtr ICON_BIG = 1;
    private const IntPtr ICON_SMALL2 = 2;
    private const int GCLP_HICONSM = -34;
    private const int GCLP_HICON = -14;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_VISIBLE = 0x10000000;
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const int DWMWA_CLOAKED = 14;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;

    // Enumerate all visible, resizable application windows, excluding this process.
    public static List<WindowInfo> ListWindows()
    {
        var windows = new List<WindowInfo>();
        int myPid = Environment.ProcessId;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            // Skip cloaked windows (UWP hidden windows, etc.)
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            if (cloaked != 0)
                return true;

            int style = GetWindowLong(hWnd, GWL_STYLE);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Must have a caption (title bar) - filters out background/system windows
            if ((style & (int)WS_CAPTION) != (int)WS_CAPTION)
                return true;

            // Skip tool windows unless they are explicitly app windows
            if ((exStyle & (int)WS_EX_TOOLWINDOW) != 0 && (exStyle & (int)WS_EX_APPWINDOW) == 0)
                return true;

            // Get title
            int titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return true;

            var titleBuilder = new StringBuilder(titleLength + 1);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            // Get process info
            GetWindowThreadProcessId(hWnd, out uint processId);
            if ((int)processId == myPid)
                return true;

            string processName = "";
            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch { }

            // Get size
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return true;

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                ProcessName = processName,
                Title = title,
                Left = rect.Left,
                Top = rect.Top,
                Width = width,
                Height = height,
                AppIcon = GetWindowIcon(hWnd)
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Retrieve the application icon for a window, trying multiple Win32 strategies.
    /// </summary>
    private static Icon? GetWindowIcon(IntPtr hWnd)
    {
        try
        {
            IntPtr iconHandle = IntPtr.Zero;

            // Try WM_GETICON with decreasing size preference (small2 -> small -> big).
            foreach (var sizeHint in new[] { ICON_SMALL2, ICON_SMALL, ICON_BIG })
            {
                iconHandle = SendMessage(hWnd, WM_GETICON, sizeHint, IntPtr.Zero);
                if (iconHandle != IntPtr.Zero) break;
            }

            // Fall back to the window class icon (small -> large).
            if (iconHandle == IntPtr.Zero)
            {
                foreach (var classIndex in new[] { GCLP_HICONSM, GCLP_HICON })
                {
                    iconHandle = GetClassLongPtr(hWnd, classIndex);
                    if (iconHandle != IntPtr.Zero) break;
                }
            }

            if (iconHandle != IntPtr.Zero)
                return Icon.FromHandle(iconHandle);
        }
        catch { }

        return null;
    }

    // Resize the window to the given preset size, keeping its current position.
    public static bool ResizeWindow(WindowInfo window, PresetSize size)
    {
        return SetWindowPos(
            window.Handle,
            IntPtr.Zero,
            0, 0,
            size.Width, size.Height,
            SWP_NOMOVE | SWP_NOZORDER
        );
    }
}
