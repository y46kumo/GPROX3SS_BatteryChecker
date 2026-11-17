using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GProX3BatteryChecker.Hidpp;

namespace GProX3BatteryChecker.UI;

/// <summary>배터리 모양 게이지 + 퍼센트 숫자를 그립니다. (트레이 아이콘과 팝업이 공용으로 사용)</summary>
internal static class BatteryPainter
{
    public static readonly Color Green = Color.FromArgb(76, 194, 106);
    public static readonly Color Yellow = Color.FromArgb(242, 178, 51);
    public static readonly Color Red = Color.FromArgb(229, 72, 77);
    public static readonly Color Blue = Color.FromArgb(58, 160, 255);
    public static readonly Color Gray = Color.FromArgb(138, 138, 138);

    private static readonly FontFamily TextFamily = ResolveFontFamily();

    public static Color LevelColor(int? percent, ChargeState state, bool dimmed = false)
    {
        if (percent is null || dimmed)
        {
            return Gray;
        }

        return state switch
        {
            ChargeState.Charging => Blue,
            ChargeState.Full => Green,
            _ when percent <= 20 => Red,
            _ when percent <= 50 => Yellow,
            _ => Green,
        };
    }

    /// <param name="percent">null이면 "--" (장치 없음)</param>
    /// <param name="outline">배터리 테두리 색</param>
    /// <param name="compact">트레이 아이콘(16~32px)용 레이아웃</param>
    /// <param name="dimmed">연결 끊김 상태에서 마지막 값을 회색으로 표시</param>
    public static void Draw(Graphics g, RectangleF area, int? percent, ChargeState state,
                            Color outline, bool compact, bool dimmed = false)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float nubWidth = Math.Max(1.5f, area.Width * (compact ? 0.09f : 0.05f));
        float stroke = Math.Max(1f, area.Height * (compact ? 0.10f : 0.055f));
        var body = new RectangleF(area.X, area.Y, area.Width - nubWidth, area.Height);

        // + 단자
        var nub = new RectangleF(body.Right - stroke * 0.25f, area.Y + area.Height * 0.3f,
                                 nubWidth + stroke * 0.25f, area.Height * 0.4f);
        using (var nubBrush = new SolidBrush(outline))
        using (var nubPath = RoundedRect(nub, nub.Width * 0.45f))
        {
            g.FillPath(nubBrush, nubPath);
        }

        // 몸체 외곽선
        var outlineRect = RectangleF.Inflate(body, -stroke / 2, -stroke / 2);
        float radius = area.Height * (compact ? 0.16f : 0.14f);
        using (var pen = new Pen(outline, stroke))
        using (var path = RoundedRect(outlineRect, radius))
        {
            g.DrawPath(pen, path);
        }

        // 내부 게이지
        float gap = compact ? stroke * 0.6f : stroke * 1.1f;
        var inner = RectangleF.Inflate(body, -(stroke + gap), -(stroke + gap));
        float innerRadius = Math.Max(0.5f, radius - stroke - gap * 0.6f);
        if (!compact)
        {
            using var track = new SolidBrush(Color.FromArgb(40, outline));
            using var trackPath = RoundedRect(inner, innerRadius);
            g.FillPath(track, trackPath);
        }

        if (percent is int p && p > 0)
        {
            var fill = inner with { Width = Math.Max(inner.Width * p / 100f, innerRadius * 2) };
            using var fillBrush = new SolidBrush(LevelColor(percent, state, dimmed));
            using var fillPath = RoundedRect(fill, innerRadius);
            g.FillPath(fillBrush, fillPath);
        }

        // 퍼센트 숫자
        string text = percent is int value ? (compact ? $"{value}" : $"{value}%") : "--";
        RectangleF textArea = compact
            ? new RectangleF(body.X + stroke * 0.6f, body.Y + body.Height * 0.14f,
                             body.Width - stroke * 1.2f, body.Height * 0.72f)
            : RectangleF.Inflate(inner, -inner.Height * 0.12f, -inner.Height * 0.2f);
        float outlineWidth = compact ? Math.Max(1.5f, area.Height * 0.16f) : Math.Max(2f, area.Height * 0.06f);
        DrawOutlinedText(g, text, textArea, Color.White, Color.FromArgb(220, 20, 20, 20), outlineWidth);
    }

    private static void DrawOutlinedText(Graphics g, string text, RectangleF target,
                                         Color fill, Color outline, float outlineWidth)
    {
        using var path = new GraphicsPath();
        path.AddString(text, TextFamily, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
        var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float scale = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
        using (var matrix = new Matrix())
        {
            matrix.Translate(target.X + (target.Width - bounds.Width * scale) / 2,
                             target.Y + (target.Height - bounds.Height * scale) / 2);
            matrix.Scale(scale, scale);
            matrix.Translate(-bounds.X, -bounds.Y);
            path.Transform(matrix);
        }

        using var pen = new Pen(outline, outlineWidth) { LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(fill);
        g.DrawPath(pen, path);
        g.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static FontFamily ResolveFontFamily()
    {
        foreach (var name in new[] { "Segoe UI", "Arial" })
        {
            try { return new FontFamily(name); }
            catch (ArgumentException) { }
        }
        return FontFamily.GenericSansSerif;
    }
}
