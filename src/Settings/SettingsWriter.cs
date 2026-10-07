using System.Globalization;

namespace WukongBenchmarkRunner.Settings;

// Игра читает файл при старте, поэтому писать его нужно, пока она закрыта.
public static class SettingsWriter
{
    private const string MainSection = "/Script/GSGameSettings.GSGameUserSettings";
    private const string ScalabilitySection = "ScalabilityGroups";

    private static readonly string[] QualityKeys =
    [
        "QualityLevel", "ViewDistance", "AntiAliasing", "PostProcessing", "ShadowQuality", "TextureQuality",
        "FxQuality", "MaterialQuality", "VegetationQuality", "GlobalIllumination", "ReflectionQuality",
    ];

    // sg.* = значение из меню - 1
    private static readonly string[] ScalabilityKeys =
    [
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.GlobalIlluminationQuality",
        "sg.ReflectionQuality", "sg.PostProcessQuality", "sg.TextureQuality", "sg.EffectsQuality",
        "sg.FoliageQuality", "sg.ShadingQuality",
    ];

    public static string ConfigPath(string gameDir) =>
        Path.Combine(gameDir, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");

    public static string? CheckFirstRunCompleted(string iniPath)
    {
        if (!File.Exists(iniPath))
            return "Не найден GameUserSettings.ini: бенчмарк ещё ни разу не запускался.";

        var ini = IniFile.Load(iniPath);
        if (!string.Equals(ini.Get(MainSection, "FirstSettingFinish"), "True", StringComparison.OrdinalIgnoreCase))
            return "Бенчмарк ещё не прошёл экраны первого запуска (язык, яркость, политика конфиденциальности).";

        return null;
    }

    public static AppliedResolution Apply(string iniPath, BenchmarkSettings s, int screenWidth, int screenHeight)
    {
        var ini = IniFile.Load(iniPath);
        var ui = UiSettingData.Parse(ini.Get(MainSection, "UISettingData"));
        var res = ComputeResolution(s, screenWidth, screenHeight);

        // В режиме без рамок окно всегда на весь монитор, меньшее разрешение задаётся масштабом.
        ui.Set("ScreenMode", 1);
        ui.Set("ScreenResolution", res.ListIndex);
        ui.Set("WindowFullImageQuality", (int)Math.Floor(res.WindowScale * 1_000_000));
        ui.Set("LockFrameRate", 0);
        ui.Set("Vsync", 0);
        ui.Set("MotionBlur", (int)s.MotionBlur);
        ui.Set("SuperResolutionSampling", (int)s.Upscaler);
        ui.Set("ImageQuality", res.RenderHeight);
        ui.Set("InsertFrame", s.FrameGeneration ? 1 : 0);
        ui.Set("Rtx", s.FullRayTracing ? 1 : 0);
        ui.Set("RtxLevel", (int)s.RayTracingLevel);
        foreach (var key in QualityKeys)
            ui.Set(key, (int)s.Quality);

        ini.Set(MainSection, "UISettingData", ui.ToString());

        ini.Set(MainSection, "FullscreenMode", "1");
        ini.Set(MainSection, "LastConfirmedFullscreenMode", "1");
        ini.Set(MainSection, "PreferredFullscreenMode", "1");
        ini.Set(MainSection, "ResolutionSizeX", screenWidth.ToString());
        ini.Set(MainSection, "ResolutionSizeY", screenHeight.ToString());
        ini.Set(MainSection, "LastUserConfirmedResolutionSizeX", screenWidth.ToString());
        ini.Set(MainSection, "LastUserConfirmedResolutionSizeY", screenHeight.ToString());
        ini.Set(MainSection, "DesiredScreenWidth", res.RenderWidth.ToString());
        ini.Set(MainSection, "DesiredScreenHeight", res.RenderHeight.ToString());
        ini.Set(MainSection, "LastUserConfirmedDesiredScreenWidth", res.RenderWidth.ToString());
        ini.Set(MainSection, "LastUserConfirmedDesiredScreenHeight", res.RenderHeight.ToString());
        ini.Set(MainSection, "bUseVSync", "False");
        ini.Set(MainSection, "bUseDynamicResolution", "False");
        ini.Set(MainSection, "FrameRateLimit", "0.000000");

        ini.Set(ScalabilitySection, "sg.ResolutionQuality", s.RenderScalePercent.ToString(CultureInfo.InvariantCulture));
        foreach (var key in ScalabilityKeys)
            ini.Set(ScalabilitySection, key, ((int)s.Quality - 1).ToString());

        var tmp = iniPath + ".tmp";
        ini.Save(tmp);
        File.Move(tmp, iniPath, overwrite: true);
        return res;
    }

    public static AppliedResolution ComputeResolution(BenchmarkSettings s, int screenWidth, int screenHeight)
    {
        // Как список «Разрешение экрана» в меню: на 1920×1080 это 1920×1080, 1600×900, 1280×720.
        int[] standardHeights = [2160, 1800, 1440, 1080, 900, 720];
        var list = standardHeights.Where(h => h < screenHeight).Prepend(screenHeight).ToList();

        var outputHeight = s.Resolution == OutputResolution.Lowest720p && screenHeight > 720 ? 720 : screenHeight;
        var scale = (double)outputHeight / screenHeight;
        var outputWidth = (int)Math.Round(screenWidth * scale);

        // Вверх: игра отбрасывает дробную часть процента, и 237/720 превратилось бы в 32 %.
        var renderScale = Math.Clamp(s.RenderScalePercent, BenchmarkSettings.MinRenderScale, BenchmarkSettings.MaxRenderScale) / 100.0;
        return new AppliedResolution(
            outputWidth, outputHeight,
            (int)Math.Ceiling(outputWidth * renderScale), (int)Math.Ceiling(outputHeight * renderScale),
            Math.Max(0, list.IndexOf(outputHeight)), scale);
    }
}

public sealed record AppliedResolution(int OutputWidth, int OutputHeight, int RenderWidth, int RenderHeight, int ListIndex, double WindowScale);
