using System.Text;
using System.Text.RegularExpressions;

namespace WukongBenchmarkRunner.Settings;

// Формат: (("ScreenMode", "1"),("Vsync", "0"),...)
public sealed partial class UiSettingData
{
    private readonly List<KeyValuePair<string, string>> _items = [];

    public static UiSettingData Parse(string? raw)
    {
        var data = new UiSettingData();
        if (string.IsNullOrWhiteSpace(raw))
            return data;

        foreach (Match m in PairRegex().Matches(raw))
            data._items.Add(new(m.Groups[1].Value, m.Groups[2].Value));
        return data;
    }

    public string? this[string key]
    {
        get => _items.FirstOrDefault(p => p.Key == key).Value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var i = _items.FindIndex(p => p.Key == key);
            if (i >= 0)
                _items[i] = new(key, value);
            else
                _items.Add(new(key, value));
        }
    }

    public void Set(string key, int value) => this[key] = value.ToString();

    public override string ToString()
    {
        var sb = new StringBuilder("(");
        for (var i = 0; i < _items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("(\"").Append(_items[i].Key).Append("\", \"").Append(_items[i].Value).Append("\")");
        }

        return sb.Append(')').ToString();
    }

    [GeneratedRegex("\\(\"([^\"]+)\",\\s*\"([^\"]*)\"\\)")]
    private static partial Regex PairRegex();
}
