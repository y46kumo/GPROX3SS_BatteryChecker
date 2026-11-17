using GProX3BatteryChecker.Hidpp;
using GProX3BatteryChecker.Services;
using GProX3BatteryChecker.UI;
using Microsoft.Win32;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace GProX3BatteryChecker;

/// <summary>작업 표시줄 오른쪽 아래(알림 영역)에 상주하는 앱 본체.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int ConnectedIntervalMs = 30_000;  // 연결됨: 30초마다 갱신
    private const int NotFoundIntervalMs = 15_000;   // 미연결: 15초마다 재검색
    private static readonly int[] WarningLevels = [15, 5];

    private readonly LogitechBatteryService _service = new();
    private readonly TrayIconRenderer _iconRenderer = new();
    private readonly NotifyIcon _tray;
    private readonly StatusPopup _popup;
    private readonly WinFormsTimer _pollTimer;
    private readonly WinFormsTimer _promoteTimer;
    private readonly ToolStripMenuItem _autoStartItem;

    private BatteryStatus _status = BatteryStatus.Searching;
    private BatteryStatus? _lastConnected;
    private bool _busy;
    private bool _exiting;
    private int _promoteAttempts;
    private readonly HashSet<int> _warnedLevels = [];
    private bool _showPopupAfterFirstRead;

    public TrayApplicationContext(bool startedByAutoStart)
    {
        _popup = new StatusPopup();
        _popup.RefreshRequested += async (_, _) => await RefreshAsync();
        _popup.OpenGHubRequested += (_, _) => OpenGHub();

        _autoStartItem = new ToolStripMenuItem("Windows 시작 시 자동 실행") { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => ToggleAutoStart();

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("배터리 상태 보기", null, (_, _) => _popup.ShowNearTray())
        {
            Font = new Font(menu.Font, FontStyle.Bold),
        });
        menu.Items.Add("지금 새로고침", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("G HUB 열기", null, (_, _) => OpenGHub());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => _autoStartItem.Checked = SafeIsAutoStartEnabled();

        _tray = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = $"{AppInfo.DefaultDeviceName}\n장치를 찾는 중…",
        };
        _tray.MouseClick += OnTrayMouseClick;
        UpdateTrayIcon();
        _tray.Visible = true;

        _pollTimer = new WinFormsTimer { Interval = ConnectedIntervalMs };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        // Windows 11에서 아이콘이 숨김 영역(^)이 아니라 작업 표시줄에 바로 보이도록 설정
        _promoteTimer = new WinFormsTimer { Interval = 2_000 };
        _promoteTimer.Tick += (_, _) => PromoteTrayIcon();
        _promoteTimer.Start();

        SystemEvents.UserPreferenceChanged += OnSystemAppearanceChanged;
        SystemEvents.DisplaySettingsChanged += OnSystemAppearanceChanged;

        // 직접 실행했을 때는 첫 측정 후 상태 창을 한 번 보여줌
        _showPopupAfterFirstRead = !startedByAutoStart;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy || _exiting)
        {
            return;
        }

        _busy = true;
        _popup.SetBusy(true);
        try
        {
            var reading = await Task.Run(() =>
            {
                try { return _service.Read(); }
                catch { return null; }
            });

            if (_exiting)
            {
                return;
            }

            ApplyStatus(BatteryStatus.From(reading, DateTime.Now));
        }
        finally
        {
            _busy = false;
            if (!_exiting)
            {
                _popup.SetBusy(false);
            }
        }

        if (_showPopupAfterFirstRead)
        {
            _showPopupAfterFirstRead = false;
            _popup.ShowNearTray();
        }
    }

    private void ApplyStatus(BatteryStatus status)
    {
        if (status.Connection == ConnectionState.Connected)
        {
            _lastConnected = status;
        }
        else if (_lastConnected is not null)
        {
            // 마우스가 절전/꺼짐 상태이면 마지막으로 읽은 값을 흐리게 표시
            status = _lastConnected with { Connection = ConnectionState.NotFound };
        }

        _status = status;
        UpdateTrayIcon();
        _popup.ShowStatus(status);

        _pollTimer.Interval = status.Connection == ConnectionState.Connected
            ? ConnectedIntervalMs
            : NotFoundIntervalMs;

        CheckLowBattery(status);
    }

    private void UpdateTrayIcon()
    {
        _tray.Icon = _iconRenderer.Render(_status.Percent, _status.State,
                                          WindowsTheme.IsTaskbarLight(), _status.IsStale);
        _tray.Text = BuildTooltip(_status);
    }

    private static string BuildTooltip(BatteryStatus status)
    {
        var name = status.DeviceName ?? AppInfo.DefaultDeviceName;
        var text = status.Connection switch
        {
            ConnectionState.Searching => $"{name}\n장치를 찾는 중…",
            ConnectionState.NotFound when status.IsStale =>
                $"{name}\n배터리 {status.Percent}% (마지막 확인 {status.UpdatedAt:HH:mm}, 현재 연결 끊김)",
            ConnectionState.NotFound => $"{name}\n장치가 연결되지 않음",
            _ => $"{name}\n배터리 {status.Percent}%" + status.State switch
            {
                ChargeState.Charging => " (충전 중)",
                ChargeState.Full => " (충전 완료)",
                _ => "",
            },
        };
        return text.Length > 127 ? text[..127] : text;
    }

    private void CheckLowBattery(BatteryStatus status)
    {
        if (status.Connection != ConnectionState.Connected || status.Percent is not int percent)
        {
            return;
        }

        if (status.State != ChargeState.Discharging || percent > WarningLevels.Max() + 5)
        {
            _warnedLevels.Clear(); // 충전하면 경고 상태 초기화
            return;
        }

        // 도달한 가장 낮은 경고 단계 (15% → 5%)에 대해 한 번만 알림
        int level = WarningLevels.Where(l => percent <= l).DefaultIfEmpty(-1).Min();
        if (level < 0 || _warnedLevels.Contains(level))
        {
            return;
        }

        foreach (var l in WarningLevels.Where(l => l >= level))
        {
            _warnedLevels.Add(l);
        }

        _tray.ShowBalloonTip(5000, "배터리 부족",
            $"{status.DeviceName ?? AppInfo.DefaultDeviceName}의 배터리가 {percent}% 남았습니다. 충전해 주세요.",
            ToolTipIcon.Warning);
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else if ((DateTime.UtcNow - _popup.LastAutoHiddenAt).TotalMilliseconds > 300)
        {
            _popup.ShowNearTray();
            _ = RefreshAsync();
        }
    }

    private void OpenGHub()
    {
        _popup.Hide();
        GHubLauncher.Open();
    }

    private void ToggleAutoStart()
    {
        try
        {
            AutoStart.SetEnabled(_autoStartItem.Checked);
        }
        catch (Exception ex)
        {
            _autoStartItem.Checked = !_autoStartItem.Checked;
            MessageBox.Show($"자동 실행 설정을 변경하지 못했습니다.\n\n{ex.Message}",
                AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool SafeIsAutoStartEnabled()
    {
        try { return AutoStart.IsEnabled; }
        catch { return false; }
    }

    private void PromoteTrayIcon()
    {
        _promoteAttempts++;
        // 탐색기가 설정 항목을 만들 때까지 잠시 걸릴 수 있으므로 몇 번 재시도
        if (TrayIconPromoter.TryPromote() || _promoteAttempts >= 10)
        {
            _promoteTimer.Stop();
        }
        else
        {
            _promoteTimer.Interval = 10_000;
        }
    }

    private void OnSystemAppearanceChanged(object? sender, EventArgs e)
    {
        if (!_exiting)
        {
            UpdateTrayIcon(); // 테마(밝게/어둡게)나 DPI가 바뀌면 아이콘 다시 그리기
        }
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        SystemEvents.UserPreferenceChanged -= OnSystemAppearanceChanged;
        SystemEvents.DisplaySettingsChanged -= OnSystemAppearanceChanged;
        _pollTimer.Stop();
        _promoteTimer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _popup.Dispose();
        _iconRenderer.Dispose();
        if (!_busy)
        {
            _service.Dispose();
        }
        base.ExitThreadCore();
    }
}
