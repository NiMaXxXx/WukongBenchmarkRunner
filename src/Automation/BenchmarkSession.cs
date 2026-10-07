using System.Diagnostics;
using System.Text.Json;
using WukongBenchmarkRunner.Results;

namespace WukongBenchmarkRunner.Automation;

public sealed class BenchmarkSession(int steamAppId, Action<string> log)
{
    public const string GameProcessName = "b1-Win64-Shipping";
    public const string LauncherProcessName = "b1_benchmark";

    public static string ResultsDirectory { get; } = Path.Combine(Path.GetTempPath(), "b1", "BenchMarkHistory", "Tool");

    // Подсвеченная плашка ~200, фон меню ~15–40.
    private const double HighlightThreshold = 120;
    private const double DarkThreshold = 80;

    public TimeSpan LaunchTimeout { get; init; } = TimeSpan.FromMinutes(3);
    public TimeSpan MenuTimeout { get; init; } = TimeSpan.FromMinutes(3);
    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public async Task<BenchmarkResult> RunAsync(CancellationToken ct)
    {
        CloseGame();
        var startedAt = DateTime.Now;
        var knownResults = ListResultFiles().ToHashSet(StringComparer.OrdinalIgnoreCase);

        log($"Запуск через Steam (steam://rungameid/{steamAppId})…");
        Process.Start(new ProcessStartInfo($"steam://rungameid/{steamAppId}") { UseShellExecute = true });

        try
        {
            var process = await WaitForAsync(() => Process.GetProcessesByName(GameProcessName).FirstOrDefault(),
                LaunchTimeout, "процесс игры не появился", ct);
            log($"Процесс игры запущен (PID {process.Id}), жду окно…");

            var window = await WaitForAsync(() => GameWindow.Find(process.Id),
                LaunchTimeout, "окно игры не появилось", ct, process);

            await StartFromMainMenuAsync(process, window, ct);
            log("Тест идёт (около 2,5 минуты), жду файл результата…");

            var resultPath = await WaitForAsync(() => FindNewResult(knownResults, startedAt),
                RunTimeout, "бенчмарк не записал результат", ct, process);
            log($"Результат получен: {resultPath}");
            return BenchmarkResult.Load(resultPath);
        }
        finally
        {
            CloseGame();
        }
    }

    // Главное меню не реагирует на стрелки, только на мышь: наводим курсор на пункт и жмём E.
    private async Task StartFromMainMenuAsync(Process process, GameWindow window, CancellationToken ct)
    {
        log("Жду главное меню…");
        var deadline = DateTime.UtcNow + MenuTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            EnsureAlive(process);

            if (!window.Focus())
            {
                await Task.Delay(1000, ct);
                continue;
            }

            var layout = new MenuLayout(window.ClientBounds);
            Input.MoveMouse(layout.BenchmarkItem.X, layout.BenchmarkItem.Y);
            await Task.Delay(500, ct);

            var dialogOpen = ScreenSampler.AverageLuminance(layout.ConfirmButton) > HighlightThreshold;
            if (!dialogOpen)
            {
                if (ScreenSampler.AverageLuminance(layout.BenchmarkItemHighlight) < HighlightThreshold)
                {
                    // Меню ещё грузится; если висит заставка, E её закроет.
                    Input.Press(Input.Key.E);
                    await Task.Delay(4000, ct);
                    continue;
                }

                log("Главное меню готово, выбираю «Тест быстродействия»…");
                Input.Press(Input.Key.E);
                dialogOpen = await WaitUntilAsync(
                    () => ScreenSampler.AverageLuminance(layout.ConfirmButton) > HighlightThreshold, TimeSpan.FromSeconds(5), ct);
                if (!dialogOpen)
                    continue;
            }

            log("Подтверждаю запуск теста…");
            Input.Press(Input.Key.E);
            await Task.Delay(2500, ct);

            if (ScreenSampler.AverageLuminance(layout.ConfirmButton) < DarkThreshold &&
                ScreenSampler.AverageLuminance(layout.BenchmarkItemHighlight) < DarkThreshold)
                return;
        }

        throw new TimeoutException("Не удалось запустить тест из главного меню.");
    }

    private static string? FindNewResult(HashSet<string> known, DateTime startedAt)
    {
        foreach (var path in ListResultFiles())
        {
            if (known.Contains(path) || File.GetLastWriteTime(path) < startedAt.AddSeconds(-5))
                continue;

            // Файл может ещё дописываться.
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("FPSAvg", out _))
                    return path;
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> ListResultFiles() =>
        Directory.Exists(ResultsDirectory) ? Directory.EnumerateFiles(ResultsDirectory) : [];

    public static void CloseGame()
    {
        foreach (var name in new[] { GameProcessName, LauncherProcessName })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(15_000);
                    }
                    catch (InvalidOperationException)
                    {
                        // уже завершился
                    }
                }
            }
        }
    }

    private static void EnsureAlive(Process process)
    {
        if (process.HasExited)
            throw new InvalidOperationException($"Игра неожиданно закрылась (код {process.ExitCode}).");
    }

    private static async Task<T> WaitForAsync<T>(Func<T?> probe, TimeSpan timeout, string error, CancellationToken ct,
        Process? process = null) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (process is not null)
                EnsureAlive(process);
            if (probe() is { } value)
                return value;
            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Таймаут {timeout.TotalMinutes:0} мин: {error}.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(250, ct);
        }

        return false;
    }
}
