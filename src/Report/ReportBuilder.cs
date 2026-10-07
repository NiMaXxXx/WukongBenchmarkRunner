using System.Globalization;
using System.Text;
using WukongBenchmarkRunner.Results;
using WukongBenchmarkRunner.Settings;
using WukongBenchmarkRunner.SystemInfo;

namespace WukongBenchmarkRunner.Report;

public sealed record PassResult(BenchmarkSettings Settings, AppliedResolution Resolution, BenchmarkResult? Result, string? Error)
{
    public ResultMetrics? Metrics { get; } = Result is null ? null : ResultMetrics.From(Result);
}

public static class ReportBuilder
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Build(SystemSpecs specs, string? benchmarkVersion, string? steamBuild, IReadOnlyList<PassResult> passes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Black Myth: Wukong Benchmark Tool — отчёт");
        sb.AppendLine();
        sb.AppendLine($"Дата: {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Ru)}  ");
        sb.AppendLine($"Версия бенчмарка: {benchmarkVersion ?? "—"}{(steamBuild is null ? "" : $" (Steam build {steamBuild})")}");
        sb.AppendLine();

        AppendSpecs(sb, specs);
        AppendResults(sb, passes);
        AppendSettings(sb, passes);
        AppendRationale(sb, passes);
        return sb.ToString();
    }

    private static void AppendSpecs(StringBuilder sb, SystemSpecs s)
    {
        sb.AppendLine("## Характеристики компьютера");
        sb.AppendLine();
        var rows = new List<(string, string?)>
        {
            ("Процессор", s.Cpu is null ? null : $"{s.Cpu.Name} — {s.Cpu.Cores} ядер / {s.Cpu.Threads} потоков, базовая частота {s.Cpu.MaxClockMhz} МГц"),
        };
        foreach (var gpu in s.Gpus)
        {
            var vram = gpu.VramGb is { } gb ? $"{gb:0.#} ГБ" : "?";
            rows.Add(("Видеокарта", $"{gpu.Name} — {vram} видеопамяти, драйвер {gpu.VendorDriverVersion ?? gpu.DriverVersion}"));
        }

        if (s.Ram is { } ram)
        {
            var speed = ram.SpeedMts is { } mts ? $", {mts} МТ/с" : "";
            var modules = ram.ModuleDetails.Distinct().Count() == 1 && ram.Modules > 0
                ? $"{ram.Modules} × {ram.ModuleDetails[0]}"
                : string.Join("; ", ram.ModuleDetails);
            rows.Add(("Оперативная память", $"{ram.TotalGb:0} ГБ{speed} ({modules})"));
        }

        rows.Add(("Материнская плата", s.Motherboard));
        rows.Add(("Операционная система", s.Os));
        rows.Add(("Монитор", s.Display is { } d ? $"{d.Width}×{d.Height}, {d.RefreshHz} Гц" : null));
        rows.Add(("Накопитель с бенчмарком", s.GameDisk));
        rows.Add(("Схема электропитания", s.PowerPlan));
        rows.Add(("Аппаратное планирование GPU", s.HardwareGpuScheduling));

        AppendTable(sb, ["Параметр", "Значение"], rows.Where(r => r.Item2 is not null).Select(r => new[] { r.Item1, r.Item2! }));
    }

    private static void AppendResults(StringBuilder sb, IReadOnlyList<PassResult> passes)
    {
        sb.AppendLine("## Результаты");
        sb.AppendLine();

        foreach (var p in passes.Where(p => p.Error is not null))
            sb.AppendLine($"> **{p.Settings.Name} не выполнен:** {p.Error}").AppendLine();

        var ok = passes.Where(p => p.Result is not null).ToList();
        if (ok.Count == 0)
            return;

        string[] header = ["Показатель", .. ok.Select(p => p.Settings.Name)];
        var rows = new List<string[]>
        {
            Row("Средний FPS", ok, p => F(p.Result!.FPSAvg)),
            Row("5-й перцентиль FPS (FPS95)", ok, p => F(p.Result!.FPS95)),
            Row("1% low FPS (по телеметрии)", ok, p => F(p.Metrics!.OnePercentLowFps)),
            Row("Минимальный FPS", ok, p => F(p.Result!.FPSMin)),
            Row("Максимальный FPS", ok, p => F(p.Result!.FPSMax)),
            Row("Видеопамять (пик)", ok, p => $"{p.Metrics!.PeakVramGb.ToString("0.0", Ru)} ГБ"),
            Row("Средняя загрузка GPU", ok, p => $"{F(p.Result!.GPUAvg)} %"),
            Row("Среднее время кадра CPU / GPU", ok, p => $"{F1(p.Metrics!.AvgCpuFrameTimeMs)} / {F1(p.Metrics.AvgGpuFrameTimeMs)} мс"),
            Row("Занятость GPU в кадре", ok, p => $"{F(p.Metrics!.GpuBusyPercent)} %"),
            Row("Узкое место", ok, p => p.Metrics!.Bottleneck),
            Row("Длительность / записей телеметрии", ok, p => $"{F(p.Metrics!.DurationSec)} с / {p.Metrics.Samples}"),
        };
        AppendTable(sb, header, rows);

        sb.AppendLine("«Занятость GPU в кадре» — отношение среднего времени рендера на GPU к среднему времени кадра. " +
                      "Если она заметно ниже 100 %, видеокарта часть кадра простаивает в ожидании процессора.");
        sb.AppendLine();
    }

    private static void AppendSettings(StringBuilder sb, IReadOnlyList<PassResult> passes)
    {
        sb.AppendLine("## Настройки");
        sb.AppendLine();
        sb.AppendLine("✓ — бенчмарк сам записал это значение в файл результата, то есть настройка подтверждена игрой.");
        sb.AppendLine();

        string[] header = ["Настройка", .. passes.Select(p => p.Settings.Name)];
        var rows = new List<string[]>
        {
            Row("Режим экрана", passes, _ => "Окно без рамок"),
            Row("Разрешение экрана", passes, p => Confirmed($"{p.Resolution.OutputWidth}×{p.Resolution.OutputHeight}", p.Result?.ScreenResolution?.Replace(" × ", "×"))),
            Row("Избыточная выборка сглаживания", passes, p => Confirmed(Name(p.Settings.Upscaler), p.Result is null ? null : Name((Upscaler)p.Result.Dlss))),
            Row("Степень избыточной выборки", passes, p => Confirmed($"{p.Settings.RenderScalePercent} %", p.Result is null ? null : $"{p.Result.ImageQuality} %")),
            Row("Разрешение рендера", passes, p => $"{p.Resolution.RenderWidth}×{p.Resolution.RenderHeight}"),
            Row("Набор настроек графики (все пункты)", passes, p => Confirmed(Name(p.Settings.Quality), p.Result is null ? null : Name((Quality)p.Result.QualityLevel))),
            Row("Генерация кадров", passes, p => Confirmed(OnOff(p.Settings.FrameGeneration), p.Result is null ? null : OnOff(p.Result.InsertFrame != 0))),
            Row("Полная трассировка лучей", passes, p => Confirmed(OnOff(p.Settings.FullRayTracing), p.Result is null ? null : OnOff(p.Result.Rtx != 0))),
            Row("Уровень трассировки лучей", passes, p => p.Settings.FullRayTracing ? Name(p.Settings.RayTracingLevel) : "—"),
            Row("Размытие при движении", passes, p => Confirmed(Name(p.Settings.MotionBlur), p.Result is null ? null : Name((MotionBlur)p.Result.MotionBlur))),
            Row("Вертикальная синхронизация", passes, _ => "Выкл."),
            Row("Ограничение частоты кадров", passes, _ => "Выкл."),
        };
        AppendTable(sb, header, rows);
    }

    private static void AppendRationale(StringBuilder sb, IReadOnlyList<PassResult> passes)
    {
        sb.AppendLine("## Почему выбраны такие настройки");
        foreach (var p in passes)
        {
            sb.AppendLine();
            sb.AppendLine($"**{p.Settings.Name}**");
            sb.AppendLine();
            foreach (var line in p.Settings.Rationale)
                sb.AppendLine($"- {line}");
        }

        sb.AppendLine();
    }

    private static string Confirmed(string requested, string? reported) => reported switch
    {
        null => requested,
        _ when reported == requested => $"{requested} ✓",
        _ => $"{requested} (бенчмарк: {reported})",
    };

    private static string[] Row(string title, IEnumerable<PassResult> passes, Func<PassResult, string> value) =>
        [title, .. passes.Select(value)];

    private static void AppendTable(StringBuilder sb, string[] header, IEnumerable<string[]> rows)
    {
        sb.Append("| ").Append(string.Join(" | ", header)).AppendLine(" |");
        sb.Append('|').Append(string.Concat(header.Select(_ => " --- |"))).AppendLine();
        foreach (var r in rows)
            sb.Append("| ").Append(string.Join(" | ", r)).AppendLine(" |");
        sb.AppendLine();
    }

    private static string F(double v) => v.ToString("0", Ru);
    private static string F1(double v) => v.ToString("0.0", Ru);
    private static string OnOff(bool v) => v ? "Вкл." : "Выкл.";

    public static string Name(Quality q) => q switch
    {
        Quality.Low => "Низкое",
        Quality.Medium => "Среднее",
        Quality.High => "Высокое",
        Quality.VeryHigh => "Очень высокое",
        Quality.Cinematic => "Кинематографичное",
        _ => $"? ({(int)q})",
    };

    public static string Name(Upscaler u) => u switch
    {
        Upscaler.Fsr => "FSR",
        Upscaler.XeSS => "XeSS",
        Upscaler.Dlss => "DLSS",
        Upscaler.Tsr => "TSR",
        _ => $"? ({(int)u})",
    };

    public static string Name(RayTracingLevel l) => l switch
    {
        RayTracingLevel.Low => "Низкий",
        RayTracingLevel.Medium => "Средний",
        RayTracingLevel.Ultra => "Ультра",
        _ => $"? ({(int)l})",
    };

    public static string Name(MotionBlur m) => m switch
    {
        MotionBlur.Off => "Выкл.",
        MotionBlur.Low => "Малозаметное",
        MotionBlur.High => "Очень заметное",
        _ => $"? ({(int)m})",
    };
}
