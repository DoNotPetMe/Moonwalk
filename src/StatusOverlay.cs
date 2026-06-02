using System;
using System.Drawing;
using System.Windows.Forms;

namespace MoonwalkPro;

/// <summary>Tiny always-on-top overlay showing on/off + the active trick.</summary>
internal sealed class StatusOverlay : Form
{
    readonly Label _label;
    readonly System.Windows.Forms.Timer _timer;
    Func<Engine> _engine;

    public StatusOverlay(Func<Engine> engine)
    {
        _engine = engine;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(12, 12);
        BackColor = Color.FromArgb(20, 20, 20);
        Opacity = 0.82;
        Size = new Size(190, 44);

        _label = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = "Moonwalk Pro",
        };
        Controls.Add(_label);

        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += (_, _) => Refresh2();
        _timer.Start();
    }

    public void Rebind(Func<Engine> engine) => _engine = engine;

    void Refresh2()
    {
        var e = _engine();
        string state = !e.Enabled ? "DISABLED"
            : e.ActiveTrick.Length == 0 ? "idle"
            : ">> " + e.ActiveTrick;
        _label.Text = $"Moonwalk Pro [{(e.Enabled ? "ON" : "OFF")}]\n{state}";
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
