using GProX3BatteryChecker.Services;

namespace GProX3BatteryChecker;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // 중복 실행 방지
        using var mutex = new Mutex(initiallyOwned: true, @"Local\GProX3BatteryChecker", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        bool autoStarted = args.Any(a => a.Equals(AutoStart.Argument, StringComparison.OrdinalIgnoreCase));
        Application.Run(new TrayApplicationContext(autoStarted));
    }
}
