namespace WukongBenchmarkRunner;

public sealed class Options
{
    public string? GameDir { get; private set; }
    public string OutputDir { get; private set; } = "results";
    public bool RunCpu { get; private set; } = true;
    public bool RunGpu { get; private set; } = true;
    public bool NoRayTracing { get; private set; }
    public bool KeepSettings { get; private set; }
    public bool ShowHelp { get; private set; }

    public const string Usage = """
        Автоматический запуск Black Myth: Wukong Benchmark Tool: CPU-тест и GPU-тест с отчётом.

        Использование: WukongBenchmarkRunner [параметры]

          --only cpu|gpu      выполнить только один из тестов
          --game-dir <путь>   папка бенчмарка, если автоопределение через Steam не сработало
          --output <папка>    куда сохранить отчёт (по умолчанию ./results)
          --no-rt             не включать полную трассировку лучей в GPU-тесте
          --keep-settings     не возвращать исходные настройки бенчмарка после тестов
          -h, --help          эта справка
        """;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--only":
                    var which = Next(args, ref i).ToLowerInvariant();
                    if (which is not ("cpu" or "gpu"))
                        throw new ArgumentException("--only принимает cpu или gpu.");
                    o.RunCpu = which == "cpu";
                    o.RunGpu = which == "gpu";
                    break;
                case "--game-dir":
                    o.GameDir = Next(args, ref i);
                    break;
                case "--output":
                    o.OutputDir = Next(args, ref i);
                    break;
                case "--no-rt":
                    o.NoRayTracing = true;
                    break;
                case "--keep-settings":
                    o.KeepSettings = true;
                    break;
                case "-h" or "--help" or "/?":
                    o.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Неизвестный параметр: {args[i]}");
            }
        }

        return o;
    }

    private static string Next(string[] args, ref int i) =>
        i + 1 < args.Length ? args[++i] : throw new ArgumentException($"После {args[i]} нужно значение.");
}
