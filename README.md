# Moonwalk Pro

A clean, professional rewrite of the classic Dead by Daylight moonwalk macro.
It's a **data-driven input automation script**: tricks are defined as step
sequences in `config.ini`, so you can tune existing tricks or invent new ones
without ever touching the code. Keyboard **and** controller triggers, with
per-trick **Hold / Toggle / Tap** activation modes.

> ⚠️ **Use responsibly.** This automates the same movement inputs a player
> performs by hand (moonwalking, jukes, 360s). It does **not** read game
> memory, see through walls, or give aim/wall hacks. Macros can still violate a
> game's Terms of Service — use it for practice/personal play and at your own
> risk.

---

## 1. Get the compiled app (no AutoHotkey install needed)

Just like the original, you can run this as a **single tray app** — no need to
download or learn AutoHotkey. GitHub builds the `.exe` for you:

1. Go to the **Actions** tab of this repository.
2. Open the most recent **“Build Moonwalk Pro (.exe)”** run.
3. At the bottom, download the **`MoonwalkPro`** artifact (a zip).
4. Unzip it and double-click **`MoonwalkPro.exe`**.

It runs in the **hidden-icons area** of the taskbar tray. **Right-click the tray
icon** for the settings menu (enable/disable, force Hold/Toggle, sprint, edit
config, reload, etc.). On first launch it creates `config.ini` beside the exe.

> Prefer building it yourself? Install AutoHotkey v2 and run `build.bat`, or just
> run the raw script (below).

## 2. Run the raw script instead (optional)

- Requires **AutoHotkey v2.0+** from <https://www.autohotkey.com/> (the old MPGH
  script was v1 and is **not** compatible).
- A controller is optional — any XInput/DirectInput pad Windows recognizes works.

1. Install AutoHotkey v2.
2. Put `moonwalk.ahk` in a folder; double-click it.
3. First run auto-creates `config.ini` with sensible defaults; a small status
   overlay appears and the tray icon holds the settings menu.

## 2a. Tray menu (the settings panel)

Right-click the tray icon — same in the compiled exe and the raw script:

| Item | Does |
|------|------|
| **Enabled (F8)** | Master on/off (checkmark shows state) |
| **Force activation mode ▸** | Make *every* trick Hold-all / Toggle-all, or use each trick's own setting. This is the one-click equivalent of the original's separate “hold” and “toggle” builds. |
| **Sprint while active** | Toggle sprint-hold on/off |
| **Only when game focused** | Don't fire unless the game window is active |
| **Edit settings (config.ini)** | Opens the config in Notepad |
| **Open script folder** / **Reload settings** / **Help** / **Exit** | Self-explanatory |

Changes made from the tray are saved to `config.ini` automatically.

## 3. System hotkeys (default)

| Key  | Action |
|------|--------|
| `F8`  | Master toggle — enable/disable all output |
| `F10` | Panic stop — instantly release every key |
| `F9`  | Detect controller button — hold a pad button, press F9, it tells you the number |

## 4. The trick engine

Every trick has up to two sequences: an **Intro** (played once) and a
**Sustain** (looped while active). Both use the same tiny token language:

```
F = forward    B = backward    L = left    R = right    S = sprint

One step  = DIR:MS        hold that direction for MS milliseconds   ->  L:200
Diagonal  = combine keys  press several at once                     ->  FL:120
Chain     = commas        run steps in order                        ->  L:200,B:300,L:200,F:300
```

`MS` is milliseconds. If you omit `:MS` a step defaults to 100ms.

### Activation modes (`Mode=` per trick)

| Mode     | Behavior |
|----------|----------|
| `Hold`   | Runs Intro, then loops Sustain **while you hold** the trigger. Release = stop. |
| `Toggle` | Press once to start (Intro + Sustain loop), press again to stop. |
| `Tap`    | Runs Intro once and finishes. Good for 360s / quick jukes. |

If `Sprinting=1`, the sprint key is held down for the entire duration of any
trick and released automatically when it ends.

## 5. Configuration reference (`config.ini`)

