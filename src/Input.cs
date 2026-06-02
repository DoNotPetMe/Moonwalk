using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoonwalkPro;

/// <summary>Native key sending (SendInput, scan codes) and key-state reading.</summary>
internal static class Input
{
    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_SCANCODE = 0x0008;
    const uint MAPVK_VK_TO_VSC = 0;

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

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint uCode, uint uMapType);

    static readonly int InputSize = Marshal.SizeOf<INPUT>();

    // Keys whose scan codes must carry the "extended" flag to be read correctly.
    static readonly HashSet<int> Extended = new()
    {
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // PgUp/PgDn/End/Home/arrows
        0x2D, 0x2E,                                     // Insert/Delete
        0xA3, 0xA5,                                     // RControl/RAlt
        0x6F, 0x90,                                     // Numpad-Divide / NumLock
    };

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public static void Down(int vk) => SendScan(vk, false);
    public static void Up(int vk) => SendScan(vk, true);

    static void SendScan(int vk, bool up)
    {
        ushort scan = (ushort)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
        uint flags = KEYEVENTF_SCANCODE;
        if (Extended.Contains(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        if (up) flags |= KEYEVENTF_KEYUP;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero } }
        };
        SendInput(1, new[] { input }, InputSize);
    }
}
