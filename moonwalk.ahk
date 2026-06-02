#Requires AutoHotkey v2.0
#SingleInstance Force
;==============================================================================
;  Moonwalk Pro  -  Movement-tech macro for Dead by Daylight
;------------------------------------------------------------------------------
;  A data-driven, fully configurable input macro. Tricks are defined as step
;  sequences in config.ini using a small token language, so you can build new
;  tricks without editing this file. Keyboard AND controller triggers, with
;  per-trick Hold / Toggle / Tap activation modes.
;
;  See README.md for the full guide. Quick legend for sequence tokens:
;     F = forward   B = backward   L = left   R = right   S = sprint
;     Tokens look like  DIR:MS  (milliseconds to hold), e.g.  L:200
;     Combine letters for simultaneous presses (diagonals):  FL:120
;     Separate steps with commas:  L:200,B:300,L:200,F:300
;==============================================================================

;----------------------------------------------------------------------------
;  Globals / state
;----------------------------------------------------------------------------
global CONFIG_FILE := A_ScriptDir "\config.ini"
global G := {}                 ; general settings
global KeyMap := Map()         ; F/B/L/R/S -> physical key
global Tricks := Map()         ; name -> trick definition object
global ActiveTrick := ""       ; name of the trick currently running ("" = none)
global Toggled := Map()        ; name -> bool, for Toggle-mode tricks
global Enabled := true         ; master on/off (output gate)
global JoyPrev := Map()        ; controller edge-detection state
global StatusGui := ""

;----------------------------------------------------------------------------
;  Boot
;----------------------------------------------------------------------------
EnsureConfig()
LoadConfig()
BuildHotkeys()
BuildStatusGui()
SetupController()
UpdateStatus()
TrayTip("Moonwalk Pro loaded. F8 = master toggle, F9 = detect controller button.", "Moonwalk Pro", 1)
return

;============================================================================
;  CONFIG
;============================================================================
EnsureConfig() {
    if FileExist(CONFIG_FILE)
        return
    FileAppend(DefaultConfig(), CONFIG_FILE, "UTF-8")
}

LoadConfig() {
    global
    ; --- General ---
    G.SendMode            := IniRead(CONFIG_FILE, "General", "SendMode", "Event")
    G.Sprinting           := Integer(IniRead(CONFIG_FILE, "General", "Sprinting", "1"))
    G.OnlyWhenGameActive  := Integer(IniRead(CONFIG_FILE, "General", "OnlyWhenGameActive", "0"))
    G.GameProcess         := IniRead(CONFIG_FILE, "General", "GameProcess", "DeadByDaylight-Win64-Shipping.exe")
    G.ShowStatusGui       := Integer(IniRead(CONFIG_FILE, "General", "ShowStatusGui", "1"))
    G.MasterToggleKey     := IniRead(CONFIG_FILE, "General", "MasterToggleKey", "F8")
    G.PanicStopKey        := IniRead(CONFIG_FILE, "General", "PanicStopKey", "F10")
    G.DetectControllerKey := IniRead(CONFIG_FILE, "General", "DetectControllerKey", "F9")

    ; --- Movement key map ---
    KeyMap := Map(
        "F", IniRead(CONFIG_FILE, "Movement", "Forward",  "w"),
        "B", IniRead(CONFIG_FILE, "Movement", "Backward", "s"),
        "L", IniRead(CONFIG_FILE, "Movement", "Left",     "a"),
        "R", IniRead(CONFIG_FILE, "Movement", "Right",    "d"),
        "S", IniRead(CONFIG_FILE, "Movement", "Sprint",   "Shift")
    )

    ; --- Controller ---
    G.JoyEnabled  := Integer(IniRead(CONFIG_FILE, "Controller", "Enabled", "1"))
    G.JoyID       := Integer(IniRead(CONFIG_FILE, "Controller", "JoyID", "1"))
    G.JoyPollRate := Integer(IniRead(CONFIG_FILE, "Controller", "PollRate", "10"))

    ; --- Tricks ---
    Tricks := Map()
    list := IniRead(CONFIG_FILE, "Tricks", "List", "")
    for name in StrSplit(list, ",", " `t") {
        if (name = "")
            continue
        t := {}
        t.name    := name
        t.mode    := IniRead(CONFIG_FILE, name, "Mode", "Hold")
        t.key     := IniRead(CONFIG_FILE, name, "Key", "")
        t.joy     := Integer(IniRead(CONFIG_FILE, name, "JoyButton", "0"))
        t.intro   := ParseSequence(IniRead(CONFIG_FILE, name, "Intro", ""))
        t.sustain := ParseSequence(IniRead(CONFIG_FILE, name, "Sustain", ""))
        Tricks[name] := t
        Toggled[name] := false
    }

    ; Apply send mode (Event is generally most game-compatible; Input is fastest)
    SendMode(G.SendMode)
    SetKeyDelay(-1)
}

