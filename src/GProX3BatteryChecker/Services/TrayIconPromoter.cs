using Microsoft.Win32;

namespace GProX3BatteryChecker.Services;

/// <summary>
/// Windows 11은 새 트레이 아이콘을 기본적으로 숨김 영역(^)에 넣습니다.
/// 이 앱의 아이콘을 작업 표시줄 오른쪽 아래에 항상 보이도록 설정합니다.
/// 사용자가 이미 직접 표시/숨김을 바꿨다면(IsPromoted 값이 있으면) 그 선택을 존중합니다.
/// </summary>
internal static class TrayIconPromoter
{
    private const string SettingsKey = @"Control Panel\NotifyIconSettings";

    /// <returns>이 앱의 항목을 찾았으면 true (더 이상 재시도할 필요 없음)</returns>
    public static bool TryPromote()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                return true;
            }

            using var settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            if (settings is null)
            {
                return true; // Windows 10 등: 해당 설정 없음
            }

            foreach (var name in settings.GetSubKeyNames())
            {
                using var entry = Registry.CurrentUser.OpenSubKey($@"{SettingsKey}\{name}", writable: true);
                if (entry?.GetValue("ExecutablePath") is not string path || !IsSamePath(path, exePath))
                {
                    continue;
                }

                if (entry.GetValue("IsPromoted") is null)
                {
                    entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                }
                return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    // 경로가 "{알려진 폴더 GUID}\하위경로" 형태로 저장되는 경우도 처리
    private static bool IsSamePath(string stored, string exePath)
    {
        if (stored.StartsWith('{'))
        {
            int end = stored.IndexOf('}');
            if (end > 0)
            {
                return exePath.EndsWith(stored[(end + 1)..], StringComparison.OrdinalIgnoreCase);
            }
        }

        return string.Equals(stored, exePath, StringComparison.OrdinalIgnoreCase);
    }
}
