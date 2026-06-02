using System;
using System.Collections.Generic;
using System.IO;

namespace MoonwalkPro;

internal sealed class Step
{
    public int[] Vks = Array.Empty<int>();
    public int Ms = 100;
}

internal sealed class Trick
{
    public string Name = "";
    public string Mode = "Hold";          // Hold | Toggle | Tap
    public int KeyVk = -1;                 // keyboard trigger virtual-key (-1 = none)
    public string[] JoyButtons = Array.Empty<string>();
    public List<Step> Intro = new();
    public List<Step> Sustain = new();
}

internal sealed class Config
{
    public string Path = "";
    public Ini Ini = null!;

    // General
    public bool Sprinting, OnlyWhenGameActive, ShowStatusGui, Humanize;
    public string GameProcess = "";
    public string ModeOverride = "";       // "" = per-trick, else Hold/Toggle
    public int JitterPercent, MinStepMs, MaxGapMs;
    public int MasterToggleVk, PanicStopVk, DetectVk;

    // Movement
    public Dictionary<char, int> KeyMap = new();
    public int SprintVk = -1;

    // Controller
    public bool JoyEnabled;
    public int JoyIndex;
    public int PollRate = 10;

    public List<Trick> Tricks = new();

    public static Config Load(string path)
    {
        var ini = new Ini(path);
        var c = new Config { Path = path, Ini = ini };

        c.Sprinting = ini.GetBool("General", "Sprinting", true);
        c.OnlyWhenGameActive = ini.GetBool("General", "OnlyWhenGameActive", false);
        c.GameProcess = ini.Get("General", "GameProcess", "DeadByDaylight-Win64-Shipping.exe")
                           .Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();
        c.ShowStatusGui = ini.GetBool("General", "ShowStatusGui", true);
        c.ModeOverride = ini.Get("General", "DefaultMode", "").Trim();
        c.Humanize = ini.GetBool("General", "Humanize", true);
        c.JitterPercent = ini.GetInt("General", "JitterPercent", 15);
        c.MinStepMs = ini.GetInt("General", "MinStepMs", 30);
        c.MaxGapMs = ini.GetInt("General", "MaxGapMs", 10);
        c.MasterToggleVk = Keys.Vk(ini.Get("General", "MasterToggleKey", "F8"));
        c.PanicStopVk = Keys.Vk(ini.Get("General", "PanicStopKey", "F10"));
        c.DetectVk = Keys.Vk(ini.Get("General", "DetectControllerKey", "F9"));

        int f = Keys.Vk(ini.Get("Movement", "Forward", "w"));
        int b = Keys.Vk(ini.Get("Movement", "Backward", "s"));
        int l = Keys.Vk(ini.Get("Movement", "Left", "a"));
        int r = Keys.Vk(ini.Get("Movement", "Right", "d"));
        c.SprintVk = Keys.Vk(ini.Get("Movement", "Sprint", "Shift"));
        c.KeyMap = new Dictionary<char, int> { ['F'] = f, ['B'] = b, ['L'] = l, ['R'] = r, ['S'] = c.SprintVk };

        c.JoyEnabled = ini.GetBool("Controller", "Enabled", true);
        c.JoyIndex = ini.GetInt("Controller", "PlayerIndex", 1) - 1;
        c.PollRate = Math.Max(5, ini.GetInt("Controller", "PollRate", 10));

        foreach (var name in ini.Get("Tricks", "List", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = new Trick
            {
                Name = name,
                Mode = ini.Get(name, "Mode", "Hold").Trim(),
                KeyVk = Keys.Vk(ini.Get(name, "Key", "")),
                JoyButtons = ini.Get(name, "JoyButton", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Intro = ParseSteps(ini.Get(name, "Intro", ""), c.KeyMap),
                Sustain = ParseSteps(ini.Get(name, "Sustain", ""), c.KeyMap),
            };
            c.Tricks.Add(t);
        }
        return c;
    }

    static List<Step> ParseSteps(string seq, Dictionary<char, int> keyMap)
    {
        var steps = new List<Step>();
        foreach (var token in seq.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            string letters = parts[0].Trim();
            int ms = parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int v) ? v : 100;

            var vks = new List<int>();
            foreach (char ch in letters.ToUpperInvariant())
                if (keyMap.TryGetValue(ch, out int vk) && vk >= 0)
                    vks.Add(vk);

            if (vks.Count > 0)
                steps.Add(new Step { Vks = vks.ToArray(), Ms = ms });
        }
        return steps;
    }

    public Trick? Find(string name) => Tricks.Find(t => t.Name == name);

    public static void EnsureDefault(string path)
    {
        if (File.Exists(path)) return;
        File.WriteAllText(path, DefaultText);
    }

    public const string DefaultText = """
        ; ============================================================================
        ;  Moonwalk Pro - configuration  (native Windows app, no AutoHotkey)
        ; ----------------------------------------------------------------------------
        ;  Sequence tokens (Intro / Sustain):
        ;     F=forward  B=backward  L=left  R=right  S=sprint
        ;     One step  = DIR:MS   (ms to hold)        e.g.  L:200
        ;     Diagonals = combine letters              e.g.  FL:120  (fwd+left)
        ;     Chain steps with commas                  e.g.  L:200,B:300,L:200,F:300
        ;  Intro   = played once.
        ;  Sustain = looped while held (Hold) or until re-pressed (Toggle).
        ; ============================================================================

        [General]
        ; Hold the sprint key for the whole trick? 1/0
        Sprinting=1
        ; Only act while the game window is focused? 1/0
        OnlyWhenGameActive=0
        GameProcess=DeadByDaylight-Win64-Shipping.exe
        ; Small on-screen status overlay: 1/0
        ShowStatusGui=1
        ; Force a mode on every (non-Tap) trick: blank=per-trick | Hold | Toggle.
        ; Also switchable live from the tray menu.
        DefaultMode=

        ; --- Human-plausible timing (keeps inputs from looking robotic) ---
        Humanize=1
        JitterPercent=15
        ; No step is ever held shorter than this (ms) - avoids impossible taps.
        MinStepMs=30
        ; Max random gap inserted between steps (ms). 0 = none.
        MaxGapMs=10

        MasterToggleKey=F8
        PanicStopKey=F10
        DetectControllerKey=F9

        [Movement]
        Forward=w
        Backward=s
        Left=a
        Right=d
        Sprint=Shift

        [Controller]
        Enabled=1
        ; Player slot 1-4 (XInput). Most setups = 1.
        PlayerIndex=1
        ; Poll interval in ms.
        PollRate=10

        ; Button names: A B X Y LB RB LT RT LS RS Back Start DUp DDown DLeft DRight
        ; (comma-separate to bind several). Press the Detect key to discover names.

        [Tricks]
        List=MoonwalkBackward,MoonwalkForward,CircleStrafe,QuickJuke

        [MoonwalkBackward]
        Mode=Hold
        Key=Numpad3
        JoyButton=LB
        Intro=L:200,B:300,L:200,F:300
        Sustain=L:60,R:60

        [MoonwalkForward]
        Mode=Hold
        Key=Numpad2
        JoyButton=RB
        Intro=F:200,L:300,B:400
        Sustain=L:80,R:80

        ; Circle-strafe juke. WASD only strafes relative to the camera - a true
        ; camera 360 needs the mouse. This walks a quick circle to bait a swing.
        [CircleStrafe]
        Mode=Tap
        Key=Numpad1
        JoyButton=Y
        Intro=L:90,FL:90,F:90,FR:90,R:90,BR:90,B:90,BL:90
        Sustain=

        [QuickJuke]
        Mode=Tap
        Key=Numpad0
        JoyButton=X
        Intro=L:120,R:120
        Sustain=

        """;
}