ParseSequence(str) {
    out := []
    for tok in StrSplit(Trim(str), ",", " `t") {
        if (tok = "")
            continue
        out.Push(tok)
    }
    return out
}

;============================================================================
;  HOTKEYS  (keyboard)
;============================================================================
BuildHotkeys() {
    global
    ; System hotkeys
    Hotkey("*" G.MasterToggleKey,     (*) => ToggleMaster())
    Hotkey("*" G.PanicStopKey,        (*) => PanicStop())
    Hotkey("*" G.DetectControllerKey, (*) => DetectController())

    ; Per-trick keyboard triggers
    for name, t in Tricks {
        if (t.key = "")
            continue
        local capName := name
        Hotkey("*" t.key, OnKeyTrigger.Bind(capName))
    }
}

OnKeyTrigger(name, *) {
    t := Tricks[name]
    if (t.mode = "Hold") {
        ; Guard against keyboard auto-repeat re-launching the loop.
        if (ActiveTrick = name)
            return
        isActive := () => Enabled && GameOK() && ActiveTrick = name && GetKeyState(t.key, "P")
        StartTrick(name, isActive)
    } else if (t.mode = "Toggle") {
        FireToggle(name)
        KeyWait(t.key)            ; debounce: ignore auto-repeat until released
    } else {                      ; Tap
        isActive := () => Enabled && GameOK() && ActiveTrick = name
        StartTrick(name, isActive)
        KeyWait(t.key)
    }
}

FireToggle(name) {
    if (Toggled[name]) {
        Toggled[name] := false          ; loop will see this and stop
        return
    }
    StopAll()
    Toggled[name] := true
    isActive := () => Enabled && GameOK() && ActiveTrick = name && Toggled[name]
    StartTrick(name, isActive)
}

;============================================================================
;  CONTROLLER  (polling-based; works for Hold and Toggle reliably)
;============================================================================
SetupController() {
    if !G.JoyEnabled
        return
    for name, t in Tricks
        if (t.joy > 0)
            JoyPrev[name] := false
    SetTimer(PollController, G.JoyPollRate)
}

PollController() {
    for name, t in Tricks {
        if (t.joy <= 0)
            continue
        joyKey := JoyButtonName(t.joy)
        cur := GetKeyState(joyKey) ? true : false
        prev := JoyPrev.Has(name) ? JoyPrev[name] : false

        if (cur && !prev) {              ; rising edge = press
            if (t.mode = "Toggle")
                FireToggle(name)
            else {                       ; Hold or Tap
                if !(t.mode = "Hold" && ActiveTrick = name) {
                    isActive := MakeJoyActiveFn(name, t)
                    StartTrick(name, isActive)
                }
            }
        }
        JoyPrev[name] := cur
    }
}

MakeJoyActiveFn(name, t) {
    if (t.mode = "Hold")
        return () => Enabled && GameOK() && ActiveTrick = name && GetKeyState(JoyButtonName(t.joy))
    return () => Enabled && GameOK() && ActiveTrick = name   ; Tap
}

