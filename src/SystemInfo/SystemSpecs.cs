namespace WukongBenchmarkRunner.SystemInfo;

public sealed class SystemSpecs
{
    public string? Os { get; init; }
    public CpuInfo? Cpu { get; init; }
    public List<GpuInfo> Gpus { get; init; } = [];
    public RamInfo? Ram { get; init; }
    public string? Motherboard { get; init; }
    public DisplayInfo? Display { get; init; }
    public string? GameDisk { get; init; }
    public string? PowerPlan { get; init; }
    public string? HardwareGpuScheduling { get; init; }
}

public sealed record CpuInfo(string Name, int Cores, int Threads, int MaxClockMhz);

public sealed record GpuInfo(string Name, string DriverVersion, double? VramGb, string? VendorDriverVersion);

public sealed record RamInfo(double TotalGb, int Modules, int? SpeedMts, List<string> ModuleDetails);

public sealed record DisplayInfo(int Width, int Height, int RefreshHz);
