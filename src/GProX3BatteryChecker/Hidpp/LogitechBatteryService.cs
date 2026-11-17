using HidSharp;

namespace GProX3BatteryChecker.Hidpp;

/// <summary>
/// 연결된 Logitech 장치(LIGHTSPEED 수신기 또는 USB 유선 연결) 중에서
/// G PRO X3 SUPERSTRIKE를 찾아 배터리 잔량을 읽어옵니다.
/// 스레드 안전하지 않으므로 한 번에 하나의 스레드에서만 호출해야 합니다.
/// </summary>
public sealed class LogitechBatteryService : IDisposable
{
    private const int LogitechVendorId = 0x046D;

    // 0xFF = USB로 직접 연결된 장치, 1~6 = 수신기에 페어링된 장치 슬롯
    private static readonly byte[] DirectIndex = [0xFF];
    private static readonly byte[] ReceiverIndices = [1, 2, 3, 4, 5, 6];

    private sealed record Connection(HidppDevice Device, BatteryFeature Battery, string Name);

    private Connection? _connection;

    /// <summary>배터리 상태를 읽습니다. 장치를 찾지 못하면 null.</summary>
    public BatteryReading? Read()
    {
        if (_connection is not null)
        {
            var reading = TryRead(_connection);
            if (reading is not null)
            {
                return reading;
            }

            Disconnect();
        }

        _connection = Discover();
        return _connection is null ? null : TryRead(_connection);
    }

    private static BatteryReading? TryRead(Connection connection)
    {
        try
        {
            var result = connection.Battery.Read(connection.Device);
            return result is { } r
                ? new BatteryReading(connection.Name, r.Percent, r.State, r.Estimated)
                : null;
        }
        catch
        {
            return null; // 장치 분리(IOException), HID++ 오류 등
        }
    }

    private static Connection? Discover()
    {
        var candidates = new List<(Connection Conn, int Score)>();
        var openedChannels = new List<HidppChannel>();

        IEnumerable<HidDevice> hidDevices;
        try
        {
            hidDevices = DeviceList.Local.GetHidDevices(LogitechVendorId).ToList();
        }
        catch
        {
            return null;
        }

        foreach (var hid in hidDevices)
        {
            var channel = HidppChannel.TryOpen(hid);
            if (channel is null)
            {
                continue;
            }
            openedChannels.Add(channel);

            try
            {
                // 먼저 USB 직접 연결(0xFF)을 확인하고,
                // 배터리 장치가 아니면(=수신기) 수신기 슬롯 1~6을 순회
                int before = candidates.Count;
                AddCandidates(candidates, channel, hid, DirectIndex);
                if (candidates.Count == before)
                {
                    AddCandidates(candidates, channel, hid, ReceiverIndices);
                }
            }
            catch
            {
                // 이 컬렉션은 건너뜀
            }
        }

        var best = candidates
            .OrderByDescending(c => c.Score)
            .Select(c => c.Conn)
            .FirstOrDefault();

        foreach (var channel in openedChannels)
        {
            if (!ReferenceEquals(channel, best?.Device.Channel))
            {
                channel.Dispose();
            }
        }

        return best;
    }

    private static void AddCandidates(List<(Connection Conn, int Score)> candidates,
                                      HidppChannel channel, HidDevice hid, byte[] indices)
    {
        foreach (var index in indices)
        {
            if (HidppDevice.Probe(channel, index) is { } device &&
                TryCreateConnection(device, hid) is { } conn)
            {
                candidates.Add((conn, Score(conn.Name)));
            }
        }
    }

    private static Connection? TryCreateConnection(HidppDevice device, HidDevice hid)
    {
        try
        {
            var battery = BatteryFeature.Detect(device);
            if (battery is null)
            {
                return null;
            }

            var name = device.GetName() ?? SafeProductName(hid) ?? "Logitech 장치";
            return new Connection(device, battery, name);
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeProductName(HidDevice hid)
    {
        try { return hid.GetProductName(); }
        catch { return null; }
    }

    /// <summary>G PRO X3 SUPERSTRIKE를 우선 선택하고, 없으면 다른 G PRO / 기타 장치 순.</summary>
    internal static int Score(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper.Contains("SUPERSTRIKE"))
        {
            return upper.Contains("X3") ? 4 : 3;
        }
        if (upper.Contains("PRO X"))
        {
            return 2;
        }
        return 1;
    }

    private void Disconnect()
    {
        _connection?.Device.Channel.Dispose();
        _connection = null;
    }

    public void Dispose() => Disconnect();
}
