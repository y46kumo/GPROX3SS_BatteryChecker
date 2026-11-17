using System.Diagnostics;
using HidSharp;

namespace GProX3BatteryChecker.Hidpp;

/// <summary>장치가 HID++ 2.0 오류 응답(feature index 0xFF)을 보냈을 때 발생합니다.</summary>
internal sealed class HidppException(byte errorCode)
    : Exception($"HID++ error 0x{errorCode:X2}")
{
    public byte ErrorCode { get; } = errorCode;
}

/// <summary>
/// Logitech 수신기/장치의 HID++ "long report"(0x11, 20바이트) 컬렉션 하나에 대한 통신 채널.
/// Windows에서는 HID 장치를 공유 모드로 열기 때문에 G HUB가 실행 중이어도 함께 사용할 수 있습니다.
/// </summary>
internal sealed class HidppChannel : IDisposable
{
    public const byte ReportIdLong = 0x11;
    private const int LongReportLength = 20;

    private readonly HidStream _stream;
    private readonly byte[] _readBuffer;
    private byte _softwareId;

    public HidDevice Device { get; }

    private HidppChannel(HidDevice device, HidStream stream, int inputLength)
    {
        Device = device;
        _stream = stream;
        _readBuffer = new byte[Math.Max(inputLength, LongReportLength)];
    }

    /// <summary>HID++ long report 컬렉션인 경우에만 장치를 엽니다.</summary>
    public static HidppChannel? TryOpen(HidDevice device)
    {
        try
        {
            if (device.GetMaxOutputReportLength() != LongReportLength ||
                device.GetMaxInputReportLength() != LongReportLength)
            {
                return null;
            }

            return device.TryOpen(out HidStream stream)
                ? new HidppChannel(device, stream, device.GetMaxInputReportLength())
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// HID++ 2.0 요청을 보내고 응답 파라미터(헤더 4바이트 제외)를 돌려줍니다.
    /// 시간 내에 응답이 없으면 null (장치 꺼짐/미연결 등).
    /// </summary>
    public byte[]? Request(byte deviceIndex, byte featureIndex, byte function,
                           ReadOnlySpan<byte> parameters, int timeoutMs)
    {
        // software id(1~15)를 매번 바꿔서 이전 요청의 늦은 응답과 섞이지 않게 합니다.
        _softwareId = (byte)(_softwareId % 0x0F + 1);
        byte functionAndSwId = (byte)((function << 4) | _softwareId);

        var report = new byte[LongReportLength];
        report[0] = ReportIdLong;
        report[1] = deviceIndex;
        report[2] = featureIndex;
        report[3] = functionAndSwId;
        parameters.CopyTo(report.AsSpan(4));

        _stream.Write(report);

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            int remaining = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
            if (remaining <= 0)
            {
                return null;
            }

            int read;
            try
            {
                _stream.ReadTimeout = remaining;
                read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
            }
            catch (TimeoutException)
            {
                return null;
            }

            var buf = _readBuffer;
            if (read < 7 || buf[0] != ReportIdLong || buf[1] != deviceIndex)
            {
                continue; // 다른 장치/알림(이벤트) 보고서는 무시
            }

            if (buf[2] == featureIndex && buf[3] == functionAndSwId)
            {
                return buf.AsSpan(4, read - 4).ToArray();
            }

            if (buf[2] == 0xFF && buf[3] == featureIndex && buf[4] == functionAndSwId)
            {
                throw new HidppException(buf[5]);
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
