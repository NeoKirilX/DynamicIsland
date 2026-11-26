using System.Windows.Threading;

namespace DynamicIsland;

public readonly record struct SystemInfo(int CpuPercent, int RamPercent, double RamUsedGb, double RamTotalGb);

sealed class SystemService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.8);
    readonly DispatcherTimer _timer;

    public SystemService()
    {
        Current = Sample();
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) =>
        {
            Current = Sample();
            Changed?.Invoke(Current);
        };
        _timer.Start();
    }

    public SystemInfo Current { get; private set; }

    public int CpuPercent => Current.CpuPercent;
    public int RamPercent => Current.RamPercent;

    public event Action<SystemInfo>? Changed;

    public static SystemInfo Sample()
    {
        int cpu = Native.GetCpuUsage();
        Native.TryGetMemory(out int ram, out double usedGb, out double totalGb);
        return new SystemInfo(cpu, ram, usedGb, totalGb);
    }
}
