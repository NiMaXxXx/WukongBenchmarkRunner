using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WukongBenchmarkRunner.SystemInfo;

public static class SystemInfoCollector
{
    public static SystemSpecs Collect(string? gameInstallDir = null)
    {
        return new SystemSpecs
        {
            Os = Safe(GetOs),
            Cpu = Safe(GetCpu),
            Gpus = Safe(GetGpus) ?? [],
            Ram = Safe(GetRam),
            Motherboard = Safe(GetMotherboard),
            Display = Safe(GetDisplay),
            GameDisk = gameInstallDir is null ? null : Safe(() => GetDiskForPath(gameInstallDir)),
            PowerPlan = Safe(GetPowerPlan),
            HardwareGpuScheduling = Safe(GetHags),
        };
    }

    private static string? GetOs()
    {
        using var obj = QueryFirst("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");
        if (obj is null) return null;
        var displayVersion = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", null) as string;
        var caption = obj["Caption"]?.ToString()?.Replace("Microsoft ", "").Replace("Майкрософт ", "").Trim();
        return $"{caption} {displayVersion} (build {obj["Version"]}, {obj["OSArchitecture"]})".Replace("  ", " ");
    }

    private static CpuInfo? GetCpu()
    {
        using var obj = QueryFirst("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
        if (obj is null) return null;
        return new CpuInfo(
            obj["Name"]?.ToString()?.Trim() ?? "?",
            Convert.ToInt32(obj["NumberOfCores"]),
            Convert.ToInt32(obj["NumberOfLogicalProcessors"]),
            Convert.ToInt32(obj["MaxClockSpeed"]));
    }

    private static List<GpuInfo> GetGpus()
    {
        var vram = ReadVramFromRegistry();
        var result = new List<GpuInfo>();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM, PNPDeviceID FROM Win32_VideoController");
        foreach (ManagementObject obj in searcher.Get())
        {
            using (obj)
            {
                var name = obj["Name"]?.ToString() ?? "?";
                var pnp = obj["PNPDeviceID"]?.ToString() ?? "";
                // Пропускаем виртуальные адаптеры (Basic Display, Parsec и т.п.).
                if (!pnp.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase))
                    continue;

                // AdapterRAM — uint32, больше 4 ГБ не показывает.
                long? bytes = vram.TryGetValue(name, out var v) ? v : null;
                bytes ??= obj["AdapterRAM"] is { } ram ? Convert.ToInt64(ram) : null;

                result.Add(new GpuInfo(name, obj["DriverVersion"]?.ToString() ?? "?",
                    bytes is > 0 ? bytes / 1024.0 / 1024 / 1024 : null,
                    NvidiaDriverVersion(obj["DriverVersion"]?.ToString(), name)));
            }
        }

        return result;
    }

    private static Dictionary<string, long> ReadVramFromRegistry()
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
        if (classKey is null) return map;

        foreach (var sub in classKey.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
        {
            try
            {
                using var key = classKey.OpenSubKey(sub);
                if (key?.GetValue("DriverDesc") is not string desc) continue;
                if (key.GetValue("HardwareInformation.qwMemorySize") is long qw)
                    map[desc] = qw;
                else if (key.GetValue("HardwareInformation.qwMemorySize") is byte[] { Length: >= 8 } b)
                    map[desc] = BitConverter.ToInt64(b, 0);
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        return map;
    }

    // 32.0.15.6094 -> 560.94
    private static string? NvidiaDriverVersion(string? wmiVersion, string gpuName)
    {
        if (wmiVersion is null || !gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return null;
        var digits = wmiVersion.Replace(".", "");
        if (digits.Length < 5) return null;
        var last5 = digits[^5..];
        return $"{last5[..3]}.{last5[3..]}";
    }

    private static RamInfo? GetRam()
    {
        using var cs = QueryFirst("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        var total = cs is null ? 0 : Convert.ToDouble(cs["TotalPhysicalMemory"]) / 1024 / 1024 / 1024;

        var modules = new List<string>();
        var speeds = new List<int>();
        using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber FROM Win32_PhysicalMemory");
        foreach (ManagementObject m in searcher.Get())
        {
            using (m)
            {
                var gb = Convert.ToDouble(m["Capacity"]) / 1024 / 1024 / 1024;
                var speed = ToInt(m["ConfiguredClockSpeed"]) is > 0 and var c ? c : ToInt(m["Speed"]);
                if (speed > 0) speeds.Add(speed);
                modules.Add($"{gb:0} ГБ {m["Manufacturer"]?.ToString()?.Trim()} {m["PartNumber"]?.ToString()?.Trim()}".Trim());
            }
        }

        return new RamInfo(total, modules.Count, speeds.Count > 0 ? speeds.Max() : null, modules);
    }

    private static string? GetMotherboard()
    {
        using var obj = QueryFirst("SELECT Manufacturer, Product FROM Win32_BaseBoard");
        return obj is null ? null : $"{obj["Manufacturer"]} {obj["Product"]}".Trim();
    }

    private static DisplayInfo? GetDisplay()
    {
        var mode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode))
            return null;
        return new DisplayInfo(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency);
    }

    private static string? GetDiskForPath(string path)
    {
        var letter = Path.GetPathRoot(Path.GetFullPath(path))?.TrimEnd('\\', ':');
        if (string.IsNullOrEmpty(letter)) return null;

        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        using var partSearcher = new ManagementObjectSearcher(scope,
            new ObjectQuery($"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{letter[0]}'"));
        var diskNumber = partSearcher.Get().Cast<ManagementObject>().Select(p => p["DiskNumber"]?.ToString()).FirstOrDefault();
        if (diskNumber is null) return null;

        using var diskSearcher = new ManagementObjectSearcher(scope,
            new ObjectQuery($"SELECT FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskNumber}'"));
        var disk = diskSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
        if (disk is null) return null;

        var media = ToInt(disk["MediaType"]) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "?" };
        var bus = ToInt(disk["BusType"]) switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", 8 => "RAID", _ => null };
        var size = Convert.ToDouble(disk["Size"]) / 1000 / 1000 / 1000;
        return $"{disk["FriendlyName"]} ({(bus is null ? media : $"{media} {bus}")}, {size:0} ГБ) — диск {letter}:";
    }

    private static string? GetPowerPlan()
    {
        var scope = new ManagementScope(@"\\.\root\cimv2\power");
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT ElementName FROM Win32_PowerPlan WHERE IsActive = true"));
        return searcher.Get().Cast<ManagementObject>().Select(p => p["ElementName"]?.ToString()).FirstOrDefault();
    }

    private static string? GetHags()
    {
        var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null);
        return value switch
        {
            2 => "включено",
            1 => "выключено",
            _ => null,
        };
    }

    private static ManagementObject? QueryFirst(string query)
    {
        using var searcher = new ManagementObjectSearcher(query);
        return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
    }

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);

    private static T? Safe<T>(Func<T?> func) where T : class
    {
        try { return func(); }
        catch { return null; }
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
}
