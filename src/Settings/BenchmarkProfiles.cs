namespace WukongBenchmarkRunner.Settings;

public static class BenchmarkProfiles
{
    public static BenchmarkSettings Cpu() => new()
    {
        Id = "cpu",
        Name = "CPU-тест",
        Quality = Quality.Low,
        Resolution = OutputResolution.Lowest720p,
        Upscaler = Upscaler.Tsr,
        RenderScalePercent = BenchmarkSettings.MinRenderScale,
        FrameGeneration = false,
        FullRayTracing = false,
        MotionBlur = MotionBlur.Off,
        Rationale =
        [
            "Разрешение 1280×720 и масштаб рендера 33 % (TSR): кадр рендерится примерно в 423×238, " +
            "поэтому работа GPU, которая растёт с числом пикселей (шейдинг, освещение, постобработка), почти исчезает.",
            "TSR выбран потому, что работает на любой видеокарте и позволяет задать масштаб рендера напрямую, " +
            "в отличие от DLSS/FSR/XeSS с фиксированными режимами.",
            "Все пресеты качества на «Низком»: меньше работы для GPU, а логику, анимацию, физику и подготовку " +
            "кадров процессор всё равно выполняет.",
            "Генерация кадров выключена: дорисованные GPU кадры завышают FPS и маскируют предел процессора.",
            "Трассировка лучей выключена: это самая тяжёлая для GPU функция.",
            "Ограничитель FPS и вертикальная синхронизация выключены, чтобы FPS не упирался в искусственный лимит.",
        ],
    };

    public static BenchmarkSettings Gpu(bool fullRayTracingSupported) => new()
    {
        Id = "gpu",
        Name = "GPU-тест",
        Quality = Quality.Cinematic,
        Resolution = OutputResolution.Native,
        Upscaler = Upscaler.Tsr,
        RenderScalePercent = BenchmarkSettings.MaxRenderScale,
        FrameGeneration = false,
        FullRayTracing = fullRayTracingSupported,
        RayTracingLevel = RayTracingLevel.Ultra,
        MotionBlur = MotionBlur.High,
        Rationale =
        [
            "Родное разрешение монитора и масштаб рендера 100 %: кадр рендерится в полном разрешении, без апскейла.",
            "Все пресеты качества на «Кинематографичном», максимальном уровне.",
            fullRayTracingSupported
                ? "Полная трассировка лучей (path tracing) включена на уровне «Ультра»: самая тяжёлая для GPU функция игры."
                : "Полная трассировка лучей недоступна на этой видеокарте (нужна NVIDIA RTX), поэтому выключена.",
            "Генерация кадров выключена: при ней половина кадров дорисовывается, и FPS перестаёт отражать " +
            "реальную производительность рендера.",
            "Размытие в движении на максимуме: это дополнительный проход постобработки на GPU.",
            "Ограничитель FPS и вертикальная синхронизация выключены, чтобы видеокарта не простаивала в ожидании лимита.",
        ],
    };
}
