# Moonwalk Pro

A **native Windows tray app** for Dead by Daylight **survivor moonwalking** —
built for controller players. No AutoHotkey — it's a self-contained C#/.NET app. Tricks
are defined as step sequences in `config.ini`, so you can tune them or invent new
ones without touching code. Keyboard **and** Xbox-controller triggers, with
per-trick **Hold / Toggle / Tap** modes.

> ⚠️ **Use responsibly.** This automates the same movement inputs a player does by
> hand. It does **not** read game memory, see walls, or aim for you. Macros can
> still violate a game's Terms of Service, and Dead by Daylight's anti-cheat can
> detect external automation by its presence regardless of how human the timing
> looks. Treat it as a practice / custom-game tool, at your own risk.

---

## How the moonwalk actually works

A survivor's model turns to face whatever direction they're moving, so holding
S on its own just spins them round to face the camera and run off. The moonwalk
defeats that turn by **never letting it finish**:

```
S  ─────────────────────────────────────────────  held down, unbroken
A  ███████        ███████        ███████           130ms
D          ███████        ███████        ███████   130ms
```

The backward key stays **down the whole time** while A and D alternate on top of
it, back to back, with no pause. Each tap re-aims the turn before the previous
one can complete, so the model never rotates — it keeps facing forward while
sliding backwards. That's the whole trick.

The two things that break it, both of which this app got wrong before:

- **Any gap where neither A nor D is pressed.** Dead time lets the rotation
  finish and your survivor turns round and runs forward. The sustain patterns
  are therefore *only* alternating taps, never a dwell.
- **Releasing and re-pressing the backward key** between taps. That stutters the
  movement and hands the game the same opening. Hence the `Hold=` setting: those
  keys go down once at the start and are never touched again until you let go.

