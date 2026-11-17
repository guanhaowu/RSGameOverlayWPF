namespace RSGameOverlayWPF;

internal static class DetectionMask
{
    // Regions are in physical desktop pixels; occupied is relative to capture.
    internal static void ClearRegions(bool[] occupied, PixelBounds capture, IReadOnlyList<PixelBounds> regions)
    {
        foreach (var region in regions)
        {
            var left = (int)Math.Clamp((long)region.X - capture.X, 0, capture.Width);
            var top = (int)Math.Clamp((long)region.Y - capture.Y, 0, capture.Height);
            var right = (int)Math.Clamp((long)region.X + region.Width - capture.X, 0, capture.Width);
            var bottom = (int)Math.Clamp((long)region.Y + region.Height - capture.Y, 0, capture.Height);
            if (right <= left || bottom <= top) continue;
            for (var y = top; y < bottom; y++)
                Array.Clear(occupied, y * capture.Width + left, right - left);
        }
    }

    internal static IEnumerable<PixelBounds> OutlineEdges(PixelBounds capture, PixelBounds match)
    {
        var x = capture.X + match.X - 2;
        var y = capture.Y + match.Y - 2;
        var width = match.Width + 4;
        var height = match.Height + 4;
        // WPF Rectangle draws its stroke inside its layout bounds. The outer
        // stroke occupies two pixels, entirely inside the requested padding.
        yield return new PixelBounds(x, y, width, 2);
        yield return new PixelBounds(x, y + height - 2, width, 2);
        yield return new PixelBounds(x, y + 2, 2, height - 4);
        yield return new PixelBounds(x + width - 2, y + 2, 2, height - 4);
    }
}
