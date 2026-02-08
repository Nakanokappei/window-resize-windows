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

    public SettingsForm()
    {
        InitializeComponents();
        LoadData();
    }

    private void InitializeComponents()
    {
        Text = Strings.SettingsTitle;
        Size = new Size(420, 520);
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
