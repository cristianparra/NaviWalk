using Microsoft.Extensions.Logging;
using NaviWalk.Helpers;
using NaviWalk.Pages;
using NaviWalk.Services;
using NaviWalk.ViewModels;

namespace NaviWalk;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        RegisterServices(builder.Services);
        RegisterViewModelsAndPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceHelper.Services = app.Services;   // Para controles creados desde XAML (ver ServiceHelper).
        return app;
    }

    /// <summary>Servicios de la app. Los singleton conservan estado durante toda la ejecución.</summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // Un solo HttpClient reutilizado (evita agotar sockets); 30 s de límite por petición.
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        services.AddSingleton<SessionStore>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<PlaybackService>();
        services.AddSingleton<NavigationService>();

        // Implementaciones específicas de Android detrás de interfaces.
#if ANDROID
        services.AddSingleton<IAudioPlayer, AndroidImpl.AndroidAudioPlayer>();
        services.AddSingleton<ILocalMediaScanner, AndroidImpl.AndroidMediaScanner>();
        services.AddSingleton<IDominantColorService, AndroidImpl.AndroidDominantColorService>();
#endif
    }

    /// <summary>
    /// ViewModels y páginas. Las páginas son transient (una instancia nueva por navegación);
    /// PlayerViewModel es singleton porque lo comparten el mini reproductor y la pantalla completa.
    /// </summary>
    private static void RegisterViewModelsAndPages(IServiceCollection services)
    {
        services.AddSingleton<PlayerViewModel>();

        services.AddTransient<StartupViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<ServerLoginViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<CollectionViewModel>();
        services.AddTransient<ArtistViewModel>();

        services.AddTransient<StartupPage>();
        services.AddTransient<WelcomePage>();
        services.AddTransient<ServerLoginPage>();
        services.AddTransient<HomePage>();
        services.AddTransient<SearchPage>();
        services.AddTransient<LibraryPage>();
        services.AddTransient<CollectionPage>();
        services.AddTransient<ArtistPage>();
        services.AddTransient<NowPlayingPage>();
    }
}
