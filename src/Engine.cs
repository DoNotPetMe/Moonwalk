using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MoonwalkPro;

/// <summary>
/// Drives everything: a background poll loop watches keyboard hotkeys and the
/// controller, and launches trick sequences. Each running trick lives on its own
/// task and checks an "isActive" predicate so it stops the instant you let go.
/// </summary>
internal sealed class Engine : IDisposable
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    readonly Config _cfg;
    readonly Controller _controller;
    readonly Action<string> _notify;
    readonly Random _rng = new();

    Thread? _pollThread;
    volatile bool _running;

    volatile string _activeTrick = "";
    readonly ConcurrentDictionary<string, bool> _toggled = new();

    public bool Enabled { get; private set; } = true;
    public string ActiveTrick => _activeTrick;
    public Controller Pad => _controller;

    public Engine(Config cfg, Action<string> notify)
    {
        _cfg = cfg;
        _notify = notify;
        _controller = new Controller(cfg.JoyIndex);
        foreach (var t in cfg.Tricks) _toggled[t.Name] = false;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "MoonwalkPoll" };
        _pollThread.Start();
    }

    public void Dispose()
    {
        _running = false;
        StopAll();
        try { _pollThread?.Join(500); } catch { }
    }

    // ---------------------------------------------------------------- poll loop
    void PollLoop()
    {
        var prev = new ConcurrentDictionary<string, bool>();
        bool pF8 = false, pF9 = false, pF10 = false;

        while (_running)
        {
            // System hotkeys (rising-edge).
            Edge(ref pF8, _cfg.MasterToggleVk, ToggleMaster);
            Edge(ref pF10, _cfg.PanicStopVk, PanicStop);
            Edge(ref pF9, _cfg.DetectVk, DetectController);

            if (_cfg.JoyEnabled) _controller.Poll();

            foreach (var t in _cfg.Tricks)
            {
                bool down = TriggerDown(t);
                bool was = prev.TryGetValue(t.Name, out var b) && b;
                if (down && !was) OnTrigger(t);
                prev[t.Name] = down;
            }

            Thread.Sleep(_cfg.PollRate);
        }
    }

    static void Edge(ref bool prev, int vk, Action onPress)
    {
        if (vk < 0) return;
        bool now = Input.IsDown(vk);
        if (now && !prev) onPress();
        prev = now;
    }

    bool TriggerDown(Trick t)
    {
        if (t.KeyVk >= 0 && Input.IsDown(t.KeyVk)) return true;
        if (_cfg.JoyEnabled)
            foreach (var b in t.JoyButtons)
                if (_controller.IsDown(b)) return true;
        return false;
    }

    string EffMode(Trick t)
        => t.Mode.Equals("Tap", StringComparison.OrdinalIgnoreCase) ? "Tap"
         : _cfg.ModeOverride.Length == 0 ? t.Mode
         : _cfg.ModeOverride;

    // ---------------------------------------------------------------- triggers
    void OnTrigger(Trick t)
    {
        string mode = EffMode(t);
        if (mode.Equals("Toggle", StringComparison.OrdinalIgnoreCase)) { FireToggle(t); return; }
        if (mode.Equals("Hold", StringComparison.OrdinalIgnoreCase) && _activeTrick == t.Name) return;
        StartTrick(t);
    }

    void FireToggle(Trick t)
    {
        if (_toggled.TryGetValue(t.Name, out var on) && on) { _toggled[t.Name] = false; return; }
        StopAll();
        _toggled[t.Name] = true;
        StartTrick(t);
    }

    void StartTrick(Trick t)
    {
        _activeTrick = t.Name;
        Task.Run(() => RunTrick(t));
    }

    // ---------------------------------------------------------------- runner
    void RunTrick(Trick t)
    {
        string mode = EffMode(t);
        Func<bool> isActive = mode.ToLowerInvariant() switch
        {
            "hold" => () => Enabled && GameOk() && _activeTrick == t.Name && TriggerDown(t),
            "toggle" => () => Enabled && GameOk() && _activeTrick == t.Name && _toggled.TryGetValue(t.Name, out var v) && v,
            _ => () => Enabled && GameOk() && _activeTrick == t.Name,   // Tap
        };

        bool sprint = (t.SprintOverride < 0 ? _cfg.Sprinting : t.SprintOverride == 1) && _cfg.SprintVk >= 0;

        ReleaseAllMovement();
        if (sprint) Input.Down(_cfg.SprintVk);

        foreach (var step in t.Intro)
        {
            if (!isActive()) break;
            DoStep(step, isActive);
        }

        if (!mode.Equals("Tap", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Sustain.Count > 0)
            {
                while (isActive())
                    foreach (var step in t.Sustain)
                    {
                        if (!isActive()) break;
                        DoStep(step, isActive);
                    }
            }
            else
            {
                while (isActive()) Thread.Sleep(10);
            }
        }

        if (sprint) Input.Up(_cfg.SprintVk);
        ReleaseAllMovement();

        if (_activeTrick == t.Name) _activeTrick = "";
        _toggled[t.Name] = false;
    }

    void DoStep(Step step, Func<bool> isActive)
    {
        int ms = HumanMs(step.Ms);
        foreach (int vk in step.Vks) Input.Down(vk);
        InterruptibleSleep(ms, isActive);
        for (int i = step.Vks.Length - 1; i >= 0; i--) Input.Up(step.Vks[i]);

        if (_cfg.Humanize && _cfg.MaxGapMs > 0)
            InterruptibleSleep(_rng.Next(0, _cfg.MaxGapMs + 1), isActive);
    }

    int HumanMs(int ms)
    {
        int baseMs = ms < _cfg.MinStepMs ? _cfg.MinStepMs : ms;
        if (!_cfg.Humanize || _cfg.JitterPercent <= 0) return baseMs;
        int span = (int)Math.Round(baseMs * _cfg.JitterPercent / 100.0);
        int outMs = baseMs + _rng.Next(-span, span + 1);
        int floor = _cfg.MinStepMs > 20 ? _cfg.MinStepMs - 10 : 15;
        return outMs < floor ? floor : outMs;
    }

    void InterruptibleSleep(int ms, Func<bool> isActive)
    {
        long deadline = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < deadline)
        {
            if (!isActive()) return;
            int chunk = (int)Math.Min(10, deadline - Environment.TickCount64);
            if (chunk > 0) Thread.Sleep(chunk);
        }
    }

    void ReleaseAllMovement()
    {
        foreach (char c in new[] { 'F', 'B', 'L', 'R' })
            if (_cfg.KeyMap.TryGetValue(c, out int vk) && vk >= 0) Input.Up(vk);
    }

    bool GameOk()
    {
        if (!_cfg.OnlyWhenGameActive) return true;
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName.Equals(_cfg.GameProcess, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ---------------------------------------------------------------- actions
    public void ToggleMaster()
    {
        Enabled = !Enabled;
        if (!Enabled) StopAll();
        _notify("Moonwalk Pro " + (Enabled ? "ENABLED" : "DISABLED"));
    }

    public void PanicStop() { StopAll(); _notify("Panic stop - all inputs released."); }

    public void StopAll()
    {
        _activeTrick = "";
        foreach (var t in _cfg.Tricks) _toggled[t.Name] = false;
        if (_cfg.SprintVk >= 0) Input.Up(_cfg.SprintVk);
        ReleaseAllMovement();
    }

    public void DetectController()
    {
        if (!_cfg.JoyEnabled) { _notify("Controller support is disabled in config."); return; }
        _controller.Poll();
        var names = _controller.PressedNames();
        _notify(names.Count == 0
            ? "Hold a controller button, then press the Detect key again."
            : "Pressed: " + string.Join(", ", names) + "  (use as JoyButton)");
    }
}
