using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowResize;

public static class ScreenshotHelper
{
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    // Per-Monitor V2: makes GetWindowRect return physical pixels instead of logical
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    // PW_RENDERFULLCONTENT = 2: captures DWM-composed content including DirectX
    private const uint PW_RENDERFULLCONTENT = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Capture a screenshot of a window after resize.
    /// If screenshots are enabled, wait briefly for the window to repaint, then capture and save/copy.
    /// </summary>
    public static void CaptureAfterResize(WindowInfo window, PresetSize targetSize, int delayMs = 500)
    {
        var store = SettingsStore.Shared;
        if (!store.ScreenshotEnabled)
            return;

        // Wait for the window to finish rendering after the resize
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            try
            {
                using var captured = CaptureWindow(window.Handle);
                if (captured == null)
                    return;

                // Scale from physical-pixel capture down to the user-specified target size
                using var bitmap = new Bitmap(targetSize.Width, targetSize.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(captured, 0, 0, targetSize.Width, targetSize.Height);
                }

                if (store.ScreenshotSaveToFile && !string.IsNullOrEmpty(store.ScreenshotSaveFolderPath))
                {
                    SaveToFile(bitmap, window, store.ScreenshotSaveFolderPath);
                }

                if (store.ScreenshotCopyToClipboard)
                {
                    CopyToClipboard(bitmap);
                }
            }
            catch { }
        };
        timer.Start();
    }

    /// <summary>
    /// Capture a window using PrintWindow API with native GDI DC.
    /// </summary>
    private static Bitmap? CaptureWindow(IntPtr hWnd)
    {
        // In DPI-virtualized environments (e.g. Parallels on Retina Mac), GetWindowRect
        // may return logical pixels. Switch to Per-Monitor V2 awareness so that the
        // pixel dimensions match what PrintWindow actually renders.
        IntPtr prevDpiContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            if (!GetWindowRect(hWnd, out RECT rect))
                return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            // Create a native GDI memory DC compatible with the window's DC
            IntPtr windowDC = GetDC(hWnd);
            IntPtr memDC = CreateCompatibleDC(windowDC);
            IntPtr hBitmap = CreateCompatibleBitmap(windowDC, width, height);
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);

            bool success = PrintWindow(hWnd, memDC, PW_RENDERFULLCONTENT);

            Bitmap? bitmap = null;
            if (success)
            {
                bitmap = Image.FromHbitmap(hBitmap);
            }

            // Clean up GDI resources
            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(hWnd, windowDC);

            return bitmap;
        }
        finally
        {
            SetThreadDpiAwarenessContext(prevDpiContext);
        }
    }

    /// <summary>
    /// Save screenshot to file. Filename format: MMddHHmmss_AppName_WindowTitle.png
    /// </summary>
    private static void SaveToFile(Bitmap bitmap, WindowInfo window, string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        string timestamp = DateTime.Now.ToString("MMddHHmmss");
        string processName = SanitizeFileName(window.ProcessName);
        string windowTitle = SanitizeFileName(window.Title);

        // Truncate overly long titles to avoid filesystem issues
        if (windowTitle.Length > 50)
            windowTitle = windowTitle[..50];

        string fileName = $"{timestamp}_{processName}_{windowTitle}.png";
        string filePath = Path.Combine(folderPath, fileName);

        bitmap.Save(filePath, ImageFormat.Png);
    }

    /// <summary>
    /// Copy screenshot to clipboard.
    /// </summary>
    private static void CopyToClipboard(Bitmap bitmap)
    {
        Clipboard.SetImage(bitmap);
    }

    /// <summary>
    /// Remove characters invalid in filenames.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unknown";

        string invalid = new string(Path.GetInvalidFileNameChars());
        string pattern = $"[{Regex.Escape(invalid)}]";
        string sanitized = Regex.Replace(name, pattern, "_");

        // Collapse consecutive underscores into one
        sanitized = Regex.Replace(sanitized, "_+", "_").Trim('_');

        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}
