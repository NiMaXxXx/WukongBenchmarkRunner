using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WukongBenchmarkRunner.Steam;

// реестр -> libraryfolders.vdf -> appmanifest_{appId}.acf -> steamapps/common/{installdir}
public static partial class SteamLocator
{
    public static string? FindSteamPath()
    {
        var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                   ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
                   ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;

        return path is not null && Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    public static IReadOnlyList<string> GetLibraryFolders(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in PathRegex().Matches(File.ReadAllText(vdf)))
            {
                var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(lib))
                    libraries.Add(Path.GetFullPath(lib));
            }
        }

        return libraries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static SteamApp? FindApp(int appId)
    {
        var steamPath = FindSteamPath();
        if (steamPath is null)
            return null;

        foreach (var library in GetLibraryFolders(steamPath))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest))
                continue;

            var text = File.ReadAllText(manifest);
            var installDir = ReadVdfValue(text, "installdir");
            if (installDir is null)
                continue;

            var fullPath = Path.Combine(library, "steamapps", "common", installDir);
            if (Directory.Exists(fullPath))
                return new SteamApp(appId, ReadVdfValue(text, "name") ?? installDir, fullPath, ReadVdfValue(text, "buildid"));
        }

        return null;
    }

    private static string? ReadVdfValue(string vdf, string key)
    {
        var m = Regex.Match(vdf, $"\"{Regex.Escape(key)}\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathRegex();
}

public sealed record SteamApp(int AppId, string Name, string InstallDir, string? BuildId);
