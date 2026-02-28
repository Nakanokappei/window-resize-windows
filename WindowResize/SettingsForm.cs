using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowResize;

public class SettingsForm : Form
{
    private readonly SettingsStore _store = SettingsStore.Shared;
    private ListBox _builtInList = null!;
    private ListBox _customList = null!;
    private TextBox _widthBox = null!;
    private TextBox _heightBox = null!;
    private Button _addButton = null!;
    private Button _removeButton = null!;
    private CheckBox _launchAtLoginCheck = null!;
    private CheckBox _screenshotEnabledCheck = null!;
    private Panel _screenshotOptionsPanel = null!;
    private CheckBox _screenshotSaveToFileCheck = null!;
    private CheckBox _screenshotCopyToClipboardCheck = null!;
    private Button _chooseFolderButton = null!;
    private Label _folderPathLabel = null!;

    // スクリーンショットセクション開始Y座標 / Y position where screenshot section starts
    private int _screenshotSectionY;

    public SettingsForm()
    {
        InitializeComponents();
        LoadData();
    }

    private void InitializeComponents()
    {
        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        int y = 12;

        // Preset Sizes header
        var headerLabel = new Label
        {
            Text = Strings.SettingsPresetSizes,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(headerLabel);
        y += 28;

        // Built-in group
        var builtInGroup = new GroupBox
        {
            Text = Strings.SettingsBuiltIn,
            Location = new Point(12, y),
            Size = new Size(380, 160)
        };

        _builtInList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(364, 130),
            SelectionMode = SelectionMode.None,
            BorderStyle = BorderStyle.None
        };
        builtInGroup.Controls.Add(_builtInList);
        Controls.Add(builtInGroup);
        y += 170;

        // Custom group
        var customGroup = new GroupBox
        {
            Text = Strings.SettingsCustom,
            Location = new Point(12, y),
            Size = new Size(380, 165)
        };

        _customList = new ListBox
        {
            Location = new Point(8, 20),
            Size = new Size(280, 100),
            BorderStyle = BorderStyle.FixedSingle
        };
        customGroup.Controls.Add(_customList);

        // Remove button — リストの右に配置 / Place to the right of the list
        _removeButton = new Button
        {
            Text = Strings.SettingsRemove,
            Location = new Point(296, 20),
            Size = new Size(76, 28),
            Enabled = false
        };
        _removeButton.Click += RemoveButton_Click;
        customGroup.Controls.Add(_removeButton);

        _customList.SelectedIndexChanged += (_, _) =>
        {
            _removeButton.Enabled = _customList.SelectedIndex >= 0;
        };

        // Add row
        var addPanel = new Panel
        {
            Location = new Point(8, 126),
            Size = new Size(364, 30)
        };

        var widthLabel = new Label
        {
            Text = Strings.SettingsWidth,
            Location = new Point(0, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(widthLabel);

        _widthBox = new TextBox
        {
            Location = new Point(50, 2),
            Size = new Size(70, 23)
        };
        addPanel.Controls.Add(_widthBox);

        var sepLabel = new Label
        {
            Text = Strings.SettingsDimensionSeparator,
            Location = new Point(125, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(sepLabel);

        var heightLabel = new Label
        {
            Text = Strings.SettingsHeight,
            Location = new Point(140, 5),
            AutoSize = true
        };
        addPanel.Controls.Add(heightLabel);

        _heightBox = new TextBox
        {
            Location = new Point(190, 2),
            Size = new Size(70, 23)
        };
        addPanel.Controls.Add(_heightBox);

        _addButton = new Button
        {
            Text = Strings.SettingsAdd,
            Location = new Point(270, 1),
            Size = new Size(60, 25)
        };
        _addButton.Click += AddButton_Click;
        addPanel.Controls.Add(_addButton);

        customGroup.Controls.Add(addPanel);

        Controls.Add(customGroup);
        y += 175;

        // Launch at Login
        _launchAtLoginCheck = new CheckBox
        {
            Text = Strings.SettingsLaunchAtLogin,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.LaunchAtLogin
        };
        _launchAtLoginCheck.CheckedChanged += (_, _) =>
        {
            _store.LaunchAtLogin = _launchAtLoginCheck.Checked;
        };
        Controls.Add(_launchAtLoginCheck);
        y += 32;

        // Screenshot section header
        var screenshotHeader = new Label
        {
            Text = Strings.SettingsScreenshot,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, y),
            AutoSize = true
        };
        Controls.Add(screenshotHeader);
        y += 24;

        // Screenshot enabled
        _screenshotEnabledCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotEnabled,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = _store.ScreenshotEnabled
        };
        _screenshotEnabledCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotEnabled = _screenshotEnabledCheck.Checked;
            _store.SaveScreenshotSettings();
            UpdateScreenshotOptionsVisibility();
        };
        Controls.Add(_screenshotEnabledCheck);
        y += 26;

        // スクリーンショットオプションパネル（表示/非表示切替用）
        // Screenshot options panel (for show/hide toggle)
        _screenshotSectionY = y;
        _screenshotOptionsPanel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(420, 86)
        };

        int py = 0;

        // Save to file
        _screenshotSaveToFileCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotSaveToFile,
            Location = new Point(28, py),
            AutoSize = true,
            Checked = _store.ScreenshotSaveToFile
        };
        _screenshotSaveToFileCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotSaveToFile = _screenshotSaveToFileCheck.Checked;
            _store.SaveScreenshotSettings();
            _chooseFolderButton.Enabled = _store.ScreenshotSaveToFile;
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotSaveToFileCheck);
        py += 26;

        // Choose folder button + path label
        _chooseFolderButton = new Button
        {
            Text = Strings.SettingsScreenshotChooseFolder,
            Location = new Point(44, py),
            AutoSize = true,
            Enabled = _store.ScreenshotSaveToFile
        };
        _chooseFolderButton.Click += ChooseFolderButton_Click;
        _screenshotOptionsPanel.Controls.Add(_chooseFolderButton);

        _folderPathLabel = new Label
        {
            Text = GetFolderDisplayText(),
            Location = new Point(_chooseFolderButton.Right + 8, py + 4),
            Size = new Size(240, 20),
            ForeColor = Color.Gray,
            AutoEllipsis = true
        };
        _screenshotOptionsPanel.Controls.Add(_folderPathLabel);
        py += 30;

        // Copy to clipboard
        _screenshotCopyToClipboardCheck = new CheckBox
        {
            Text = Strings.SettingsScreenshotCopyToClipboard,
            Location = new Point(28, py),
            AutoSize = true,
            Checked = _store.ScreenshotCopyToClipboard
        };
        _screenshotCopyToClipboardCheck.CheckedChanged += (_, _) =>
        {
            _store.ScreenshotCopyToClipboard = _screenshotCopyToClipboardCheck.Checked;
            _store.SaveScreenshotSettings();
        };
        _screenshotOptionsPanel.Controls.Add(_screenshotCopyToClipboardCheck);

        Controls.Add(_screenshotOptionsPanel);

        // 初期表示状態を設定 / Set initial visibility
        UpdateScreenshotOptionsVisibility();
    }

    /// <summary>
    /// スクリーンショットオプションの表示/非表示を切り替え、フォーム高さを自動調整
    /// Toggle screenshot options visibility and auto-adjust form height
    /// </summary>
    private void UpdateScreenshotOptionsVisibility()
    {
        bool show = _store.ScreenshotEnabled;
        _screenshotOptionsPanel.Visible = show;

        // フォーム高さを再計算 / Recalculate form height
        int contentBottom = show
            ? _screenshotSectionY + _screenshotOptionsPanel.Height + 12
            : _screenshotSectionY + 12;

        // タイトルバー分を含むフォームサイズ / Form size including title bar
        ClientSize = new Size(404, contentBottom);
    }

    private void LoadData()
    {
        // Built-in sizes
        _builtInList.Items.Clear();
        foreach (var size in SettingsStore.BuiltInSizes)
        {
            string display = size.DisplayName;
            if (!string.IsNullOrEmpty(size.Label))
                display += $"    {size.Label}";
            _builtInList.Items.Add(display);
        }

        // Custom sizes
        RefreshCustomList();
    }

    private void RefreshCustomList()
    {
        _customList.Items.Clear();
        foreach (var size in _store.CustomSizes)
        {
            _customList.Items.Add(size.DisplayName);
        }

        if (_store.CustomSizes.Count == 0)
        {
            _customList.Items.Add(Strings.SettingsNoCustomSizes);
            _customList.Enabled = false;
        }
        else
        {
            _customList.Enabled = true;
        }

        _removeButton.Enabled = false;
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (int.TryParse(_widthBox.Text, out int w) &&
            int.TryParse(_heightBox.Text, out int h) &&
            w > 0 && h > 0)
        {
            _store.AddSize(new PresetSize(w, h));
            _widthBox.Clear();
            _heightBox.Clear();
            RefreshCustomList();
        }
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        int idx = _customList.SelectedIndex;
        if (idx >= 0 && idx < _store.CustomSizes.Count)
        {
            _store.RemoveSize(_store.CustomSizes[idx]);
            RefreshCustomList();
        }
    }

    private void ChooseFolderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath))
            dialog.SelectedPath = _store.ScreenshotSaveFolderPath;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _store.ScreenshotSaveFolderPath = dialog.SelectedPath;
            _store.SaveScreenshotSettings();
            _folderPathLabel.Text = GetFolderDisplayText();
        }
    }

    private string GetFolderDisplayText()
    {
        return string.IsNullOrEmpty(_store.ScreenshotSaveFolderPath)
            ? Strings.SettingsScreenshotNoFolderSelected
            : _store.ScreenshotSaveFolderPath;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
