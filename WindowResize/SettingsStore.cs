using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace WindowResize;

public class SettingsStore
{
    private static readonly Lazy<SettingsStore> _instance = new(() => new SettingsStore());
    public static SettingsStore Shared => _instance.Value;

    private readonly string _settingsPath;
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowResize";

    public List<PresetSize> CustomSizes { get; private set; } = new();

    public bool LaunchAtLogin
    {
        get => GetLaunchAtLogin();
        set => SetLaunchAtLogin(value);
    }

    public static readonly List<PresetSize> BuiltInSizes = new()
    {
        new(3840, 2160, "4K UHD"),
        new(2560, 1440, "QHD"),
        new(1920, 1080, "Full HD"),
        new(1680, 1050, "WSXGA+"),
        new(1366, 768,  "HD+"),
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
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var data = new SettingsData { CustomSizes = CustomSizes };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private bool GetLaunchAtLogin()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    private void SetLaunchAtLogin(bool enabled)
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

    private class SettingsData
    {
        public List<PresetSize>? CustomSizes { get; set; }
    }
}
