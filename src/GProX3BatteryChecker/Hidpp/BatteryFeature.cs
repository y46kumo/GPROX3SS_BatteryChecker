namespace GProX3BatteryChecker.Hidpp;

/// <summary>
/// 장치가 지원하는 배터리 기능 하나를 감싸는 클래스.
/// 최신 장치는 UNIFIED_BATTERY(0x1004), 구형 게이밍 장치는 BATTERY_VOLTAGE(0x1001),
/// 그 외 일부 장치는 BATTERY_STATUS(0x1000)를 사용합니다.
/// </summary>
internal sealed class BatteryFeature
{
    private enum Kind { Unified, Status, Voltage }

    private readonly Kind _kind;
    private readonly byte _featureIndex;
    private readonly bool _reportsStateOfCharge;

    private BatteryFeature(Kind kind, byte featureIndex, bool reportsStateOfCharge = true)
    {
        _kind = kind;
        _featureIndex = featureIndex;
        _reportsStateOfCharge = reportsStateOfCharge;
    }

    public static BatteryFeature? Detect(HidppDevice device)
    {
        if (device.GetFeatureIndex(HidppFeature.UnifiedBattery) is byte unified)
        {
            // GetCapabilities: [지원 레벨 비트마스크, 플래그(bit1 = 잔량% 보고 지원)]
            var caps = device.Request(unified, 0);
            bool soc = caps is null || (caps[1] & 0x02) != 0;
            return new BatteryFeature(Kind.Unified, unified, soc);
        }

        if (device.GetFeatureIndex(HidppFeature.BatteryStatus) is byte status)
        {
            return new BatteryFeature(Kind.Status, status);
        }

        if (device.GetFeatureIndex(HidppFeature.BatteryVoltage) is byte voltage)
        {
            return new BatteryFeature(Kind.Voltage, voltage);
        }

        return null;
    }

    /// <returns>(잔량 %, 충전 상태, 추정치 여부) — 응답이 없으면 null</returns>
    public (int Percent, ChargeState State, bool Estimated)? Read(HidppDevice device)
    {
        var r = device.Request(_featureIndex, _kind == Kind.Unified ? (byte)1 : (byte)0);
        if (r is null || r.Length < 4)
        {
            return null;
        }

        return _kind switch
        {
            Kind.Unified => ReadUnified(r),
            Kind.Status => ReadStatus(r),
            _ => ReadVoltage(r),
        };
    }

    // 0x1004 GetStatus: [잔량%, 레벨 비트마스크, 충전 상태, 외부 전원]
    private (int, ChargeState, bool) ReadUnified(byte[] r)
    {
        int percent = r[0];
        bool estimated = false;
        if (!_reportsStateOfCharge || (percent == 0 && r[1] != 0))
        {
            estimated = true;
            percent = r[1] switch
            {
                >= 8 => 100, // full
                >= 4 => 60,  // good
                >= 2 => 20,  // low
                >= 1 => 5,   // critical
                _ => 0,
            };
        }

        var state = r[2] switch
        {
            0 => ChargeState.Discharging,
            1 or 2 => ChargeState.Charging,
            3 => ChargeState.Full,
            _ => ChargeState.Unknown,
        };
        if (state == ChargeState.Full)
        {
            percent = Math.Max(percent, 100);
        }

        return (Math.Clamp(percent, 0, 100), state, estimated);
    }

    // 0x1000 GetBatteryLevelStatus: [잔량%, 다음 레벨, 상태]
    private static (int, ChargeState, bool) ReadStatus(byte[] r)
    {
        var state = r[2] switch
        {
            0 => ChargeState.Discharging,
            1 or 2 or 4 => ChargeState.Charging,
            3 => ChargeState.Full,
            _ => ChargeState.Unknown,
        };
        int percent = state == ChargeState.Full ? 100 : r[0];
        return (Math.Clamp(percent, 0, 100), state, false);
    }

    // 0x1001 GetBatteryVoltage: [전압(mV) 상위, 하위, 플래그]
    private static (int, ChargeState, bool) ReadVoltage(byte[] r)
    {
        int millivolts = (r[0] << 8) | r[1];
        byte flags = r[2];

        ChargeState state;
        if ((flags & 0x80) != 0)
        {
            state = (flags & 0x07) switch
            {
                0 => ChargeState.Charging,
                1 => ChargeState.Full,
                2 => ChargeState.Discharging, // 충전 중단
                _ => ChargeState.Unknown,
            };
        }
        else
        {
            state = ChargeState.Discharging;
        }

        int percent = state == ChargeState.Full ? 100 : VoltageToPercent(millivolts);
        return (percent, state, true);
    }

    // 1셀 리튬폴리머 방전 곡선 근사 (mV → %)
    private static readonly (int Mv, int Pct)[] VoltageCurve =
    [
        (4186, 100), (4067, 90), (3989, 80), (3922, 70), (3859, 60), (3811, 50),
        (3778, 40), (3751, 30), (3717, 20), (3671, 10), (3646, 5), (3579, 2), (3500, 0),
    ];

    internal static int VoltageToPercent(int millivolts)
    {
        if (millivolts >= VoltageCurve[0].Mv)
        {
            return 100;
        }

        for (int i = 1; i < VoltageCurve.Length; i++)
        {
            var (hiMv, hiPct) = VoltageCurve[i - 1];
            var (loMv, loPct) = VoltageCurve[i];
            if (millivolts >= loMv)
            {
                double t = (double)(millivolts - loMv) / (hiMv - loMv);
                return (int)Math.Round(loPct + t * (hiPct - loPct));
            }
        }

        return 0;
    }
}
