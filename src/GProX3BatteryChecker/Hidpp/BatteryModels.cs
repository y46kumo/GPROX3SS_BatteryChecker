namespace GProX3BatteryChecker.Hidpp;

public enum ChargeState
{
    Unknown,
    Discharging,
    Charging,
    Full,
}

/// <summary>배터리 측정 결과.</summary>
/// <param name="Percent">0~100</param>
/// <param name="Estimated">전압으로부터 추정한 값이면 true</param>
public sealed record BatteryReading(string DeviceName, int Percent, ChargeState State, bool Estimated);
