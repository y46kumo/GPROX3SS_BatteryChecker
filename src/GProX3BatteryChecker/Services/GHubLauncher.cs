using System.Diagnostics;
using Microsoft.Win32;

namespace GProX3BatteryChecker.Services;

/// <summary>Logitech G HUB 실행.</summary>
internal static class GHubLauncher
{
    private const string DownloadUrl = "https://www.logitechg.com/ko-kr/innovation/g-hub.html";

    public static void Open(IWin32Window? owner = null)
    {
        var path = FindExecutable();
        if (path is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path)!,
                });
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"G HUB를 실행하지 못했습니다.\n\n{ex.Message}",
                    AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        var answer = MessageBox.Show(owner,
            "Logitech G HUB가 설치되어 있지 않은 것 같습니다.\n다운로드 페이지를 여시겠습니까?",
            AppInfo.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
        }
    }

    public static string? FindExecutable()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        [
            Path.Combine(programFiles, "LGHUB", "lghub.exe"),
            Path.Combine(programFiles, "LGHUB", "system_tray", "lghub_system_tray.exe"),
        ];

        foreach (var candidate in candidates.Concat(FromUninstallRegistry()))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>기본 경로가 아닌 곳에 설치된 경우를 위해 제거 프로그램 레지스트리를 검색합니다.</summary>
    private static IEnumerable<string> FromUninstallRegistry()
    {
        const string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        var results = new List<string>();

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var uninstall = root.OpenSubKey(uninstallKey);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var app = uninstall.OpenSubKey(name);
                    if (app?.GetValue("DisplayName") is not string displayName ||
                        !displayName.Contains("G HUB", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (app.GetValue("InstallLocation") is string location && location.Length > 0)
                    {
                        results.Add(Path.Combine(location.Trim('"'), "lghub.exe"));
                    }

                    if (app.GetValue("DisplayIcon") is string icon && icon.Length > 0)
                    {
                        var iconPath = icon.Split(',')[0].Trim('"');
                        if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(Path.Combine(Path.GetDirectoryName(iconPath) ?? "", "lghub.exe"));
                            results.Add(iconPath);
                        }
                    }
                }
            }
            catch
            {
                // 레지스트리 접근 실패는 무시
            }
        }

        return results;
    }
}
