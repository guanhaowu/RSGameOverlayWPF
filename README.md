# RuneScape color overlay

A WPF control window and a transparent, click-through overlay. Reads visible
pixels and draws outlines. No mouse movement, click injection, or game automation.

## Use

Run the project in Rider, or run `dotnet run --project RSGameOverlayWPF.csproj`.

1. Open RuneScape in windowed or borderless mode.
2. Select its window in the client list. Refresh if needed.
3. Press **F8** or click **Pick new color** for a live preview beside your pointer. The
   larger swatch shows HEX and RGB values underneath. Move to the desired pixel
   and press **F8** again to select it. No click on the game is required.
4. Click **Start highlighting** or press **F9**.
5. Adjust tolerance. Zero requires an exact RGB match; 12 allows each RGB channel
   to differ by up to 12. The cursor preview and control-window preview retain
   the selected color while you move the pointer. Click **Pick new color** to
   resume live sampling; highlighting pauses until you select with F8.
6. Press F9 to stop. Closing the control window closes the overlay too.

F8/F9 are global shortcuts while the app runs. If another app has registered
either key, close that app and reopen this one. The shortcuts consume those keys.
F8 starts live picking when idle and confirms the selected color while picking.
Selections last for the current application session and reset on restart.
The cursor preview is click-through, stays within the current monitor's work
area, and is excluded from capture so it cannot contaminate color detection.

## Detection

Captures the visible client area approximately every 180 ms, with at most one
background capture/detection job at a time. Matching pixels that touch (including
diagonally) form groups. Each gets a tight rectangular outline around its actual
pixel bounds, plus 2 physical pixels of padding on each side. At client edges,
padding is clipped to the overlay. At most 250 groups are displayed.

The overlay follows client position and size and handles display scaling. It
hides when minimized or when another application has focus; detection also works
while the control window is active. Capture excludes the overlay's own marks.

This is desktop capture: covered game pixels cannot be detected. Keep the control
window beside the game. Use Windows 10 version 2004 or newer for overlay capture
exclusion. Exclusive fullscreen, protected content, HDR, lighting, and
antialiasing can affect results. Start with a visible SDR windowed client.

## Files

- `MainWindow.xaml` / `.cs`: controls, hotkeys, scheduling.
- `OverlayWindow.xaml` / `.cs`: transparent window and highlight drawing.
- `CursorPreviewWindow.cs`: larger cursor swatch, HEX/RGB readout, selected/live state.
- `NativeScreen.cs`: window discovery, capture, RGB matching, overlay positioning.
- `app.manifest`: per-monitor DPI awareness.
