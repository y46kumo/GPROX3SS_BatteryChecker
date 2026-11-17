using GProX3BatteryChecker.Hidpp;

namespace GProX3BatteryChecker;

public enum ConnectionState
{
    Searching,
    Connected,
    NotFound,
}

/// <summary>UI에 표시할 현재 상태.</summary>
public sealed record BatteryStatus(
    ConnectionState Connection,
    string? DeviceName,
    int? Percent,
    ChargeState State,
    bool Estimated,
    DateTime? UpdatedAt)
{
    /// <summary>지금은 연결되지 않았지만 마지막으로 읽은 값을 보여주는 중</summary>
    public bool IsStale => Connection != ConnectionState.Connected && Percent is not null;

    public static BatteryStatus Searching { get; } =
        new(ConnectionState.Searching, null, null, ChargeState.Unknown, false, null);

    public static BatteryStatus From(BatteryReading? reading, DateTime now) => reading is null
        ? new(ConnectionState.NotFound, null, null, ChargeState.Unknown, false, now)
        : new(ConnectionState.Connected, reading.DeviceName, reading.Percent, reading.State, reading.Estimated, now);
}
