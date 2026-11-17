using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;

namespace RSGameOverlayWPF;

internal record ClientWindow(nint Handle, string Title);
internal readonly record struct PixelBounds(int X, int Y, int Width, int Height);

internal static class NativeScreen
{
    internal static List<ClientWindow> ListWindows(nint ownWindow)
    {
        var result = new List<ClientWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == ownWindow || !IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);
            if (title.Length > 0 && title.ToString() != "Color highlights" && title.ToString() != "Color picker preview")
                result.Add(new ClientWindow(hwnd, title.ToString()));
            return true;
        }, 0);
        return result.OrderBy(c => c.Title).ToList();
    }

    internal static bool TryGetClientBounds(nint hwnd, out PixelBounds bounds)
    {
        bounds = default;
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd) || !GetClientRect(hwnd, out var rect)) return false;
        var origin = new Point();
        if (!ClientToScreen(hwnd, ref origin)) return false;
        // Limit capture to the visible virtual desktop, including monitors with negative coordinates.
        var x = Math.Max(origin.X, GetSystemMetrics(76));
        var y = Math.Max(origin.Y, GetSystemMetrics(77));
        var right = Math.Min(origin.X + rect.Right, GetSystemMetrics(76) + GetSystemMetrics(78));
        var bottom = Math.Min(origin.Y + rect.Bottom, GetSystemMetrics(77) + GetSystemMetrics(79));
        if (right <= x || bottom <= y) return false;
        bounds = new PixelBounds(x, y, right - x, bottom - y);
        return true;
    }

    internal static void ConfigureOverlay(nint hwnd)
    {
        // WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE.
        var style = GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, style | 0x20 | 0x80 | 0x08000000);
    }

    internal static void PositionOverlay(nint hwnd, PixelBounds bounds)
    {
        if (!SetWindowPos(hwnd, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static Color PickColor()
    {
        if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var pixels = Capture(new PixelBounds(point.X, point.Y, 1, 1));
        return Color.FromRgb(pixels[2], pixels[1], pixels[0]);
    }

    internal static PixelBounds GetCursorPreviewBounds(int width, int height)
    {
        if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var monitor = MonitorFromPoint(point, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        const int gap = 24;
        var x = point.X + gap;
        var y = point.Y + gap;
        if (x + width > info.Work.Right) x = point.X - width - gap;
        if (y + height > info.Work.Bottom) y = point.Y - height - gap;
        x = Math.Max(info.Work.Left, Math.Min(x, info.Work.Right - width));
        y = Math.Max(info.Work.Top, Math.Min(y, info.Work.Bottom - height));
        return new PixelBounds(x, y, width, height);
    }

    internal static List<PixelBounds> FindMatches(PixelBounds bounds, Color color, int tolerance)
    {
        var pixels = Capture(bounds);
        // Flood fill actual matching pixels (including diagonal neighbors).
        // Bounding boxes use the outer pixel edges, without tile rounding.
        var columns = bounds.Width;
        var rows = bounds.Height;
        var occupied = new bool[columns * rows];
        for (var y = 0; y < bounds.Height; y++)
        for (var x = 0; x < bounds.Width; x++)
        {
            var p = (y * bounds.Width + x) * 4;
            if (Math.Abs(pixels[p + 2] - color.R) <= tolerance &&
                Math.Abs(pixels[p + 1] - color.G) <= tolerance &&
                Math.Abs(pixels[p] - color.B) <= tolerance)
                occupied[y * columns + x] = true;
        }
        var result = new List<PixelBounds>();
        var queue = new Queue<int>();
        for (var i = 0; i < occupied.Length && result.Count < 250; i++)
        {
            if (!occupied[i]) continue;
            occupied[i] = false;
            queue.Enqueue(i);
            var minX = i % columns;
            var maxX = minX;
            var minY = i / columns;
            var maxY = minY;
            while (queue.TryDequeue(out var cell))
            {
                var cx = cell % columns;
                var cy = cell / columns;
                minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
                minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = cx + dx; var ny = cy + dy;
                    if (nx < 0 || nx >= columns || ny < 0 || ny >= rows) continue;
                    var next = ny * columns + nx;
                    if (!occupied[next]) continue;
                    occupied[next] = false;
                    queue.Enqueue(next);
                }
            }
            result.Add(new PixelBounds(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }
        return result;
    }

    private static byte[] Capture(PixelBounds bounds)
    {
        var size = checked(bounds.Width * bounds.Height * 4);
        var screen = GetDC(0);
        if (screen == 0) throw new Win32Exception("Cannot access the desktop.");
        nint memory = 0, bitmap = 0, previous = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            if (memory == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new BitmapInfo
            {
                Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = bounds.Width,
                Height = -bounds.Height, Planes = 1, BitCount = 32
            };
            bitmap = CreateDIBSection(screen, ref info, 0, out var bits, 0, 0);
            if (bitmap == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(memory, bitmap);
            if (previous == 0 || previous == new nint(-1)) throw new Win32Exception("Cannot select capture bitmap.");
            if (!BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var pixels = new byte[size];
            Marshal.Copy(bits, pixels, 0, size);
            return pixels;
        }
        finally
        {
            if (previous != 0 && previous != new nint(-1)) SelectObject(memory, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }
    private delegate bool EnumWindowCallback(nint hwnd, nint parameter);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint hwnd, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
