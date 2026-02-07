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
        _contextMenu = new ContextMenuStrip();
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
        // Resize submenu
        var resizeItem = new ToolStripMenuItem(Strings.MenuResize);

        foreach (var size in _store.AllSizes)
        {
            string text = size.DisplayName;
            if (!string.IsNullOrEmpty(size.Label))
                text += $"    {size.Label}";

            var sizeItem = new ToolStripMenuItem(text);

            // Lazy-load window list when submenu opens
            sizeItem.DropDownOpening += (_, _) =>
            {
                sizeItem.DropDownItems.Clear();
                PopulateWindowList(sizeItem, size);
            };

            // Add placeholder so submenu arrow shows
            sizeItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuLoading) { Enabled = false });
            resizeItem.DropDownItems.Add(sizeItem);
        }

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

    private void PopulateWindowList(ToolStripMenuItem parent, PresetSize size)
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
            var item = new ToolStripMenuItem(title);
            item.Click += (_, _) =>
            {
                bool success = WindowManager.ResizeWindow(win, size);
                if (!success)
                {
                    MessageBox.Show(
                        Strings.AlertResizeFailedBody,
                        Strings.AlertResizeFailedTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
            parent.DropDownItems.Add(item);
        }
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
        // Create a simple 16x16 icon with "WR" text
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.White, 1);
            // Draw a simple window-resize icon: a rectangle with arrows
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
