using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WukongBenchmarkRunner.Automation;
using WukongBenchmarkRunner.Report;
using WukongBenchmarkRunner.Settings;
using WukongBenchmarkRunner.Steam;
using WukongBenchmarkRunner.SystemInfo;

namespace WukongBenchmarkRunner;

public static class Program
{
    private const int BenchmarkAppId = 3132990;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        // Иначе при масштабе Windows != 100 % координаты курсора не совпадут с окном игры.
        SetProcessDpiAwarenessContext(-4); // PER_MONITOR_AWARE_V2

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Options.Usage);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(Options.Usage);
            return 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Log("Прерывание: закрываю игру и восстанавливаю настройки…");
            cts.Cancel();
        };

        try
        {
            return await RunAsync(options, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
    }

    private static async Task<int> RunAsync(Options options, CancellationToken ct)
    {
        var app = options.GameDir is not null
            ? new SteamApp(BenchmarkAppId, "Black Myth: Wukong Benchmark Tool", options.GameDir, null)
            : SteamLocator.FindApp(BenchmarkAppId);
        if (app is null || !Directory.Exists(app.InstallDir))
        {
            Console.Error.WriteLine("Black Myth: Wukong Benchmark Tool не найден в библиотеках Steam. " +
                                    "Установите его (Steam, бесплатно) или укажите папку: --game-dir \"<путь>\".");
            return 1;
        }

        Log($"Бенчмарк: {app.InstallDir}");

        var iniPath = SettingsWriter.ConfigPath(app.InstallDir);
        if (SettingsWriter.CheckFirstRunCompleted(iniPath) is { } firstRunProblem)
        {
            Console.Error.WriteLine(firstRunProblem);
            Console.Error.WriteLine("Запустите бенчмарк один раз вручную, пройдите стартовые экраны до главного меню и закройте его. " +
                                    "Экран с политикой конфиденциальности — ваше решение, инструмент его не принимает за вас.");
            return 1;
        }

        Log("Собираю характеристики компьютера…");
        var specs = SystemInfoCollector.Collect(app.InstallDir);
        if (specs.Display is not { } display)
        {
            Console.Error.WriteLine("Не удалось определить разрешение основного монитора.");
            return 1;
        }

        var rtSupported = !options.NoRayTracing && specs.Gpus.Any(g => g.Name.Contains("RTX", StringComparison.OrdinalIgnoreCase));
        var profiles = new List<BenchmarkSettings>();
        if (options.RunCpu) profiles.Add(BenchmarkProfiles.Cpu());
        if (options.RunGpu) profiles.Add(BenchmarkProfiles.Gpu(rtSupported));

        var outDir = Path.Combine(options.OutputDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(outDir);

        Log("Не трогайте мышь и клавиатуру, пока идут тесты: инструмент управляет меню бенчмарка через них.");

        BenchmarkSession.CloseGame();
        var originalIni = await File.ReadAllBytesAsync(iniPath, ct);
        await File.WriteAllBytesAsync(Path.Combine(outDir, "GameUserSettings.original.ini"), originalIni, CancellationToken.None);

        var passes = new List<PassResult>();
        try
        {
            var session = new BenchmarkSession(BenchmarkAppId, Log);
            foreach (var profile in profiles)
            {
                Log($"=== {profile.Name} ===");
                var resolution = SettingsWriter.Apply(iniPath, profile, display.Width, display.Height);
                Log($"Настройки записаны: {resolution.OutputWidth}×{resolution.OutputHeight}, рендер {resolution.RenderWidth}×{resolution.RenderHeight}, " +
                    $"качество «{ReportBuilder.Name(profile.Quality)}», трассировка {(profile.FullRayTracing ? "вкл." : "выкл.")}");

                try
                {
                    var result = await session.RunAsync(ct);
                    File.Copy(result.SourcePath!, Path.Combine(outDir, $"{profile.Id}_raw.json"), overwrite: true);
                    passes.Add(new PassResult(profile, resolution, result, null));
                    Log($"{profile.Name}: средний FPS {result.FPSAvg:0}, 5-й перцентиль {result.FPS95:0}");
                }
                catch (Exception e) when (e is TimeoutException or InvalidOperationException or IOException or JsonException)
                {
                    Log($"{profile.Name} не выполнен: {e.Message}");
                    passes.Add(new PassResult(profile, resolution, null, e.Message));
                }
            }
        }
        finally
        {
            BenchmarkSession.CloseGame();
            if (!options.KeepSettings)
            {
                await File.WriteAllBytesAsync(iniPath, originalIni, CancellationToken.None);
                Log("Исходные настройки бенчмарка восстановлены.");
            }
        }

        var benchmarkVersion = passes.Select(p => p.Result?.GameVer).FirstOrDefault(v => v is not null);
        var report = ReportBuilder.Build(specs, benchmarkVersion, app.BuildId, passes);
        await File.WriteAllTextAsync(Path.Combine(outDir, "report.md"), report, new UTF8Encoding(false), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(outDir, "report.json"), ToJson(specs, passes), new UTF8Encoding(false), CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine(report);
        Log($"Отчёт сохранён: {Path.GetFullPath(outDir)}");
        return passes.All(p => p.Result is not null) ? 0 : 1;
    }

    private static string ToJson(SystemSpecs specs, IReadOnlyList<PassResult> passes)
    {
        var data = new
        {
            System = specs,
            Passes = passes.Select(p => new
            {
                p.Settings.Name,
                Settings = new
                {
                    Quality = p.Settings.Quality.ToString(),
                    Upscaler = p.Settings.Upscaler.ToString(),
                    p.Settings.RenderScalePercent,
                    p.Settings.FrameGeneration,
                    p.Settings.FullRayTracing,
                    RayTracingLevel = p.Settings.RayTracingLevel.ToString(),
                    MotionBlur = p.Settings.MotionBlur.ToString(),
                    p.Resolution,
                },
                p.Error,
                Result = p.Result is null ? null : new
                {
                    p.Result.FPSAvg, p.Result.FPS95, p.Result.FPSMin, p.Result.FPSMax, p.Result.GPUAvg, p.Result.VideoMem,
                    p.Result.ScreenResolution, p.Result.QualityLevel, p.Result.ImageQuality, p.Result.Dlss, p.Result.Rtx,
                    p.Result.InsertFrame, p.Result.GameVer,
                },
                p.Metrics,
            }),
        };
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        });
    }

    private static void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
