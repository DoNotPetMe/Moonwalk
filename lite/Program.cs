using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MoonwalkLite;

/// <summary>
/// A faithful port of iparamsh/MoonWalkScriptDBD, triggered from an XInput controller.
///
/// The original is a single loop: while mouse4 is held, alternate A and D - 130ms
/// each, 1ms apart - forever, while YOU hold S yourself. That unbroken alternation
/// IS the moonwalk: every tap re-aims the turn before the previous one can finish,
/// so the survivor never rotates and slides backwards still facing forward.
///
/// Two things change here, and nothing else:
///   1. the trigger is a controller button (D-pad by default) instead of mouse4;
///   2. it can hold S for you, so the whole moonwalk runs off that one button.
/// Timings, ordering and the END/INSERT delay tuning are the original's.
/// </summary>
internal static class Program
{
    // Virtual-key codes, same ones the original script pokes.
    const int VK_A = 0x41, VK_D = 0x44, VK_S = 0x53;
    const int VK_MBUTTON = 0x04, VK_XBUTTON1 = 0x05;
    const int VK_END = 0x23, VK_INSERT = 0x2D, VK_F9 = 0x78, VK_F10 = 0x79;

    const int DefaultDelay = 130;

    static int _delay = DefaultDelay;
    static string _button = "DDown";
    static bool _holdBackward = true;
    static int _padIndex;
    static string _iniPath = "";

    static int Main()
    {
        Console.Title = "Moonwalk Lite";
        _iniPath = Path.Combine(AppContext.BaseDirectory, "MoonwalkLite.ini");
        LoadSettings();

        Banner();

        bool active = false;
        bool pEnd = false, pIns = false, pMid = false, pF9 = false, pF10 = false;

        while (true)
        {
            bool want = TriggerDown();

            if (want)
            {
                if (!active)
                {
                    active = true;
                    if (_holdBackward) Key.Down(VK_S);
                    Status("MOONWALKING");
                }

                // --- the original loop, verbatim in shape ---
                Key.Down(VK_A);
                if (!Wait(_delay)) { Release(ref active); continue; }
                Key.Up(VK_A);

                if (!Wait(1)) { Release(ref active); continue; }

                Key.Down(VK_D);
                if (!Wait(_delay)) { Release(ref active); continue; }
                Key.Up(VK_D);
            }
            else if (active)
            {
                Release(ref active);
            }
            else
            {
                Thread.Sleep(5);
            }

            // Delay tuning - END/INSERT/middle-click, as in the original.
            Edge(ref pEnd, VK_END, () => SetDelay(_delay + 5));
            Edge(ref pIns, VK_INSERT, () => SetDelay(_delay - 5));
            Edge(ref pMid, VK_MBUTTON, () => SetDelay(DefaultDelay));
            Edge(ref pF9, VK_F9, DetectButton);
            Edge(ref pF10, VK_F10, () => { Key.Up(VK_A); Key.Up(VK_D); Key.Up(VK_S); Status("panic - all keys released"); });
        }
    }

    // ------------------------------------------------------------------ trigger
    static bool TriggerDown()
    {
        if (Key.IsDown(VK_XBUTTON1)) return true;              // mouse4, like the original
        var state = Pad.Poll(_padIndex);
        return state.HasValue && Pad.IsDown(state.Value, _button);
    }