JoyButtonName(btn) {
    return (G.JoyID > 0 ? G.JoyID : "") "Joy" btn
}

;============================================================================
;  TRICK ENGINE
;============================================================================
StartTrick(name, isActive) {
    ActiveTrick := name
    UpdateStatus()
    ; Run in its own one-shot thread so triggers/poll-timer stay responsive.
    SetTimer(RunTrick.Bind(name, isActive), -1)
}

RunTrick(name, isActive) {
    t := Tricks[name]
    ReleaseAllMovement()

    if (G.Sprinting)
        Send("{" KeyMap["S"] " down}")

    ; --- Intro sequence (runs once) ---
    for tok in t.intro {
        if !isActive()
            break
        DoStep(tok, isActive)
    }

    ; --- Sustain ---
    if (t.mode != "Tap") {
        if (t.sustain.Length) {
            while isActive() {
                for tok in t.sustain {
                    if !isActive()
                        break
                    DoStep(tok, isActive)
                }
            }
        } else {
            ; Hold/Toggle with no sustain steps: just wait until released/toggled.
            while isActive()
                Sleep(10)
        }
    }

    ; --- Cleanup ---
    if (G.Sprinting)
        Send("{" KeyMap["S"] " up}")
    ReleaseAllMovement()
    if (ActiveTrick = name) {
        ActiveTrick := ""
        Toggled[name] := false
    }
    UpdateStatus()
}

; Execute one token: press all mapped keys, hold for MS (interruptibly), release.
DoStep(token, isActive) {
    parts   := StrSplit(token, ":")
    letters := parts[1]
    ms      := parts.Length >= 2 ? Integer(Trim(parts[2])) : 100

    keys := []
    for c in StrSplit(letters) {
        c := StrUpper(c)
        if KeyMap.Has(c)
            keys.Push(KeyMap[c])
    }
    for k in keys
        Send("{" k " down}")

    InterruptibleSleep(ms, isActive)

    loop keys.Length                       ; release in reverse order
        Send("{" keys[keys.Length - A_Index + 1] " up}")
}

InterruptibleSleep(ms, isActive) {
    deadline := A_TickCount + ms
    while (A_TickCount < deadline) {
        if !isActive()
            return false
        chunk := deadline - A_TickCount
        Sleep(chunk < 10 ? chunk : 10)
    }
    return true
}

ReleaseAllMovement() {
    for letter in ["F", "B", "L", "R"]
        Send("{" KeyMap[letter] " up}")
}

;============================================================================
;  SYSTEM ACTIONS
;============================================================================
GameOK() {
    if !G.OnlyWhenGameActive
        return true
    return WinActive("ahk_exe " G.GameProcess)
}

ToggleMaster() {
    global Enabled
    Enabled := !Enabled
    if !Enabled
        StopAll()
    UpdateStatus()
    TrayTip("Moonwalk Pro " (Enabled ? "ENABLED" : "DISABLED"), "Moonwalk Pro", 1)
}

PanicStop() {
    StopAll()
    TrayTip("Panic stop - all inputs released.", "Moonwalk Pro", 1)
}

StopAll() {
    global ActiveTrick
    ActiveTrick := ""
    for name in Toggled
        Toggled[name] := false
    if (G.Sprinting)
        Send("{" KeyMap["S"] " up}")
    ReleaseAllMovement()
    UpdateStatus()
}

; Press a controller button and a MsgBox tells you its number -> easy binding.
DetectController() {
    found := ""
    loop 32 {
        id := G.JoyID > 0 ? G.JoyID : 1
        if GetKeyState(id "Joy" A_Index)
            found .= (found = "" ? "" : ", ") A_Index
    }
    if (found = "")
        ToolTip("Hold a controller button, then press " G.DetectControllerKey " again.")
    else
        ToolTip("Controller button(s) down: " found "  (use this number as JoyButton)")
    SetTimer(() => ToolTip(), -2500)
}

