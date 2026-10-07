using System.Text.Json;
using System.Text.Json.Serialization;

namespace WukongBenchmarkRunner.Results;

// %TEMP%\b1\BenchMarkHistory\Tool\<unix-время>, имена полей как в файле.
public sealed class BenchmarkResult
{
    public long TimeStamp { get; set; }
    public double FPSAvg { get; set; }
    public double FPSMax { get; set; }
    public double FPSMin { get; set; }
    public double FPS95 { get; set; } // 5-й перцентиль
    public double CPUAvg { get; set; }
    public double GPUAvg { get; set; }
    public double VideoMem { get; set; } // ГБ

    public string? GameVer { get; set; }
    public string? SysVer { get; set; }
    public string? CPUModel { get; set; }
    public string? GPUModel { get; set; }
    public string? GpuDriverVer { get; set; }
    public string? VideoMemSize { get; set; }
    public string? SysMem { get; set; }

    public int ScreenMode { get; set; }
    public string? ScreenResolution { get; set; }
    public int QualityLevel { get; set; }
    public int ImageQuality { get; set; } // масштаб рендера, %
    public int ViewDistance { get; set; }
    public int AntiAliasing { get; set; }
    public int PostProcessing { get; set; }
    public int ShadowQuality { get; set; }
    public int TextureQuality { get; set; }
    public int MaterialQuality { get; set; }
    public int VegetationQuality { get; set; }
    public int MotionBlur { get; set; }
    public int Rtx { get; set; }
    public int Dlss { get; set; } // апскейлер, кодировка как у SuperResolutionSampling
    public int InsertFrame { get; set; }
    public int Dx12 { get; set; }

    public List<FrameRecord> Records { get; set; } = [];

    [JsonIgnore]
    public string? SourcePath { get; set; }

    public static BenchmarkResult Load(string path)
    {
        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<BenchmarkResult>(json)
                     ?? throw new InvalidDataException($"Пустой файл результата: {path}");
        result.SourcePath = path;
        return result;
    }
}

public sealed class FrameRecord
{
    public long TimeStamp { get; set; } // мс
    public double FrameRate { get; set; }
    public double CPUUsage { get; set; }
    public double GPUUsage { get; set; }
    public double CPUFrameTime { get; set; }
    public double GPUFrameTime { get; set; }
    public double VideoMemoryUsage { get; set; }
}
