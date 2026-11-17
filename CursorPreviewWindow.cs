using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace RSGameOverlayWPF;

// A passive readout: it never activates or receives mouse input.
internal sealed class CursorPreviewWindow : Window
{
    private readonly Border swatch = new() { Height = 90, BorderBrush = Brushes.White, BorderThickness = new Thickness(1) };
    private readonly TextBlock hex = new() { FontSize = 18, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 8, 0, 2) };
    private readonly TextBlock rgb = new() { FontSize = 14, FontFamily = new FontFamily("Consolas") };
    private readonly TextBlock mode = new() { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

    internal CursorPreviewWindow()
    {
        Title = "Color picker preview";
        Width = 224;
        Height = 222;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        var panel = new StackPanel();
        panel.Children.Add(swatch);
        panel.Children.Add(hex);
        panel.Children.Add(rgb);
        panel.Children.Add(mode);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)),
            BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
            Padding = new Thickness(14), CornerRadius = new CornerRadius(8), Child = panel
        };
        Foreground = Brushes.White;
        SourceInitialized += (_, _) => NativeScreen.ConfigureOverlay(new WindowInteropHelper(this).Handle);
    }

    internal void Display(Color color, bool picking)
    {
        swatch.Background = new SolidColorBrush(color);
        hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        rgb.Text = $"RGB {color.R}, {color.G}, {color.B}";
        mode.Text = picking ? "Live preview - F8 to select" : "Selected color";
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (!NativeScreen.SetWindowDisplayAffinity(handle, 0x11))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot exclude color preview from capture.");
        FollowCursor();
        if (!IsVisible) Show();
    }

    internal void FollowCursor()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        NativeScreen.PositionOverlay(new WindowInteropHelper(this).Handle,
            NativeScreen.GetCursorPreviewBounds((int)Math.Ceiling(Width * dpi.DpiScaleX), (int)Math.Ceiling(Height * dpi.DpiScaleY)));
    }
}
