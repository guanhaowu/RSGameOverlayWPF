using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RSGameOverlayWPF;

public partial class MainWindow : Window
{
    private readonly OverlayWindow overlay = new();
    private readonly CursorPreviewWindow cursorPreview = new();
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private bool picking;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private Color selectedColor = Colors.Magenta;
    private bool running, scanning, closing;
    private int generation;
    private HwndSource? source;
    private bool pickRegistered, toggleRegistered;
    private bool escapeRegistered;
    private bool freezeRegistered;
    private bool frozen;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WindowMessage);
            pickRegistered = NativeScreen.RegisterHotKey(source.Handle, 1, 0x4000, 0x77);
            toggleRegistered = NativeScreen.RegisterHotKey(source.Handle, 2, 0x4000, 0x78);
            freezeRegistered = NativeScreen.RegisterHotKey(source.Handle, 4, 0x4000, 0x79);
            if (!pickRegistered || !toggleRegistered)
                Status.Text = "F8 or F9 is already in use. Close the conflicting app and reopen this app.";
            if (!freezeRegistered) Status.Text += " F10 is unavailable; use the Freeze highlights button.";
        };
        Loaded += (_, _) => RefreshClients();
        timer.Tick += Scan_Tick;
        previewTimer.Tick += Preview_Tick;
        Closed += (_, _) =>
        {
            closing = true;
            timer.Stop();
            previewTimer.Stop();
            if (source is not null)
            {
                if (pickRegistered) NativeScreen.UnregisterHotKey(source.Handle, 1);
                if (toggleRegistered) NativeScreen.UnregisterHotKey(source.Handle, 2);
                if (escapeRegistered) NativeScreen.UnregisterHotKey(source.Handle, 3);
                if (freezeRegistered) NativeScreen.UnregisterHotKey(source.Handle, 4);
                source.RemoveHook(WindowMessage);
            }
            overlay.Close();
            cursorPreview.Close();
        };
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != 0x0312) return 0;
        if (wParam == 1)
        {
            if (!picking)
            {
                PickNewColor_Click(this, new RoutedEventArgs());
                handled = true;
                return 0;
            }
            try
            {
                selectedColor = NativeScreen.PickColor();
                picking = false;
                ColorPreview.Background = new SolidColorBrush(selectedColor);
                ColorLabel.Text = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}\nRGB {selectedColor.R}, {selectedColor.G}, {selectedColor.B}";
                cursorPreview.Display(selectedColor, false);
                previewTimer.Start();
                generation++;
                overlay.Clear();
                Status.Text = "Color selected. " + (running ? "Highlighting." : "Ready to start.");
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        }
        else if (wParam == 2) Toggle();
        else if (wParam == 3) StopPicker_Click(this, new RoutedEventArgs());
        else if (wParam == 4) FreezeHighlights_Click(this, new RoutedEventArgs());
        handled = true;
        return 0;
    }

    private void RefreshClients()
    {
        var previous = (ClientList.SelectedItem as ClientWindow)?.Handle;
        var clients = NativeScreen.ListWindows(new WindowInteropHelper(this).Handle);
        ClientList.ItemsSource = clients;
        ClientList.SelectedItem = clients.FirstOrDefault(c => c.Handle == previous)
            ?? clients.FirstOrDefault(c => c.Title.Contains("RuneScape", StringComparison.OrdinalIgnoreCase)
                || c.Title.Contains("RuneLite", StringComparison.OrdinalIgnoreCase));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshClients();
    private void PickNewColor_Click(object sender, RoutedEventArgs e)
    {
        picking = true;
        frozen = false;
        FreezeButton.Content = "Freeze highlights for screenshot (F10)";
        generation++;
        overlay.Hide();
        overlay.Clear();
        Status.Text = "Live color preview. Move your pointer, then press F8 to select.";
        StopPickerButton.IsEnabled = true;
        if (source is not null && !escapeRegistered)
        {
            escapeRegistered = NativeScreen.RegisterHotKey(source.Handle, 3, 0x4000, 0x1B);
            if (!escapeRegistered)
                Status.Text += " Esc is unavailable; use Stop picker to cancel.";
        }
        previewTimer.Start();
        Preview_Tick(null, EventArgs.Empty);
    }

    private void StopPicker_Click(object sender, RoutedEventArgs e)
    {
        picking = false;
        generation++;
        previewTimer.Stop();
        cursorPreview.Hide();
        StopPickerButton.IsEnabled = false;
        if (source is not null && escapeRegistered)
        {
            NativeScreen.UnregisterHotKey(source.Handle, 3);
            escapeRegistered = false;
        }
        ColorPreview.Background = new SolidColorBrush(selectedColor);
        ColorLabel.Text = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}\nRGB {selectedColor.R}, {selectedColor.G}, {selectedColor.B}";
        Status.Text = "Picker stopped. Last selected color retained.";
    }

    private void Preview_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (picking)
            {
                var live = NativeScreen.PickColor();
                cursorPreview.Display(live, true);
                ColorPreview.Background = new SolidColorBrush(live);
                ColorLabel.Text = $"#{live.R:X2}{live.G:X2}{live.B:X2}\nRGB {live.R}, {live.G}, {live.B}";
            }
            else cursorPreview.FollowCursor();
        }
        catch (Exception ex)
        {
            StopPicker_Click(this, new RoutedEventArgs());
            Status.Text = $"Color preview failed: {ex.Message}";
        }
    }
    private void Toggle_Click(object sender, RoutedEventArgs e) => Toggle();
    private void FreezeHighlights_Click(object sender, RoutedEventArgs e)
    {
        if (!running || picking)
        {
            Status.Text = "Select a color and start highlighting before freezing.";
            return;
        }
        frozen = !frozen;
        generation++;
        FreezeButton.Content = frozen ? "Resume live highlights (F10)" : "Freeze highlights for screenshot (F10)";
        Status.Text = frozen ? "Highlights frozen. Take your screenshot, then press F10 to resume." : "Live highlighting resumed.";
    }
    private void ClientList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        generation++;
        overlay.Hide();
        overlay.Clear();
    }

    private void Toggle()
    {
        if (!running && ClientList.SelectedItem is not ClientWindow)
        {
            Status.Text = "Select the RuneScape client window first.";
            return;
        }
        running = !running;
        frozen = false;
        FreezeButton.IsEnabled = running;
        FreezeButton.Content = "Freeze highlights for screenshot (F10)";
        generation++;
        ToggleButton.Content = running ? "Stop highlighting (F9)" : "Start highlighting (F9)";
        if (running) timer.Start();
        else { timer.Stop(); overlay.Hide(); }
        Status.Text = running ? "Highlighting matching color areas…" : "Highlighting stopped.";
    }

    private async void Scan_Tick(object? sender, EventArgs e)
    {
        if (scanning || picking || !running || ClientList.SelectedItem is not ClientWindow client) return;
        if (!NativeScreen.TryGetClientBounds(client.Handle, out var bounds))
        {
            overlay.Hide();
            Status.Text = "Client is minimized or unavailable. Waiting…";
            return;
        }
        // Pause detection without hiding the last result when ANY other app
        // takes focus. Screenshot tools need no process-name special cases.
        var foreground = NativeScreen.GetForegroundWindow();
        if (frozen || (foreground != client.Handle && foreground != new WindowInteropHelper(this).Handle))
        {
            if (overlay.IsVisible) overlay.Place(bounds);
            Status.Text = frozen
                ? "Highlights frozen. Press F10 to resume."
                : "Highlights retained while another application is active.";
            return;
        }
        scanning = true;
        var version = generation;
        var color = selectedColor;
        var tolerance = (int)Tolerance.Value;
        try
        {
            overlay.Place(bounds);
            var excluded = overlay.GetDetectionExclusions(bounds);
            if (NativeScreen.TryGetWindowBounds(new WindowInteropHelper(this).Handle, out var controlBounds))
                excluded.Add(controlBounds);
            // Mask the preview as well, even when a capture API does not honor
            // its Windows display-affinity exclusion.
            if (NativeScreen.TryGetWindowBounds(new WindowInteropHelper(cursorPreview).Handle, out var previewBounds))
                excluded.Add(previewBounds);
            var matches = await Task.Run(() => NativeScreen.FindMatches(bounds, color, tolerance, excluded));
            if (closing || !running || frozen || generation != version) return;
            foreground = NativeScreen.GetForegroundWindow();
            if (foreground != client.Handle && foreground != new WindowInteropHelper(this).Handle)
            {
                return;
            }
            if (!NativeScreen.TryGetClientBounds(client.Handle, out var current) || current != bounds)
            {
                overlay.Hide();
                return;
            }
            overlay.SetMatches(matches);
            Status.Text = $"{matches.Count} matching areas" + (matches.Count >= 250 ? " (display limit reached)." : ".");
        }
        catch (Exception ex)
        {
            if (!closing) { overlay.Hide(); Status.Text = $"Capture failed: {ex.Message}"; }
        }
        finally { scanning = false; }
    }
}
