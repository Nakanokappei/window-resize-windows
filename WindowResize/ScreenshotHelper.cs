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
    /// リサイズ後にウィンドウのスクリーンショットをキャプチャする
    /// Capture a screenshot of a window after resize
    /// </summary>
    public static void CaptureAfterResize(WindowInfo window, int delayMs = 500)
    {
        var store = SettingsStore.Shared;
        if (!store.ScreenshotEnabled)
            return;

        // リサイズ後の描画完了を待つ / Wait for render to complete after resize
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            try
            {
                using var bitmap = CaptureWindow(window.Handle);
                if (bitmap == null)
                    return;

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
    /// PrintWindow APIでウィンドウをキャプチャ
    /// Capture a window using PrintWindow API
    /// </summary>
    private static Bitmap? CaptureWindow(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out RECT rect))
            return null;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
            return null;

        // GetWindowRect はDPIスケーリング済みの物理ピクセルサイズを返す
        // PrintWindow もそのサイズで描画するので、そのまま使う
        // GetWindowRect returns physical pixel dimensions (DPI-scaled)
        // PrintWindow renders at the same size, so use dimensions directly
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            bool success = PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT);
            graphics.ReleaseHdc(hdc);

            if (!success)
            {
                bitmap.Dispose();
                return null;
            }
        }

        return bitmap;
    }

    /// <summary>
    /// ファイルに保存 / Save screenshot to file
    /// ファイル名形式: MMddHHmmss_AppName_WindowTitle.png
    /// </summary>
    private static void SaveToFile(Bitmap bitmap, WindowInfo window, string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        string timestamp = DateTime.Now.ToString("MMddHHmmss");
        string processName = SanitizeFileName(window.ProcessName);
        string windowTitle = SanitizeFileName(window.Title);

        // タイトルが長すぎる場合は切り詰める / Truncate if too long
        if (windowTitle.Length > 50)
            windowTitle = windowTitle[..50];

        string fileName = $"{timestamp}_{processName}_{windowTitle}.png";
        string filePath = Path.Combine(folderPath, fileName);

        bitmap.Save(filePath, ImageFormat.Png);
    }

    /// <summary>
    /// クリップボードにコピー / Copy screenshot to clipboard
    /// </summary>
    private static void CopyToClipboard(Bitmap bitmap)
    {
        Clipboard.SetImage(bitmap);
    }

    /// <summary>
    /// ファイル名に使えない文字を除去 / Remove invalid filename characters
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unknown";

        string invalid = new string(Path.GetInvalidFileNameChars());
        string pattern = $"[{Regex.Escape(invalid)}]";
        string sanitized = Regex.Replace(name, pattern, "_");

        // 連続するアンダースコアを1つにまとめる / Collapse consecutive underscores
        sanitized = Regex.Replace(sanitized, "_+", "_").Trim('_');

        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}
