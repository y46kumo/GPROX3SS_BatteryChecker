using Microsoft.Win32;

namespace GProX3BatteryChecker.Services;

internal static class WindowsTheme
{
    /// <summary>작업 표시줄(시스템)이 밝은 테마인지 여부.</summary>
    public static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }
}
