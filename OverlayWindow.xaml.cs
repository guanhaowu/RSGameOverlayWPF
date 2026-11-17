using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RSGameOverlayWPF;

public partial class OverlayWindow : Window
{
    private IReadOnlyList<PixelBounds> displayedMatches = Array.Empty<PixelBounds>();
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeScreen.ConfigureOverlay(new WindowInteropHelper(this).Handle);
    }

    internal void Place(PixelBounds bounds)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        // Leave the highlights visible to screenshot tools. Detection masks only
        // their painted border pixels instead of hiding the entire window.
        NativeScreen.PositionOverlay(handle, bounds);
        if (!IsVisible) Show();
    }

    internal void Clear()
    {
        displayedMatches = Array.Empty<PixelBounds>();
        Highlights.Children.Clear();
    }

    internal List<PixelBounds> GetDetectionExclusions(PixelBounds capture)
    {
        return IsVisible
            ? displayedMatches.SelectMany(match => DetectionMask.OutlineEdges(capture, match)).ToList()
            : new List<PixelBounds>();
    }

    internal void SetMatches(IReadOnlyList<PixelBounds> matches)
    {
        Clear();
        displayedMatches = matches.ToArray();
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
            Stroke = brush, StrokeThickness = thickness / dpi.DpiScaleX, IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        System.Windows.Controls.Canvas.SetLeft(outline, x / dpi.DpiScaleX);
        System.Windows.Controls.Canvas.SetTop(outline, y / dpi.DpiScaleY);
        Highlights.Children.Add(outline);
    }
}
