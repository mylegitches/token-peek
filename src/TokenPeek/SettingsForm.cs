namespace TokenPeek;

/// <summary>
/// Simple settings dialog for entering the Ollama API key and toggling
/// "Start with Windows". Shown on first run (no key) and from the tray menu.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action _onSaved;

    // Controls
    private readonly TextBox  _keyBox       = new();
    private readonly CheckBox _startupCheck = new();
    private readonly Button   _saveBtn      = new();
    private readonly Button   _cancelBtn    = new();
    private readonly LinkLabel _keysLink    = new();
    private readonly Label    _keyLabel     = new();
    private readonly Label    _hint         = new();

    public SettingsForm(AppConfig config, Action onSaved)
    {
        _config  = config;
        _onSaved = onSaved;

        BuildUi();
        Load += OnLoad;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        string? key = _config.GetApiKey();
        // If loaded from env var, show placeholder
        if (!string.IsNullOrWhiteSpace(key))
            _keyBox.Text = key.StartsWith("ollama_") ? key : key; // show as-is
        _startupCheck.Checked = _config.StartWithWindows;
    }

    private void BuildUi()
    {
        Text            = "Token Peek — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = false;
        ClientSize      = new Size(420, 220);
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        // Title label
        var title = new Label
        {
            Text      = "Ollama API Key",
            Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x1E, 0x1E, 0x1E),
            Left      = 16,
            Top       = 14,
            AutoSize  = true,
        };

        // Key label
        _keyLabel.Text      = "API key:";
        _keyLabel.Left      = 16;
        _keyLabel.Top       = 50;
        _keyLabel.AutoSize  = true;

        // Key text box
        _keyBox.Left          = 16;
        _keyBox.Top           = 68;
        _keyBox.Width         = 384;
        _keyBox.Height        = 24;
        _keyBox.PasswordChar  = '●';
        _keyBox.Font          = new Font("Segoe UI", 9f);

        // Toggle password visibility button
        var showBtn = new Button
        {
            Text   = "👁",
            Left   = 360,
            Top    = 67,
            Width  = 40,
            Height = 26,
            Font   = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        showBtn.FlatAppearance.BorderSize = 0;
        showBtn.Click += (_, _) =>
            _keyBox.PasswordChar = _keyBox.PasswordChar == '\0' ? '●' : '\0';

        // Hint
        _hint.Text      = "Create a key at:";
        _hint.Left      = 16;
        _hint.Top       = 100;
        _hint.AutoSize  = true;
        _hint.ForeColor = Color.Gray;

        // Link
        _keysLink.Text      = "ollama.com/settings/keys";
        _keysLink.Left      = 103;
        _keysLink.Top       = 100;
        _keysLink.AutoSize  = true;
        _keysLink.LinkClicked += (_, _) =>
            OpenUrl("https://ollama.com/settings/keys");

        // Startup checkbox
        _startupCheck.Text    = "Start Token Peek with Windows";
        _startupCheck.Left    = 16;
        _startupCheck.Top     = 128;
        _startupCheck.AutoSize = true;

        // Buttons
        _saveBtn.Text      = "Save";
        _saveBtn.Left      = 232;
        _saveBtn.Top       = 176;
        _saveBtn.Width     = 80;
        _saveBtn.Height    = 28;
        _saveBtn.BackColor = Color.FromArgb(0x1E, 0x6F, 0xC8);
        _saveBtn.ForeColor = Color.White;
        _saveBtn.FlatStyle = FlatStyle.Flat;
        _saveBtn.FlatAppearance.BorderSize = 0;
        _saveBtn.Click    += OnSave;

        _cancelBtn.Text      = "Cancel";
        _cancelBtn.Left      = 320;
        _cancelBtn.Top       = 176;
        _cancelBtn.Width     = 80;
        _cancelBtn.Height    = 28;
        _cancelBtn.FlatStyle = FlatStyle.Flat;
        _cancelBtn.Click    += (_, _) => Close();

        AcceptButton = _saveBtn;
        CancelButton = _cancelBtn;

        Controls.AddRange([
            title, _keyLabel, _keyBox, showBtn,
            _hint, _keysLink, _startupCheck,
            _saveBtn, _cancelBtn,
        ]);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string key = _keyBox.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show("Please enter a valid API key.", "Token Peek",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.SetApiKey(key);
        _config.StartWithWindows = _startupCheck.Checked;

        _onSaved();
        Close();
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }
}