```ini
[General]
SendMode=Event              ; Event (most compatible) | Input (fastest) | Play
Sprinting=1                 ; hold sprint during tricks: 1/0
OnlyWhenGameActive=0        ; only fire when the game window is focused: 1/0
GameProcess=DeadByDaylight-Win64-Shipping.exe
ShowStatusGui=1             ; on-screen overlay: 1/0
MasterToggleKey=F8
PanicStopKey=F10
DetectControllerKey=F9

[Movement]                  ; your in-game movement binds
Forward=w
Backward=s
Left=a
Right=d
Sprint=Shift

[Controller]
Enabled=1
JoyID=1                     ; joystick number (use F9 if unsure)
PollRate=10                 ; ms between controller polls

[Tricks]
List=MoonwalkBackward,MoonwalkForward,Spin360,QuickJuke
```

Each name in `List` gets its own section:

```ini
[MoonwalkBackward]
Mode=Hold
Key=Numpad3                 ; keyboard trigger ("" to disable)
JoyButton=5                 ; controller button number (0 to disable)
Intro=L:200,B:300,L:200,F:300
Sustain=L:60,R:60
```

Hotkey names follow AutoHotkey syntax:
<https://www.autohotkey.com/docs/v2/KeyList.htm>

## 6. Adding your own trick

1. Add a name to `List=` in `[Tricks]`.
2. Add a matching `[YourTrickName]` section with `Mode`, `Key`/`JoyButton`,
   `Intro`, and optional `Sustain`.
3. Save and **Reload** (tray menu or restart the script).

Example — a "spin then drift backward" combo on Toggle:

```ini
[Tricks]
List=MoonwalkBackward,MoonwalkForward,Spin360,QuickJuke,SpinDrift

[SpinDrift]
Mode=Toggle
Key=Numpad4
JoyButton=0
Intro=R:300,B:300,L:300,F:300
Sustain=L:70,R:70
```

## 7. Controller setup

1. Set `[Controller] Enabled=1`.
2. Find your pad number: hold a button, press `F9`. The tooltip shows the
   joystick button number and reminds you of the `JoyID`.
3. Put that number in each trick's `JoyButton=`.
4. Set `JoyID=` if you have more than one device (default `1`).

Polling means Hold and Toggle both work correctly from the controller, not just
single presses.

## 8. Tuning tips

- **Stuttering / not registering in-game?** Try `SendMode=Input`. If a step
  feels dropped, nudge its `MS` up by 10–20ms.
- **Moonwalk "drops" too early?** Lower the Sustain tap times (e.g. `L:50,R:50`).
- **Too twitchy?** Raise Sustain tap times.
- **Diagonals** (`FL`, `BR`, …) give smoother, more believable juke arcs than
  pure cardinal taps.
- Keep `OnlyWhenGameActive=1` so the macro never fires while you're typing.

## 9. Mapping to the old MPGH settings

The original script's numbered `Time1..Time11` are just hold durations. They map
directly onto the new token sequences:

| Old | Meaning | New equivalent |
|-----|---------|----------------|
| Hotkey1–4 | walk F/B/L/R | `[Movement]` Forward/Backward/Left/Right |
| Hotkey5   | sprint | `[Movement] Sprint` + `Sprinting=1` |
| Hotkey6/7 | activate back/forward moonwalk | `[MoonwalkBackward] Key` / `[MoonwalkForward] Key` |
| Time1–4   | backward-moonwalk intro holds | `[MoonwalkBackward] Intro=L:200,B:300,L:200,F:300` |
| Time5/6   | backward sustain taps | `[MoonwalkBackward] Sustain=L:60,R:60` |
| Time7–9   | forward-moonwalk intro holds | `[MoonwalkForward] Intro=F:200,L:300,B:400` |
| Time10/11 | forward sustain taps | `[MoonwalkForward] Sustain=L:80,R:80` |

Everything the old script did is reproducible, plus diagonals, controller
support, Toggle/Tap modes, panic stop, and unlimited custom tricks.

## 10. Troubleshooting

- **Nothing happens** → Check `F8` master state (overlay shows `ON/OFF`), and
  that AutoHotkey **v2** is installed (v1 will throw syntax errors).
- **Keys get "stuck" down** → press `F10` (panic stop). Lower per-step times.
- **Controller ignored** → `Enabled=1`, correct `JoyID`, and use `F9` to confirm
  the button number.
- **Fires while typing** → set `OnlyWhenGameActive=1`.
