using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MoonwalkPro;

/// <summary>Owns the tray icon, the right-click settings menu, and the overlay.</summary>
internal sealed class TrayContext : ApplicationContext
{
    Config _cfg;
    Engine _engine;
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu;
    StatusOverlay? _overlay;

    // Menu items we update with check marks.
    ToolStripMenuItem _miEnabled = null!, _miSprint = null!, _miFocus = null!;
    ToolStripMenuItem _miModePer = null!, _miModeHold = null!, _miModeToggle = null!;

    public TrayContext(Config cfg)
    {
        _cfg = cfg;
        _engine = new Engine(_cfg, Balloon);

        _menu = BuildMenu();
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Moonwalk Pro",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.DoubleClick += (_, _) => _engine.ToggleMaster();

        if (_cfg.ShowStatusGui)
        {
            _overlay = new StatusOverlay(() => _engine);
            _overlay.Show();
        }

        _engine.Start();
        Balloon("Loaded. Right-click the tray icon for settings. F8=on/off, F9=detect pad, F10=panic.");
    }

    // ---------------------------------------------------------------- menu
    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();

        var header = new ToolStripMenuItem("Moonwalk Pro") { Enabled = false };
        m.Items.Add(header);
        m.Items.Add(new ToolStripSeparator());

        _miEnabled = Item("Enabled  (F8)", (_, _) => _engine.ToggleMaster());
        m.Items.Add(_miEnabled);

        var mode = new ToolStripMenuItem("Force activation mode");
        _miModePer = Item("Per-trick (use config)", (_, _) => SetMode(""));
        _miModeHold = Item("Hold all", (_, _) => SetMode("Hold"));
        _miModeToggle = Item("Toggle all", (_, _) => SetMode("Toggle"));
        mode.DropDownItems.AddRange(new ToolStripItem[] { _miModePer, _miModeHold, _miModeToggle });
        m.Items.Add(mode);

        _miSprint = Item("Sprint while active", (_, _) => FlipBool("Sprinting"));
        m.Items.Add(_miSprint);
        _miFocus = Item("Only when game focused", (_, _) => FlipBool("OnlyWhenGameActive"));
        m.Items.Add(_miFocus);

        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Item("Edit settings (config.ini)", (_, _) => OpenConfig()));
        m.Items.Add(Item("Open folder", (_, _) => SafeRun(AppContext.BaseDirectory)));
        m.Items.Add(Item("Reload settings", (_, _) => Reload()));
        m.Items.Add(Item("Detect controller button (F9)", (_, _) => _engine.DetectController()));
        m.Items.Add(Item("Help", (_, _) => ShowHelp()));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Item("Exit", (_, _) => ExitApp()));

        m.Opening += (_, _) => RefreshChecks();
        return m;
    }

    static ToolStripMenuItem Item(string text, EventHandler onClick)
    {
        var it = new ToolStripMenuItem(text);
        it.Click += onClick;
        return it;
    }

    void RefreshChecks()
    {
        _miEnabled.Checked = _engine.Enabled;
        _miSprint.Checked = _cfg.Sprinting;
        _miFocus.Checked = _cfg.OnlyWhenGameActive;
        _miModePer.Checked = _cfg.ModeOverride.Length == 0;
        _miModeHold.Checked = _cfg.ModeOverride.Equals("Hold", StringComparison.OrdinalIgnoreCase);
        _miModeToggle.Checked = _cfg.ModeOverride.Equals("Toggle", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- settings actions
    void SetMode(string mode)
    {
        _cfg.ModeOverride = mode;
        _cfg.Ini.Set("General", "DefaultMode", mode);
        _cfg.Ini.Save();
        _engine.StopAll();
    }

    void FlipBool(string field)
    {
        if (field == "Sprinting")
        {
            _cfg.Sprinting = !_cfg.Sprinting;
            _cfg.Ini.Set("General", "Sprinting", _cfg.Sprinting ? "1" : "0");
        }
        else
        {
            _cfg.OnlyWhenGameActive = !_cfg.OnlyWhenGameActive;
            _cfg.Ini.Set("General", "OnlyWhenGameActive", _cfg.OnlyWhenGameActive ? "1" : "0");
        }
        _cfg.Ini.Save();
    }

    void OpenConfig()
    {
        try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _cfg.Path + "\"") { UseShellExecute = true }); }
        catch { SafeRun(_cfg.Path); }
    }

    void Reload()
    {
        _engine.Dispose();
        _cfg = Config.Load(_cfg.Path);
        _engine = new Engine(_cfg, Balloon);
        _overlay?.Rebind(() => _engine);

        if (_cfg.ShowStatusGui && _overlay == null)
        {
            _overlay = new StatusOverlay(() => _engine);
            _overlay.Show();
        }
        else if (!_cfg.ShowStatusGui && _overlay != null)
        {
            _overlay.Close();
            _overlay.Dispose();
            _overlay = null;
        }

        _engine.Start();
        Balloon("Settings reloaded.");
    }

    void ShowHelp()
    {
        var sb = new System.Text.StringBuilder("Your tricks:\n");
        foreach (var t in _cfg.Tricks)
        {
            string pad = t.JoyButtons.Length > 0 ? string.Join("/", t.JoyButtons) : "-";
            sb.AppendLine($"  {t.Name,-18} pad: {pad,-8} key: {_cfg.Ini.Get(t.Name, "Key", "-")}");
        }

        MessageBox.Show(
            sb + "\nSystem keys:\n" +
            "  F8 = enable/disable     F9 = detect controller button     F10 = panic stop\n" +
            "  PgUp / PgDn = tempo +/- 5ms     Home = reset tempo\n\n" +
            "If your survivor turns round mid-moonwalk, that is ping - nudge the tempo\n" +
            "with PgUp/PgDn until it holds.\n\n" +
            "Controller: bind buttons by name (A B X Y LB RB LT RT LS RS Back Start DUp...).\n" +
            "Edit everything (keys, timings, tricks) from the tray menu -> Edit settings.",
            "Moonwalk Pro - help");
    }

    void Balloon(string text)
    {
        try
        {
            _tray.BalloonTipTitle = "Moonwalk Pro";
            _tray.BalloonTipText = text;
            _tray.ShowBalloonTip(1500);
            _tray.Text = ("Moonwalk Pro - " + text).Length > 63
                ? "Moonwalk Pro" : "Moonwalk Pro - " + text;
        }
        catch { }
    }

    static void SafeRun(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }

    void ExitApp()
    {
        _engine.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _overlay?.Close();
        ExitThread();
    }
}
