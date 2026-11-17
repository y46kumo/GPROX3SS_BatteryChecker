using System.Runtime.InteropServices;
using GProX3BatteryChecker.Hidpp;

namespace GProX3BatteryChecker.UI;

/// <summary>트레이 아이콘을 클릭하면 작업 표시줄 오른쪽 아래에 뜨는 상태 창.</summary>
internal sealed partial class StatusPopup : Form
{
    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color Border = Color.FromArgb(64, 64, 64);
    private static readonly Color SubText = Color.FromArgb(170, 170, 170);
    private static readonly Color ButtonBack = Color.FromArgb(48, 48, 48);
    private static readonly Color AccentBack = Color.FromArgb(0, 120, 212);

    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly BatteryGauge _gauge;
    private readonly Label _status;
    private readonly Label _updated;
    private readonly Button _refreshButton;
    private readonly Button _ghubButton;

    /// <summary>포커스를 잃어 자동으로 닫힌 시각 (트레이 아이콘 클릭 시 다시 열림 방지용)</summary>
    public DateTime LastAutoHiddenAt { get; private set; }

    public event EventHandler? RefreshRequested;
    public event EventHandler? OpenGHubRequested;

    public StatusPopup()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(300, 236);
        Padding = new Padding(16);
        Text = AppInfo.Title;

        _title = new Label
        {
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(268, 24),
            Font = new Font("Segoe UI Semibold", 11.5F),
            Text = AppInfo.DefaultDeviceName,
            AutoEllipsis = true,
        };

        _subtitle = new Label
        {
            AutoSize = false,
            Location = new Point(16, 38),
            Size = new Size(268, 18),
            ForeColor = SubText,
            Text = "Logitech G · 배터리 잔량",
        };

        _gauge = new BatteryGauge
        {
            Location = new Point(16, 66),
            Size = new Size(268, 84),
            Padding = new Padding(2, 4, 2, 4),
        };

        _status = new Label
        {
            AutoSize = false,
            Location = new Point(16, 156),
            Size = new Size(170, 20),
            Font = new Font("Segoe UI Semibold", 9.5F),
            Text = "장치를 찾는 중…",
        };

        _updated = new Label
        {
            AutoSize = false,
            Location = new Point(186, 156),
            Size = new Size(98, 20),
            ForeColor = SubText,
            TextAlign = ContentAlignment.MiddleRight,
        };

        _refreshButton = CreateButton("새로고침", new Point(16, 188), new Size(110, 32), ButtonBack);
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _ghubButton = CreateButton("G HUB 열기", new Point(134, 188), new Size(150, 32), AccentBack);
        _ghubButton.Click += (_, _) => OpenGHubRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([_title, _subtitle, _gauge, _status, _updated, _refreshButton, _ghubButton]);
    }

    private static Button CreateButton(string text, Point location, Size size, Color back)
    {
        var button = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = ControlPaint.Light(back, 0.2f);
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.25f);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.1f);
        return button;
    }

    public void ShowStatus(BatteryStatus status)
    {
        _title.Text = status.DeviceName ?? AppInfo.DefaultDeviceName;
        _gauge.SetValue(status.Percent, status.State, status.IsStale);

        (_status.Text, _status.ForeColor) = status.Connection switch
        {
            ConnectionState.Searching => ("장치를 찾는 중…", SubText),
            ConnectionState.NotFound when status.IsStale => ("연결 끊김 · 마지막 값", BatteryPainter.Gray),
            ConnectionState.NotFound => ("장치가 연결되지 않음", BatteryPainter.Gray),
            _ => status.State switch
            {
                ChargeState.Charging => ("⚡ 충전 중", BatteryPainter.Blue),
                ChargeState.Full => ("✔ 충전 완료", BatteryPainter.Green),
                ChargeState.Discharging when status.Percent <= 20 => ("배터리 부족", BatteryPainter.Red),
                ChargeState.Discharging => ("사용 중 (방전)", Color.White),
                _ => ("상태 알 수 없음", SubText),
            },
        };

        if (status.Connection == ConnectionState.Connected && status.Estimated)
        {
            _status.Text += " · 추정치";
        }

        _updated.Text = status.UpdatedAt is DateTime t ? $"{t:HH:mm:ss} 확인" : "";
    }

    public void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _refreshButton.Text = busy ? "확인 중…" : "새로고침";
    }

    /// <summary>작업 표시줄(알림 영역) 근처, 화면 오른쪽 아래에 창을 띄웁니다.</summary>
    public void ShowNearTray()
    {
        if (!IsHandleCreated)
        {
            Opacity = 0;
            Show(); // 핸들 생성 + DPI 스케일링 적용 후 실제 크기로 위치를 계산
        }

        var screen = Screen.FromPoint(Cursor.Position);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        int margin = LogicalToDeviceUnits(12);

        int x = work.Right - Width - margin;
        int y = work.Bottom - Height - margin;
        if (work.Top > bounds.Top) y = work.Top + margin;      // 작업 표시줄이 위쪽
        if (work.Left > bounds.Left) x = work.Left + margin;   // 작업 표시줄이 왼쪽

        Location = new Point(x, y);
        Opacity = 1;
        Show();
        Activate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible && Opacity > 0)
        {
            LastAutoHiddenAt = DateTime.UtcNow;
        }
        Hide(); // 다른 곳을 클릭하면 닫힘
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; // Alt+F4 등은 숨기기만
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        KeyPreview = true;
        // Windows 11: 둥근 모서리 (Windows 10에서는 무시됨)
        int preference = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(Handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref preference, sizeof(int));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Border);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: Alt+Tab 목록에서 숨김
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
