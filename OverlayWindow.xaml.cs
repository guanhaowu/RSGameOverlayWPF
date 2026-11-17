using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RSGameOverlayWPF;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeScreen.ConfigureOverlay(new WindowInteropHelper(this).Handle);
    }

    internal void Place(PixelBounds bounds)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (!NativeScreen.SetWindowDisplayAffinity(handle, 0x11))
            throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Cannot exclude overlay from capture.");
        NativeScreen.PositionOverlay(handle, bounds);
        if (!IsVisible) Show();
    }

    internal void Clear() => Highlights.Children.Clear();

    internal void SetMatches(IReadOnlyList<PixelBounds> matches)
    {
        Clear();
        var dpi = VisualTreeHelper.GetDpi(this);
        foreach (var match in matches)
        {
            // Two physical screen pixels of padding on each edge.
            var left = match.X - 2.0;
            var top = match.Y - 2.0;
            var width = match.Width + 4.0;
            var height = match.Height + 4.0;
            AddOutline(left, top, width, dpi, Brushes.Black, 2, height);
            AddOutline(left, top, width, dpi, Brushes.Lime, 1, height);
        }
    }

    private void AddOutline(double x, double y, double width, DpiScale dpi, Brush brush, double thickness, double height)
    {
        var outline = new Rectangle
        {
            Width = width / dpi.DpiScaleX, Height = height / dpi.DpiScaleY,
            Stroke = brush, StrokeThickness = thickness / dpi.DpiScaleX, IsHitTestVisible = false
        };
        System.Windows.Controls.Canvas.SetLeft(outline, x / dpi.DpiScaleX);
        System.Windows.Controls.Canvas.SetTop(outline, y / dpi.DpiScaleY);
        Highlights.Children.Add(outline);
    }
}
