using GProX3BatteryChecker.Hidpp;

namespace GProX3BatteryChecker.UI;

/// <summary>팝업 창에 표시되는 큰 배터리 게이지.</summary>
internal sealed class BatteryGauge : Control
{
    private int? _percent;
    private ChargeState _state;
    private bool _dimmed;

    public BatteryGauge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    public void SetValue(int? percent, ChargeState state, bool dimmed)
    {
        _percent = percent;
        _state = state;
        _dimmed = dimmed;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var area = new RectangleF(Padding.Left, Padding.Top,
                                  Width - Padding.Horizontal, Height - Padding.Vertical);
        if (area.Width > 0 && area.Height > 0)
        {
            var outline = _percent is null || _dimmed ? BatteryPainter.Gray : Color.FromArgb(235, 235, 235);
            BatteryPainter.Draw(e.Graphics, area, _percent, _state, outline, compact: false, _dimmed);
        }
    }
}
