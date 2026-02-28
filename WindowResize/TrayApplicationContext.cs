using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowResize;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly SettingsStore _store = SettingsStore.Shared;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _contextMenu = new ContextMenuStrip { ShowImageMargin = true };
        BuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "Window Resize"
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                // Show context menu on left click too
                var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                mi?.Invoke(_notifyIcon, null);
            }
        };

        _store.SettingsChanged += () =>
        {
            _contextMenu.Items.Clear();
            BuildMenu();
        };

        // Show splash screen briefly
        var splash = new SplashForm();
        splash.ShowSplash(1500);
    }

    private void BuildMenu()
    {
        // Resize submenu: ウィンドウ一覧 → サイズ選択 / Window list → Size selection
        var resizeItem = new ToolStripMenuItem(Strings.MenuResize);

        // ウィンドウ一覧を遅延取得 / Lazy-load window list when submenu opens
        resizeItem.DropDownOpening += (_, _) =>
        {
            resizeItem.DropDownItems.Clear();
            PopulateWindowList(resizeItem);
        };

        // サブメニュー矢印表示用のプレースホルダー / Placeholder so submenu arrow shows
        resizeItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuLoading) { Enabled = false });
        _contextMenu.Items.Add(resizeItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Settings
        var settingsItem = new ToolStripMenuItem(Strings.MenuSettings);
        settingsItem.Click += (_, _) => OpenSettings();
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Quit
        var quitItem = new ToolStripMenuItem(Strings.MenuQuit);
        quitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        };
        _contextMenu.Items.Add(quitItem);
    }

    private void PopulateWindowList(ToolStripMenuItem parent)
    {
        var windows = WindowManager.ListWindows();

        if (windows.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuNoWindows) { Enabled = false });
            return;
        }

        foreach (var win in windows)
        {
            string displayName = string.IsNullOrEmpty(win.Title) ? Strings.MenuUntitled : win.Title;
            string title = $"[{win.ProcessName}] {displayName}";
            var windowItem = new ToolStripMenuItem(title);

            // アプリアイコンをメニューに表示 / Show app icon in menu
            if (win.AppIcon != null)
            {
                try
                {
                    windowItem.Image = win.AppIcon.ToBitmap();
                    windowItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                }
                catch { }
            }

            // サイズ一覧をサブメニューとして追加 / Add size list as submenu
            PopulateSizeList(windowItem, win);

            parent.DropDownItems.Add(windowItem);
        }
    }

    private void PopulateSizeList(ToolStripMenuItem parent, WindowInfo win)
    {
        // ウィンドウが属するスクリーンの解像度を取得 / Get the screen resolution for the window's display
        var screenSize = GetScreenSizeForWindow(win);

        foreach (var size in _store.AllSizes)
        {
            string text = size.DisplayName;
            if (!string.IsNullOrEmpty(size.Label))
                text += $"    {size.Label}";

            bool exceedsScreen = size.Width > screenSize.Width || size.Height > screenSize.Height;

            var sizeItem = new ToolStripMenuItem(text);
            sizeItem.Enabled = !exceedsScreen;

            if (!exceedsScreen)
            {
                sizeItem.Click += (_, _) =>
                {
                    bool success = WindowManager.ResizeWindow(win, size);
                    if (success)
                    {
                        // リサイズ成功後にスクリーンショットをキャプチャ
                        // Capture screenshot after successful resize
                        ScreenshotHelper.CaptureAfterResize(win, size);
                    }
                    else
                    {
                        MessageBox.Show(
                            Strings.AlertResizeFailedBody,
                            Strings.AlertResizeFailedTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                };
            }

            parent.DropDownItems.Add(sizeItem);
        }
    }

    /// <summary>
    /// ウィンドウの中心座標が属するスクリーンのサイズを返す
    /// Returns the screen size of the display containing the window's center point
    /// </summary>
    private static Size GetScreenSizeForWindow(WindowInfo win)
    {
        var centerPoint = new Point(
            win.Left + win.Width / 2,
            win.Top + win.Height / 2
        );

        var screen = Screen.FromPoint(centerPoint);
        return screen.Bounds.Size;
    }

    private void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm();
        }

        _settingsForm.Show();
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    private static Icon CreateDefaultIcon()
    {
        // 埋め込みリソースからアイコンを読み込む / Load icon from embedded resource
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("WindowResize.Resources.app.ico");
        if (stream != null)
        {
            return new Icon(stream);
        }

        // フォールバック: 簡易アイコンを生成 / Fallback: generate simple icon
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.White, 1);
            g.DrawRectangle(pen, 2, 2, 11, 11);
            g.DrawLine(pen, 8, 6, 12, 6);
            g.DrawLine(pen, 12, 6, 12, 2);
            g.DrawLine(pen, 3, 9, 7, 9);
            g.DrawLine(pen, 3, 9, 3, 13);
        }

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _settingsForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
