using System;
using System.Collections.Generic;

namespace MoonwalkPro;

/// <summary>Maps friendly key names from config.ini to Windows virtual-key codes.</summary>
internal static class Keys
{
    static readonly Dictionary<string, int> Map = Build();

    public static int Vk(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        name = name.Trim();
        return Map.TryGetValue(name.ToLowerInvariant(), out int vk) ? vk : -1;
    }

    static Dictionary<string, int> Build()
    {
        var m = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (char c = 'a'; c <= 'z'; c++) m[c.ToString()] = char.ToUpper(c);      // 0x41-0x5A
        for (char d = '0'; d <= '9'; d++) m[d.ToString()] = d;                    // 0x30-0x39
        for (int n = 0; n <= 9; n++) m["numpad" + n] = 0x60 + n;                  // 0x60-0x69
        for (int f = 1; f <= 24; f++) m["f" + f] = 0x6F + f;                      // F1=0x70 ... F24=0x87

        m["numpadmult"] = 0x6A; m["numpadadd"] = 0x6B; m["numpadsub"] = 0x6D;
        m["numpaddot"] = 0x6E; m["numpaddiv"] = 0x6F;

        m["shift"] = 0x10; m["lshift"] = 0xA0; m["rshift"] = 0xA1;
        m["ctrl"] = 0x11; m["control"] = 0x11; m["lctrl"] = 0xA2; m["rctrl"] = 0xA3;
        m["alt"] = 0x12; m["lalt"] = 0xA4; m["ralt"] = 0xA5;

        m["space"] = 0x20; m["enter"] = 0x0D; m["return"] = 0x0D;
        m["tab"] = 0x09; m["esc"] = 0x1B; m["escape"] = 0x1B; m["backspace"] = 0x08;
        m["capslock"] = 0x14;

        m["left"] = 0x25; m["up"] = 0x26; m["right"] = 0x27; m["down"] = 0x28;
        m["ins"] = 0x2D; m["insert"] = 0x2D; m["del"] = 0x2E; m["delete"] = 0x2E;
        m["home"] = 0x24; m["end"] = 0x23; m["pgup"] = 0x21; m["pgdn"] = 0x22;

        // Mouse side/extra buttons can be read (not sent) for triggering:
        m["xbutton1"] = 0x05; m["xbutton2"] = 0x06; m["mbutton"] = 0x04;

        return m;
    }
}