    /// <summary>Sleeps in small slices so letting go stops the moonwalk promptly.</summary>
    static bool Wait(int ms)
    {
        long end = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < end)
        {
            if (!TriggerDown()) return false;
            int slice = (int)Math.Min(5, end - Environment.TickCount64);
            if (slice > 0) Thread.Sleep(slice);
        }
        return true;
    }

    static void Release(ref bool active)
    {
        Key.Up(VK_A);
        Key.Up(VK_D);
        if (_holdBackward) Key.Up(VK_S);
        active = false;
        Status("ready");
    }

    static void Edge(ref bool prev, int vk, Action onPress)
    {
        bool now = Key.IsDown(vk);
        if (now && !prev) onPress();
        prev = now;
    }

    // ------------------------------------------------------------------ settings
    static void SetDelay(int ms)
    {
        _delay = Math.Clamp(ms, 40, 400);
        Save("Delay", _delay.ToString());
        Console.WriteLine($"  delay = {_delay}ms{(_delay == DefaultDelay ? "  (default)" : "")}");
    }

    static void DetectButton()
    {
        var state = Pad.Poll(_padIndex);
        if (state is null) { Console.WriteLine("  no controller found in slot " + (_padIndex + 1)); return; }

        var names = Pad.PressedNames(state.Value);
        if (names.Count == 0) { Console.WriteLine("  hold a pad button, then press F9 again"); return; }

        _button = names[0];
        Save("Button", _button);
        Console.WriteLine($"  trigger is now: {_button}  (saved)");
    }

    static void LoadSettings()
    {
        if (!File.Exists(_iniPath))
        {
            File.WriteAllText(_iniPath, string.Join(Environment.NewLine,
                "; Moonwalk Lite - settings",
                "; Button       pad button that starts the moonwalk. Press F9 in the app to",
                ";              set this by pressing the button you want.",
                ";              A B X Y LB RB LT RT LS RS Back Start DUp DDown DLeft DRight",
                "; Delay        ms per A/D tap. 130 is the original's default. END/INSERT",
                ";              nudge it live by 5ms; middle-click resets it.",
                "; HoldBackward 1 = the app holds S for you, so one button does everything.",
                ";              0 = you hold S yourself (exactly like the original script).",
                "; PlayerIndex  XInput slot, 1-4.",
                "",
                "Button=DDown",
                "Delay=130",
                "HoldBackward=1",
                "PlayerIndex=1",
                ""));
        }

        foreach (var raw in File.ReadAllLines(_iniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
            if (k.Equals("Button", StringComparison.OrdinalIgnoreCase) && v.Length > 0) _button = v;
            else if (k.Equals("Delay", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out int d)) _delay = Math.Clamp(d, 40, 400);
            else if (k.Equals("HoldBackward", StringComparison.OrdinalIgnoreCase)) _holdBackward = v != "0";
            else if (k.Equals("PlayerIndex", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out int p)) _padIndex = Math.Clamp(p - 1, 0, 3);
        }
    }

    static void Save(string key, string value)
    {
        try
        {
            var lines = new List<string>(File.ReadAllLines(_iniPath));
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith(";")) continue;
                int eq = lines[i].IndexOf('=');
                if (eq > 0 && lines[i].Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + "=" + value;
                    found = true;
                    break;
                }
            }
            if (!found) lines.Add(key + "=" + value);
            File.WriteAllLines(_iniPath, lines);
        }
        catch { /* running from a read-only folder is not worth crashing over */ }
    }

    // ------------------------------------------------------------------ console
    static void Banner()
    {
        Console.WriteLine("  Moonwalk Lite");
        Console.WriteLine("  ---------------------------------------------------------------");
        Console.WriteLine($"  hold {_button,-8} (or mouse4)   moonwalk");
        Console.WriteLine($"  holds S for you: {(_holdBackward ? "yes" : "no - hold S yourself")}");
        Console.WriteLine($"  delay: {_delay}ms per tap");
        Console.WriteLine();
        Console.WriteLine("  END / INSERT   delay +/- 5ms  (use this if ping turns you round)");
        Console.WriteLine("  middle-click   delay back to 130");
        Console.WriteLine("  F9             press a pad button to re-bind the trigger");
        Console.WriteLine("  F10            panic - release every key");
        Console.WriteLine();
        Console.WriteLine("  Do not push the left stick while moonwalking - stick and keyboard");
        Console.WriteLine("  movement fight each other. Steer with the right stick only.");
        Console.WriteLine("  ---------------------------------------------------------------");
        Console.WriteLine("  ready");
    }

    static void Status(string s) => Console.WriteLine("  " + s);
}

/// <summary>SendInput wrappers. Scan codes, which is what games read.</summary>
internal static class Key
{
    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;   // present only so INPUT is the OS-expected size
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion U; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);

    static readonly int Size = Marshal.SizeOf<INPUT>();

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    public static void Down(int vk) => Send(vk, false);
    public static void Up(int vk) => Send(vk, true);

    static void Send(int vk, bool up)
    {
        ushort scan = (ushort)MapVirtualKey((uint)vk, 0);
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scan,
                    dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0),
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                }
            }
        };
        SendInput(1, new[] { input }, Size);
    }
}

/// <summary>Minimal XInput reader.</summary>
internal static class Pad
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct State { public uint dwPacketNumber; public Gamepad Gamepad; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern int GetState14(int index, out State state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    static extern int GetState910(int index, out State state);

    const int TriggerThreshold = 60;

    static readonly Dictionary<string, ushort> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DUp"] = 0x0001, ["DDown"] = 0x0002, ["DLeft"] = 0x0004, ["DRight"] = 0x0008,
        ["Start"] = 0x0010, ["Back"] = 0x0020,
        ["LS"] = 0x0040, ["RS"] = 0x0080,
        ["LB"] = 0x0100, ["RB"] = 0x0200,
        ["A"] = 0x1000, ["B"] = 0x2000, ["X"] = 0x4000, ["Y"] = 0x8000,
    };

    static Func<int, State?>? _get;

    public static State? Poll(int index)
    {
        _get ??= Resolve();
        return _get(index);
    }

    static Func<int, State?> Resolve()
    {
        try { GetState14(0, out _); return i => GetState14(i, out var s) == 0 ? s : (State?)null; }
        catch (DllNotFoundException) { }
        try { GetState910(0, out _); return i => GetState910(i, out var s) == 0 ? s : (State?)null; }
        catch (DllNotFoundException) { }
        return _ => null;
    }

    public static bool IsDown(State s, string name)
    {
        name = name.Trim();
        if (name.Equals("LT", StringComparison.OrdinalIgnoreCase)) return s.Gamepad.bLeftTrigger > TriggerThreshold;
        if (name.Equals("RT", StringComparison.OrdinalIgnoreCase)) return s.Gamepad.bRightTrigger > TriggerThreshold;
        return Buttons.TryGetValue(name, out ushort flag) && (s.Gamepad.wButtons & flag) != 0;
    }

    public static List<string> PressedNames(State s)
    {
        var list = new List<string>();
        foreach (var kv in Buttons)
            if ((s.Gamepad.wButtons & kv.Value) != 0) list.Add(kv.Key);
        if (s.Gamepad.bLeftTrigger > TriggerThreshold) list.Add("LT");
        if (s.Gamepad.bRightTrigger > TriggerThreshold) list.Add("RT");
        return list;
    }
}
