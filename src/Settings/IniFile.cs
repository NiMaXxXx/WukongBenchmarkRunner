using System.Text;

namespace WukongBenchmarkRunner.Settings;

// Меняет только нужные ключи, остальные строки файла сохраняются как были.
public sealed class IniFile
{
    private readonly List<Section> _sections = [];

    public static IniFile Load(string path) => Parse(File.ReadAllLines(path));

    public static IniFile Parse(IEnumerable<string> lines)
    {
        var ini = new IniFile();
        var current = new Section(null); // строки до первой секции
        ini._sections.Add(current);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new Section(line[1..^1]);
                ini._sections.Add(current);
            }
            else
            {
                current.Lines.Add(raw);
            }
        }

        return ini;
    }

    public string? Get(string section, string key)
    {
        var s = FindSection(section);
        if (s is null) return null;
        foreach (var line in s.Lines)
            if (TrySplit(line, out var k, out var v) && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                return v;
        return null;
    }

    public void Set(string section, string key, string value)
    {
        var s = FindSection(section);
        if (s is null)
        {
            s = new Section(section);
            var last = _sections[^1];
            if (last.Lines.Count > 0 && last.Lines[^1].Trim().Length > 0)
                last.Lines.Add("");
            _sections.Add(s);
        }

        for (var i = 0; i < s.Lines.Count; i++)
        {
            if (TrySplit(s.Lines[i], out var k, out _) && k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                s.Lines[i] = $"{k}={value}";
                return;
            }
        }

        var insertAt = s.Lines.FindLastIndex(l => l.Trim().Length > 0) + 1;
        s.Lines.Insert(insertAt, $"{key}={value}");
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        foreach (var s in _sections)
        {
            if (s.Name is not null)
                sb.Append('[').Append(s.Name).Append(']').Append("\r\n");
            foreach (var line in s.Lines)
                sb.Append(line).Append("\r\n");
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private Section? FindSection(string name) =>
        _sections.FirstOrDefault(s => s.Name is not null && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool TrySplit(string line, out string key, out string value)
    {
        key = value = "";
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is ';' or '#')
            return false;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0)
            return false;
        key = trimmed[..eq].Trim();
        value = trimmed[(eq + 1)..].Trim();
        return true;
    }

    private sealed class Section(string? name)
    {
        public string? Name { get; } = name;
        public List<string> Lines { get; } = [];
    }
}
