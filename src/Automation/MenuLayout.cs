using System.Drawing;

namespace WukongBenchmarkRunner.Automation;

// Сняты на окне 1920×1080 и масштабируются по высоте окна, как интерфейс UE.
public sealed class MenuLayout
{
    private const double RefHeight = 1080.0;
    private const double RefCenterX = 960.0;

    private readonly Rectangle _client;
    private readonly double _scale;

    public MenuLayout(Rectangle client)
    {
        _client = client;
        _scale = client.Height / RefHeight;
    }

    // «Тест быстродействия» в главном меню
    public Point BenchmarkItem => Left(220, 484);
    public Rectangle BenchmarkItemHighlight => LeftRect(70, 466, 450, 502);

    // «Подтвердить» в диалоге запуска теста
    public Rectangle ConfirmButton => CenterRect(570, 616, 940, 654);

    private Point Left(int x, int y) => new(_client.X + Scale(x), _client.Y + Scale(y));

    private Rectangle LeftRect(int x0, int y0, int x1, int y1) =>
        Rectangle.FromLTRB(_client.X + Scale(x0), _client.Y + Scale(y0), _client.X + Scale(x1), _client.Y + Scale(y1));

    private Rectangle CenterRect(int x0, int y0, int x1, int y1)
    {
        var cx = _client.X + _client.Width / 2.0;
        int X(int x) => (int)Math.Round(cx + (x - RefCenterX) * _scale);
        return Rectangle.FromLTRB(X(x0), _client.Y + Scale(y0), X(x1), _client.Y + Scale(y1));
    }

    private int Scale(int v) => (int)Math.Round(v * _scale);
}