;============================================================================
;  STATUS GUI  +  TRAY
;============================================================================
BuildStatusGui() {
    global StatusGui
    A_TrayMenu.Delete()
    A_TrayMenu.Add("Edit config.ini", (*) => Run("notepad.exe " CONFIG_FILE))
    A_TrayMenu.Add("Reload", (*) => Reload())
    A_TrayMenu.Add("Toggle master (F8)", (*) => ToggleMaster())
    A_TrayMenu.Add()
    A_TrayMenu.Add("Exit", (*) => ExitApp())
    A_TrayMenu.Default := "Reload"

    if !G.ShowStatusGui
        return
    StatusGui := Gui("+AlwaysOnTop -Caption +ToolWindow +E0x20", "Moonwalk Pro")
    StatusGui.BackColor := "1a1a1a"
    StatusGui.SetFont("s9 cFFFFFF", "Consolas")
    StatusGui.Add("Text", "vLine1 w220", "Moonwalk Pro")
    StatusGui.Add("Text", "vLine2 w220", "")
    StatusGui.Show("x10 y10 NoActivate")
    WinSetTransparent(210, "ahk_id " StatusGui.Hwnd)
}

UpdateStatus() {
    if !(IsObject(StatusGui))
        return
    state := !Enabled ? "DISABLED" : (ActiveTrick = "" ? "idle" : ">> " ActiveTrick)
    try {
        StatusGui["Line1"].Value := "Moonwalk Pro  [" (Enabled ? "ON" : "OFF") "]"
        StatusGui["Line2"].Value := state
    }
}

;============================================================================
;  DEFAULT CONFIG  (written on first run if config.ini is missing)
;============================================================================
DefaultConfig() {
    return "
(
; ============================================================================
;  Moonwalk Pro - configuration
; ----------------------------------------------------------------------------
;  Sequence token language (used by Intro / Sustain):
;     F=forward  B=backward  L=left  R=right  S=sprint
;     One step  = DIR:MS   (milliseconds to hold).  e.g.  L:200
;     Diagonals = combine letters.                  e.g.  FL:120  (fwd+left)
;     Chain steps with commas.                      e.g.  L:200,B:300,L:200,F:300
;  Intro   = played once when the trick starts.
;  Sustain = looped while the trigger is held (Hold) or until re-pressed (Toggle).
; ============================================================================

[General]
; Event = best compatibility with most games.  Input = fastest.  Play = legacy.
SendMode=Event
; Hold the sprint key for the whole trick? 1=yes 0=no
Sprinting=1
; Only act while the game window is focused? 1=yes 0=no
OnlyWhenGameActive=0
GameProcess=DeadByDaylight-Win64-Shipping.exe
; Tiny on-screen status overlay. 1=show 0=hide
ShowStatusGui=1
; System hotkeys
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
; 1=enable controller triggers, 0=disable
Enabled=1
; Joystick number (1-16). Use the Detect key (F9) if unsure.
JoyID=1
; Polling interval in ms (lower = more responsive, slightly more CPU)
PollRate=10

; List every trick name here (comma separated). Each needs its own [Section].
[Tricks]
List=MoonwalkBackward,MoonwalkForward,Spin360,QuickJuke

; ---- Backward moonwalk: face forward, drift backward ----
[MoonwalkBackward]
Mode=Hold
Key=Numpad3
JoyButton=5
Intro=L:200,B:300,L:200,F:300
Sustain=L:60,R:60

; ---- Forward moonwalk: face backward, drift forward ----
[MoonwalkForward]
Mode=Hold
Key=Numpad2
JoyButton=6
Intro=F:200,L:300,B:400
Sustain=L:80,R:80

; ---- 360 spin (one-shot) ----
[Spin360]
Mode=Tap
Key=Numpad1
JoyButton=7
Intro=L:90,FL:90,F:90,FR:90,R:90,BR:90,B:90,BL:90
Sustain=

; ---- Quick left/right juke (one-shot) ----
[QuickJuke]
Mode=Tap
Key=Numpad0
JoyButton=8
Intro=L:120,R:120
Sustain=
)"
}
