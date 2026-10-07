namespace NaviWalk.Helpers;

/// <summary>
/// Acceso estático al contenedor de dependencias.
/// Solo se usa en controles reutilizables creados desde XAML (como el mini reproductor),
/// que no admiten inyección por constructor. En el resto del código, inyecta por constructor.
/// </summary>
public static class ServiceHelper
{
    public static IServiceProvider Services { get; set; } = default!;

    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();
}
