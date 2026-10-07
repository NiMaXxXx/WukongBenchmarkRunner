namespace WukongBenchmarkRunner.Settings;

// Значения совпадают с тем, что меню бенчмарка пишет в UISettingData.

public enum Quality
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4,
    Cinematic = 5,
}

// SuperResolutionSampling
public enum Upscaler
{
    Fsr = 0,
    XeSS = 1,
    Dlss = 2,
    Tsr = 3,
}

// RtxLevel
public enum RayTracingLevel
{
    Low = 1,
    Medium = 2,
    Ultra = 3,
}

public enum MotionBlur
{
    Off = 0,
    Low = 1,
    High = 2,
}

public enum OutputResolution
{
    Native,
    Lowest720p,
}

public sealed record BenchmarkSettings
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Quality Quality { get; init; }
    public required OutputResolution Resolution { get; init; }
    public required Upscaler Upscaler { get; init; }

    // «Степень избыточной выборки сглаживания», 33–100 %
    public required int RenderScalePercent { get; init; }

    public required bool FrameGeneration { get; init; }
    public required bool FullRayTracing { get; init; }
    public RayTracingLevel RayTracingLevel { get; init; } = RayTracingLevel.Low;
    public required MotionBlur MotionBlur { get; init; }
    public required IReadOnlyList<string> Rationale { get; init; }

    public const int MinRenderScale = 33;
    public const int MaxRenderScale = 100;
}
