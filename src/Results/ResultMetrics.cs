namespace WukongBenchmarkRunner.Results;

public sealed record ResultMetrics(
    int Samples,
    double DurationSec,
    double OnePercentLowFps,
    double AvgCpuFrameTimeMs,
    double AvgGpuFrameTimeMs,
    double AvgGpuUsagePercent,
    double GpuBusyPercent,
    double PeakVramGb)
{
    // Если GPU занят заметно меньше длительности кадра, он ждёт процессор.
    public string Bottleneck => GpuBusyPercent switch
    {
        < 85 => "процессор (GPU часть кадра простаивает)",
        _ => "видеокарта (GPU занят почти весь кадр)",
    };

    public static ResultMetrics From(BenchmarkResult r)
    {
        var records = r.Records.Where(x => x.FrameRate > 0).ToList();
        if (records.Count == 0)
            return new ResultMetrics(0, 0, 0, 0, 0, 0, 0, r.VideoMem);

        var fps = records.Select(x => x.FrameRate).OrderBy(x => x).ToList();
        var avgFrameTime = records.Average(x => 1000.0 / x.FrameRate);
        var avgGpu = records.Average(x => x.GPUFrameTime);

        return new ResultMetrics(
            Samples: records.Count,
            DurationSec: (records[^1].TimeStamp - records[0].TimeStamp) / 1000.0,
            OnePercentLowFps: Percentile(fps, 0.01),
            AvgCpuFrameTimeMs: records.Average(x => x.CPUFrameTime),
            AvgGpuFrameTimeMs: avgGpu,
            AvgGpuUsagePercent: records.Average(x => x.GPUUsage),
            GpuBusyPercent: Math.Min(100, avgGpu / avgFrameTime * 100),
            PeakVramGb: Math.Max(r.VideoMem, records.Max(x => x.VideoMemoryUsage)));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        var pos = (sorted.Count - 1) * p;
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