`130ms` per tap is the value the long-running community moonwalk tool
([iparamsh/MoonWalkScriptDBD](https://github.com/iparamsh/MoonWalkScriptDBD))
settles on, and it's the default here. That tool also makes the delay adjustable
because **ping** shifts it — so `PgUp` / `PgDn` nudge every tap ±5ms live and
`Home` resets. If you start creeping round mid-glide, that's the dial to turn.

Two more mechanics the tricks are built around:

- **Shift is the survivor *walk* key** in DbD. Run-speed moonwalks must keep it
  released (per-trick `Sprint=0`); holding it gives the walk-speed version.
- **Walking leaves no scratch marks** — so the walk-speed moonwalk doubles as a
  stealth mind-game at loops and line-of-sight breaks, not just style.

### The tricks

| D-pad | Trick | What it is |
|-------|-------|------------|
| **Down** | `Moonwalk` | **The** moonwalk — backward run held unbroken, A/D alternating at 130ms. Hold to glide, release to stop. |
| **Up** | `StealthMoonwalk` | Same thing at walk speed (holds Shift): slower, silent, **no scratch marks**. |
| **Left** | `DriftLeft` | One side gets a longer tap (180/90), so you slide left while still moonwalking — for peeling round a loop mid-glide. |
| **Right** | `DriftRight` | Mirror of the above. |

All four are **interchangeable**: each is just a `JoyButton=` line in
`config.ini`, so swap `DUp`/`DDown`/`DLeft`/`DRight` between sections (or move a
trick to any other pad button) and hit **Reload settings**.

There's also `RhythmOnly` (Numpad5, keyboard-only until you give it a
`JoyButton=`). It has **no** backward hold — *you* hold S yourself and it just
supplies the A/D rhythm, which is exactly what the original community tool does.
Use it when you want to steer and stop the backward movement by hand.

**Why this works while you play on controller:** DbD accepts keyboard and
controller input at the same time. You keep steering the camera with the right
stick as normal; the app sends the WASD pattern underneath. Note that the D-pad
still does whatever the game has bound to it — pick buttons the game isn't
using, or rebind them in DbD's own settings.

---

## 1. Get the app (nothing to install)

1. Go to the **Actions** tab of this repo.
2. Open the latest **“Build Moonwalk Pro (.exe)”** run.
3. Download the **`MoonwalkPro`** artifact (a zip) at the bottom.
4. Unzip and double-click **`MoonwalkPro.exe`**.

It's a single self-contained executable — **no .NET, no AutoHotkey, nothing to
install.** It runs in the **hidden-icons** area of the taskbar tray. On first
launch it writes `config.ini` next to itself.

## 2. Tray menu (the settings panel)

**Right-click the tray icon:**

| Item | Does |
|------|------|
| **Enabled (F8)** | Master on/off (checkmark shows state) |
| **Force activation mode ▸** | Make *every* trick Hold-all / Toggle-all, or use each trick's own setting |
| **Sprint while active** | Toggle sprint-hold |
| **Only when game focused** | Don't fire unless the game window is active |
| **Edit settings (config.ini)** | Opens config in Notepad |
| **Open folder / Reload settings** | Self-explanatory |
| **Detect controller button (F9)** | Press a pad button, this tells you its name |
| **Help / Exit** | — |

Changes from the menu are saved to `config.ini` automatically. Double-clicking the
tray icon toggles enable/disable.

## 3. System hotkeys

| Key | Action |
|-----|--------|
| `F8`  | Enable / disable all output |
| `F10` | Panic stop — release every key |
| `F9`  | Detect controller button (shows the name to bind) |
| `PgUp` / `PgDn` | Tempo ±5ms on every tap — **your ping dial** |
| `Home` | Reset tempo to the configured values |

## 4. The trick engine

Each trick has a **Hold** (keys pressed for its whole duration), an **Intro**
(played once) and a **Sustain** (looped while active). They share one token
language:

```
F = forward    B = backward    L = left    R = right    S = sprint/walk

One step  = DIR:MS        hold that direction for MS milliseconds   ->  L:130
Diagonal  = combine keys  press several at once                     ->  FL:120
Chain     = commas        run steps in order                        ->  L:130,R:130
```

`MS` is milliseconds (omit `:MS` for a 100ms default).

**`Hold=` is what makes a moonwalk a moonwalk.** Those keys go down once when the
trick starts and stay down until it ends — they are never released between steps,
and a step naming the same key won't re-press it. `Hold=B` with
`Sustain=L:130,R:130` is the core pattern: unbroken backward movement with
uninterrupted A/D alternation layered on top.

### Activation modes (`Mode=` per trick)

| Mode | Behavior |
|------|----------|
| `Hold` | Intro, then loops Sustain **while held**. Release = stop. |
| `Toggle` | Press to start, press again to stop. |
| `Tap` | Runs Intro once. Good for circle-strafes / quick jukes. |

## 5. Configuration (`config.ini`)

```ini
[General]
Sprinting=0                 ; hold Shift during tricks (Shift = survivor WALK in DbD)
OnlyWhenGameActive=0        ; only fire when the game window is focused
GameProcess=DeadByDaylight-Win64-Shipping.exe
ShowStatusGui=1             ; on-screen overlay
DefaultMode=                ; blank=per-trick | Hold | Toggle (force all)
Humanize=1                  ; natural timing variation
JitterPercent=6             ; +/- random variation per hold (small: rhythm matters)
MinStepMs=30                ; floor: no step ever shorter than this
MaxGapMs=2                  ; max random gap between steps (keep tiny)
MasterToggleKey=F8
PanicStopKey=F10
DetectControllerKey=F9
TempoUpKey=PgUp             ; live +5ms per tap
TempoDownKey=PgDn           ; live -5ms per tap
TempoResetKey=Home
TempoStepMs=5

[Movement]                  ; your in-game binds
Forward=w
Backward=s
Left=a
Right=d
Sprint=Shift

[Controller]
Enabled=1
PlayerIndex=1               ; XInput slot 1-4
PollRate=10

[Tricks]
List=Moonwalk,StealthMoonwalk,DriftLeft,DriftRight,RhythmOnly
```

Each name in `List` gets a section:

```ini
[Moonwalk]
Mode=Hold
Key=Numpad2                 ; keyboard trigger ("" to disable)
JoyButton=DDown             ; controller button name(s), comma-separated ("" to disable)
Sprint=0                    ; per-trick: 1=hold Shift, 0=never, omit=use [General] Sprinting
Hold=B                      ; held down for the whole trick, never released between steps
Intro=
Sustain=L:130,R:130
```

**Key names:** letters, digits, `Numpad0`–`Numpad9`, `F1`–`F24`, `Shift`, `Ctrl`,
`Alt`, `Space`, arrows, etc.
**Controller button names:** `A B X Y LB RB LT RT LS RS Back Start DUp DDown
DLeft DRight` (press `F9` to discover which is which).

### Add your own trick

1. Add a name to `List=`.
2. Add a `[YourTrick]` section with `Mode`, `Key`/`JoyButton`, optional `Hold`,
   `Intro` and `Sustain`.
3. Tray → **Reload settings**.

## 6. Timing realism & detection (read this)

The default 130ms tap comes from the community moonwalk tool that players have
been using for this exact tech. On top of that the engine keeps inputs
human-plausible:

- **`MinStepMs` floor** — no step is held shorter than ~30ms, so you can't
  configure a faster-than-human tap by mistake.
- **`Humanize` + `JitterPercent`** — every hold gets a small random ± variation,
  so it never sends the exact same robotic duration twice.
- **`MaxGapMs`** — small random gaps between presses instead of machine-clean,
  perfectly back-to-back input.

Inputs are sent as hardware-style **scan codes** (`SendInput`), which is what most
games actually read.

> **Honest caveat:** humanizing the *timing* makes the pattern look natural, but
> it does **not** make the tool invisible. Easy Anti-Cheat can detect external
> input/automation programs by their presence regardless of timing. Using this in
> public/ranked play can violate the game's ToS and carry a ban risk. Your call —
> the app just makes sure it isn't doing anything physically impossible.

## 7. Build it yourself (optional)

You don't need to — GitHub builds the exe. But if you want to:

```
dotnet publish MoonwalkPro.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Requires the .NET 8 SDK. Output: `publish/MoonwalkPro.exe`.

## 8. Troubleshooting

- **Survivor turns round and runs off mid-moonwalk** → that's ping. Tap `PgUp`
  (or `PgDn`) a few times while moonwalking until it holds; the overlay shows the
  current offset. If it never holds, check `Hold=B` is present on the trick — with
  no held backward key it can only wobble left and right on the spot.
- **It only wiggles left/right and doesn't go backwards** → same cause: `Hold=`
  is empty. That's intentional on `RhythmOnly`, where *you* hold S.
- **Nothing happens** → check the `F8` master state (overlay shows `ON/OFF`).
- **Keys feel stuck** → press `F10` (panic). Raise low per-step times.
- **Controller ignored** → `Enabled=1`, correct `PlayerIndex`, and use `F9` to
  confirm the button name.
- **Fires while typing** → set `OnlyWhenGameActive=1`.
- **Windows SmartScreen warning** → the exe is unsigned; “More info → Run anyway”.
  (Self-built/CI artifacts from open code; sign it yourself if you prefer.)
