using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
#if WINDOWS10_0_17763_0_OR_GREATER
using Windows.ApplicationModel;
#endif

namespace WindowResize;

public class SettingsStore
{
    private static readonly Lazy<SettingsStore> _instance = new(() => new SettingsStore());
    public static SettingsStore Shared => _instance.Value;

    private readonly string _settingsPath;
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowResize";

    public List<PresetSize> CustomSizes { get; private set; } = new();

    // Screenshot settings
    public bool ScreenshotEnabled { get; set; }
    public bool ScreenshotSaveToFile { get; set; } = true;
    public string ScreenshotSaveFolderPath { get; set; } = "";
    public bool ScreenshotCopyToClipboard { get; set; }

    public bool LaunchAtLogin
    {
        get => GetLaunchAtLogin();
        set => SetLaunchAtLogin(value);
    }

    public static readonly List<PresetSize> BuiltInSizes = new()
    {
        new(3840, 2160, "4K UHD"),
        new(2560, 1440, "QHD"),
        new(1920, 1200, "WUXGA"),
        new(1920, 1080, "Full HD"),
        new(1680, 1050, "WSXGA+"),
        new(1600, 900,  "HD+"),
        new(1440, 900,  "WXGA+"),
        new(1366, 768,  "WXGA"),
        new(1280, 1024, "SXGA"),
        new(1280, 720,  "HD"),
        new(1024, 768,  "XGA"),
        new(800,  600,  "SVGA"),
    };

    public List<PresetSize> AllSizes
    {
        get
        {
            var all = new List<PresetSize>(BuiltInSizes);
            all.AddRange(CustomSizes);
            return all;
        }
    }

    public event Action? SettingsChanged;

    private SettingsStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appDir = Path.Combine(appData, "WindowResize");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "settings.json");
        Load();
    }

    public void AddSize(PresetSize size)
    {
        CustomSizes.Add(size);
        Save();
        SettingsChanged?.Invoke();
    }

    public void RemoveSize(PresetSize size)
    {
        CustomSizes.RemoveAll(s => s.Id == size.Id);
        Save();
        SettingsChanged?.Invoke();
    }

    public void SaveScreenshotSettings()
    {
        Save();
        SettingsChanged?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data?.CustomSizes != null)
                    CustomSizes = data.CustomSizes;
                ScreenshotEnabled = data?.ScreenshotEnabled ?? false;
                ScreenshotSaveToFile = data?.ScreenshotSaveToFile ?? true;
                ScreenshotSaveFolderPath = data?.ScreenshotSaveFolderPath ?? "";
                ScreenshotCopyToClipboard = data?.ScreenshotCopyToClipboard ?? false;
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var data = new SettingsData
            {
                CustomSizes = CustomSizes,
                ScreenshotEnabled = ScreenshotEnabled,
                ScreenshotSaveToFile = ScreenshotSaveToFile,
                ScreenshotSaveFolderPath = ScreenshotSaveFolderPath,
                ScreenshotCopyToClipboard = ScreenshotCopyToClipboard
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Detects whether the app is running as a packaged MSIX app.
    /// </summary>
    public static bool IsPackaged()
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            // Package.Current throws if not packaged
            _ = Package.Current.Id;
            return true;
        }
        catch { }
#endif
        return false;
    }

    private bool GetLaunchAtLogin()
    {
        if (IsPackaged())
            return GetLaunchAtLoginPackaged();
        return GetLaunchAtLoginRegistry();
    }

    private void SetLaunchAtLogin(bool enabled)
    {
        if (IsPackaged())
            SetLaunchAtLoginPackaged(enabled);
        else
            SetLaunchAtLoginRegistry(enabled);
    }

    // --- Registry-based (non-packaged / EXE distribution) ---

    private bool GetLaunchAtLoginRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    private void SetLaunchAtLoginRegistry(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enabled)
            {
                string exePath = Environment.ProcessPath ?? "";
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }

    // --- StartupTask-based (packaged / MSIX Store distribution) ---

    private bool GetLaunchAtLoginPackaged()
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            var task = StartupTask.GetAsync("WindowResizeStartup").GetAwaiter().GetResult();
            return task.State == StartupTaskState.Enabled;
        }
        catch { }
#endif
        return false;
    }

    private void SetLaunchAtLoginPackaged(bool enabled)
    {
#if WINDOWS10_0_17763_0_OR_GREATER
        try
        {
            var task = StartupTask.GetAsync("WindowResizeStartup").GetAwaiter().GetResult();
            if (enabled)
            {
                if (task.State == StartupTaskState.Disabled)
                    task.RequestEnableAsync().GetAwaiter().GetResult();
            }
            else
            {
                task.Disable();
            }
        }
        catch { }
#endif
    }

    private class SettingsData
    {
        public List<PresetSize>? CustomSizes { get; set; }
        public bool ScreenshotEnabled { get; set; }
        public bool ScreenshotSaveToFile { get; set; } = true;
        public string ScreenshotSaveFolderPath { get; set; } = "";
        public bool ScreenshotCopyToClipboard { get; set; }
    }
}
