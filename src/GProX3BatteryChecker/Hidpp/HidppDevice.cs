using System.Text;

namespace GProX3BatteryChecker.Hidpp;

/// <summary>HID++ 2.0 기능(feature) ID</summary>
internal static class HidppFeature
{
    public const ushort Root = 0x0000;
    public const ushort DeviceName = 0x0005;
    public const ushort BatteryStatus = 0x1000;
    public const ushort BatteryVoltage = 0x1001;
    public const ushort UnifiedBattery = 0x1004;
}

/// <summary>채널 위의 특정 device index에 있는 HID++ 2.0 장치.</summary>
internal sealed class HidppDevice
{
    private const int ProbeTimeoutMs = 400;
    private const int RequestTimeoutMs = 1200;

    private readonly Dictionary<ushort, byte?> _featureCache = [];

    public HidppChannel Channel { get; }
    public byte Index { get; }

    private HidppDevice(HidppChannel channel, byte index)
    {
        Channel = channel;
        Index = index;
    }

    /// <summary>Root.GetProtocolVersion(ping)으로 해당 index에 HID++ 2.0 장치가 있는지 확인합니다.</summary>
    public static HidppDevice? Probe(HidppChannel channel, byte index)
    {
        const byte pingData = 0x5A;
        try
        {
            var response = channel.Request(index, 0x00, 1, [0x00, 0x00, pingData], ProbeTimeoutMs);
            if (response is { Length: >= 3 } && response[0] >= 2 && response[2] == pingData)
            {
                return new HidppDevice(channel, index);
            }
        }
        catch (HidppException)
        {
            // HID++ 1.0 장치 등
        }

        return null;
    }

    public byte[]? Request(byte featureIndex, byte function, params byte[] parameters)
        => Channel.Request(Index, featureIndex, function, parameters, RequestTimeoutMs);

    /// <summary>Root.GetFeature: feature ID에 해당하는 feature index (지원하지 않으면 null)</summary>
    public byte? GetFeatureIndex(ushort featureId)
    {
        if (_featureCache.TryGetValue(featureId, out var cached))
        {
            return cached;
        }

        var response = Request(0x00, 0, (byte)(featureId >> 8), (byte)featureId);
        if (response is null)
        {
            throw new TimeoutException("No response from HID++ device.");
        }

        byte? index = response[0] == 0 ? null : response[0];
        _featureCache[featureId] = index;
        return index;
    }

    /// <summary>DeviceName(0x0005) 기능으로 장치 이름을 읽습니다.</summary>
    public string? GetName()
    {
        try
        {
            var featureIndex = GetFeatureIndex(HidppFeature.DeviceName);
            if (featureIndex is null)
            {
                return null;
            }

            var countResponse = Request(featureIndex.Value, 0);
            if (countResponse is null)
            {
                return null;
            }

            int length = countResponse[0];
            var bytes = new List<byte>(length);
            while (bytes.Count < length)
            {
                var chunk = Request(featureIndex.Value, 1, (byte)bytes.Count);
                if (chunk is null)
                {
                    break;
                }

                int before = bytes.Count;
                foreach (var b in chunk)
                {
                    if (b == 0 || bytes.Count >= length)
                    {
                        break;
                    }
                    bytes.Add(b);
                }

                if (bytes.Count == before)
                {
                    break;
                }
            }

            var name = Encoding.UTF8.GetString(bytes.ToArray()).Trim();
            return name.Length > 0 ? name : null;
        }
        catch (Exception ex) when (ex is HidppException or TimeoutException)
        {
            return null;
        }
    }
}
