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
    public int SprintOverride = -1;        // -1 = inherit [General] Sprinting, 0 = off, 1 = on
    public int[] Hold = Array.Empty<int>(); // held down for the whole trick, never released between steps
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
    public int TempoUpVk, TempoDownVk, TempoResetVk, TempoStepMs;

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

        c.Sprinting = ini.GetBool("General", "Sprinting", false);
        c.OnlyWhenGameActive = ini.GetBool("General", "OnlyWhenGameActive", false);
        c.GameProcess = ini.Get("General", "GameProcess", "DeadByDaylight-Win64-Shipping.exe")
                           .Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();
        c.ShowStatusGui = ini.GetBool("General", "ShowStatusGui", true);
        c.ModeOverride = ini.Get("General", "DefaultMode", "").Trim();
        c.Humanize = ini.GetBool("General", "Humanize", true);
        c.JitterPercent = ini.GetInt("General", "JitterPercent", 6);
        c.MinStepMs = ini.GetInt("General", "MinStepMs", 30);
        c.MaxGapMs = ini.GetInt("General", "MaxGapMs", 2);
        c.MasterToggleVk = Keys.Vk(ini.Get("General", "MasterToggleKey", "F8"));
        c.PanicStopVk = Keys.Vk(ini.Get("General", "PanicStopKey", "F10"));
        c.DetectVk = Keys.Vk(ini.Get("General", "DetectControllerKey", "F9"));
        c.TempoUpVk = Keys.Vk(ini.Get("General", "TempoUpKey", "PgUp"));
        c.TempoDownVk = Keys.Vk(ini.Get("General", "TempoDownKey", "PgDn"));
        c.TempoResetVk = Keys.Vk(ini.Get("General", "TempoResetKey", "Home"));
        c.TempoStepMs = Math.Max(1, ini.GetInt("General", "TempoStepMs", 5));

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
                SprintOverride = ini.Get(name, "Sprint", "").Trim() switch { "0" => 0, "1" => 1, _ => -1 },
                Hold = ParseKeys(ini.Get(name, "Hold", ""), c.KeyMap),
                Intro = ParseSteps(ini.Get(name, "Intro", ""), c.KeyMap),
                Sustain = ParseSteps(ini.Get(name, "Sustain", ""), c.KeyMap),
            };
            c.Tricks.Add(t);
        }
        return c;
    }

    /// <summary>Turns a token like "B" or "BL" into the virtual-keys it names.</summary>
    static int[] ParseKeys(string letters, Dictionary<char, int> keyMap)
    {
        var vks = new List<int>();
        foreach (char ch in letters.Trim().ToUpperInvariant())
            if (keyMap.TryGetValue(ch, out int vk) && vk >= 0 && !vks.Contains(vk))
                vks.Add(vk);
        return vks.ToArray();
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
        ;  HOW A MOONWALK IS BUILT
        ;    Hold=B      -> S is pressed once and stays DOWN for the whole trick.
        ;    Sustain=... -> A / D alternate on top of that unbroken backward hold.
        ;  The alternation must never pause: any moment where neither A nor D is
        ;  pressed gives the turn animation time to finish, and your survivor spins
        ;  round and runs off. That is why the sustain is only L and R, back to back.
        ;
        ;  Sequence tokens (Intro / Sustain / Hold):
        ;     F=forward  B=backward  L=left  R=right  S=sprint(walk)
        ;     One step  = DIR:MS   (ms to hold)        e.g.  L:130
        ;     Diagonals = combine letters              e.g.  FL:120  (fwd+left)
        ;     Chain steps with commas                  e.g.  L:130,R:130
        ;  Intro   = played once.   Sustain = looped while held.
        ; ============================================================================

        [General]
        ; Hold the [Movement] Sprint key during tricks? 1/0. NOTE: in DbD, Shift is the
        ; survivor WALK key - holding it slows you to walking speed. Leave this 0 for
        ; run-speed tricks; individual tricks can override with their own Sprint= line.
        Sprinting=0
        ; Only act while the game window is focused? 1/0
        OnlyWhenGameActive=0
        GameProcess=DeadByDaylight-Win64-Shipping.exe
        ; Small on-screen status overlay: 1/0
        ShowStatusGui=1
        ; Force a mode on every (non-Tap) trick: blank=per-trick | Hold | Toggle.
        ; Also switchable live from the tray menu.
        DefaultMode=

        ; --- Timing ---
        ; Jitter is deliberately small: the moonwalk depends on an even rhythm, and a
        ; sloppy one lets the survivor turn.
        Humanize=1
        JitterPercent=6
        ; No step is ever held shorter than this (ms) - avoids impossible taps.
        MinStepMs=30
        ; Max random gap inserted between steps (ms). Keep tiny - a gap is dead time
        ; where nothing is pressed and the survivor can start turning.
        MaxGapMs=2

        MasterToggleKey=F8
        PanicStopKey=F10
        DetectControllerKey=F9

        ; --- Live tempo tuning (press these mid-match) ---
        ; If ping makes you creep round mid-moonwalk, nudge every tap up or down a few
        ; ms until it holds. This is the one setting worth tuning to your connection.
        TempoUpKey=PgUp
        TempoDownKey=PgDn
        TempoResetKey=Home
        TempoStepMs=5

        [Movement]
        Forward=w
        Backward=s
        Left=a
        Right=d
        ; In DbD this is the survivor Walk key (Shift). Held only when Sprinting=1
        ; globally or a trick sets Sprint=1.
        Sprint=Shift

        [Controller]
        Enabled=1
        ; Player slot 1-4 (XInput). Most setups = 1.
        PlayerIndex=1
        ; Poll interval in ms.
        PollRate=10

        ; Button names: A B X Y LB RB LT RT LS RS Back Start DUp DDown DLeft DRight
        ; (comma-separate to bind several). Press the Detect key to discover names.
        ; The moonwalks default to the D-pad - swap the JoyButton= lines between
        ; sections to rearrange them however you like.

        [Tricks]
        List=Moonwalk,StealthMoonwalk,DriftLeft,DriftRight,RhythmOnly

        ; --- D-pad Down: THE moonwalk ------------------------------------------------
        ; S held down continuously while A and D alternate at 130ms each. 130 is the
        ; value the long-running community moonwalk tool settles on; if you get turned
        ; round, nudge it live with PgUp/PgDn rather than editing this.
        [Moonwalk]
        Mode=Hold
        Key=Numpad2
        JoyButton=DDown
        Sprint=0
        Hold=B
        Intro=
        Sustain=L:130,R:130

        ; --- D-pad Up: stealth (walking) moonwalk ------------------------------------
        ; Same thing at WALK speed (holds Shift). Walking leaves no scratch marks, so
        ; this is the mind-game version. Slower movement turns slower, so the taps can
        ; be a little longer.
        [StealthMoonwalk]
        Mode=Hold
        Key=Numpad8
        JoyButton=DUp
        Sprint=1
        Hold=B
        Intro=
        Sustain=L:150,R:150

        ; --- D-pad Left / Right: drifting moonwalks ----------------------------------
        ; Same unbroken backward hold, but one side gets a longer tap, so you slide
        ; that way while still moonwalking. For peeling round a loop mid-glide.
        [DriftLeft]
        Mode=Hold
        Key=Numpad4
        JoyButton=DLeft
        Sprint=0
        Hold=B
        Intro=
        Sustain=L:180,R:90

        [DriftRight]
        Mode=Hold
        Key=Numpad6
        JoyButton=DRight
        Sprint=0
        Hold=B
        Intro=
        Sustain=L:90,R:180

        ; --- Assist mode (keyboard only by default; add a JoyButton to pad-bind) -----
        ; No backward hold - YOU hold S yourself and this just supplies the A/D rhythm.
        ; This is exactly what the original community moonwalk tool does. Use it if you
        ; want to control the backward movement (and stop it) by hand.
        [RhythmOnly]
        Mode=Hold
        Key=Numpad5
        JoyButton=
        Sprint=0
        Hold=
        Intro=
        Sustain=L:130,R:130

        """;
}
