using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using GProX3BatteryChecker.Hidpp;

namespace GProX3BatteryChecker.UI;

/// <summary>트레이(알림 영역)용 배터리 아이콘을 만듭니다. 이전 아이콘 핸들은 자동으로 해제합니다.</summary>
internal sealed partial class TrayIconRenderer : IDisposable
{
    private IntPtr _handle;
    private Icon? _icon;

    public Icon Render(int? percent, ChargeState state, bool lightTaskbar, bool dimmed)
    {
        var size = SystemInformation.SmallIconSize;
        int s = Math.Max(16, Math.Min(size.Width, size.Height));

        using var bitmap = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            float height = MathF.Round(s * 0.75f);
            var area = new RectangleF(0, (s - height) / 2f, s, height);
            var outline = lightTaskbar ? Color.FromArgb(30, 30, 30) : Color.White;
            if (percent is null || dimmed)
            {
                outline = Color.FromArgb(160, outline);
            }
            BatteryPainter.Draw(g, area, percent, state, outline, compact: true, dimmed);
        }

        var newHandle = bitmap.GetHicon();
        var newIcon = Icon.FromHandle(newHandle);

        ReleaseCurrent();
        _handle = newHandle;
        _icon = newIcon;
        return newIcon;
    }

    private void ReleaseCurrent()
    {
        _icon?.Dispose();
        _icon = null;
        if (_handle != IntPtr.Zero)
        {
            DestroyIcon(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public void Dispose() => ReleaseCurrent();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);
}
