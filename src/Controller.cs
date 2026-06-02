using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoonwalkPro;

/// <summary>Reads an Xbox/XInput controller and resolves friendly button names.</summary>
internal sealed class Controller
{
    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern int XInputGetState14(int index, out XINPUT_STATE state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    static extern int XInputGetState910(int index, out XINPUT_STATE state);

    const int TriggerThreshold = 60;

    static readonly Dictionary<string, ushort> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DUp"] = 0x0001, ["DDown"] = 0x0002, ["DLeft"] = 0x0004, ["DRight"] = 0x0008,
        ["Start"] = 0x0010, ["Back"] = 0x0020, ["Select"] = 0x0020,
        ["LS"] = 0x0040, ["L3"] = 0x0040, ["RS"] = 0x0080, ["R3"] = 0x0080,
        ["LB"] = 0x0100, ["RB"] = 0x0200,
        ["A"] = 0x1000, ["B"] = 0x2000, ["X"] = 0x4000, ["Y"] = 0x8000,
    };

    readonly Func<int, XINPUT_STATE?> _get;
    readonly int _index;
    ushort _buttons;
    byte _lt, _rt;

    public bool Connected { get; private set; }

    public Controller(int playerIndex)
    {
        _index = Math.Clamp(playerIndex, 0, 3);
        _get = MakeGetter();
    }

    static Func<int, XINPUT_STATE?> MakeGetter()
    {
        try { XInputGetState14(0, out _); return i => XInputGetState14(i, out var s) == 0 ? s : (XINPUT_STATE?)null; }
        catch (DllNotFoundException) { }
        try { XInputGetState910(0, out _); return i => XInputGetState910(i, out var s) == 0 ? s : (XINPUT_STATE?)null; }
        catch (DllNotFoundException) { }
        return _ => null;   // no XInput available on this machine
    }

    public void Poll()
    {
        var s = _get(_index);
        if (s.HasValue)
        {
            Connected = true;
            _buttons = s.Value.Gamepad.wButtons;
            _lt = s.Value.Gamepad.bLeftTrigger;
            _rt = s.Value.Gamepad.bRightTrigger;
        }
        else
        {
            Connected = false;
            _buttons = 0; _lt = 0; _rt = 0;
        }
    }

    public bool IsDown(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        if (name.Equals("LT", StringComparison.OrdinalIgnoreCase)) return _lt > TriggerThreshold;
        if (name.Equals("RT", StringComparison.OrdinalIgnoreCase)) return _rt > TriggerThreshold;
        if (name.StartsWith("Dpad", StringComparison.OrdinalIgnoreCase)) name = "D" + name.Substring(4);
        return Buttons.TryGetValue(name, out ushort flag) && (_buttons & flag) != 0;
    }

    public List<string> PressedNames()
    {
        var list = new List<string>();
        foreach (var kv in Buttons)
            if ((_buttons & kv.Value) != 0 && !list.Contains(kv.Key))
                list.Add(kv.Key);
        if (_lt > TriggerThreshold) list.Add("LT");
        if (_rt > TriggerThreshold) list.Add("RT");
        return list;
    }
}
