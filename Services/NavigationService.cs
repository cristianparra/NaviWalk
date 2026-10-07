using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>
/// Navegación centralizada. Los ViewModels piden "abrir este álbum" sin conocer rutas de Shell,
/// así cambiar la estructura de pantallas se hace en un solo lugar.
/// </summary>
public sealed class NavigationService
{
    // Rutas registradas en AppShell (páginas fuera de las pestañas principales).
    public const string CollectionRoute = "collection";
    public const string ArtistRoute = "artist";
    public const string NowPlayingRoute = "nowplaying";

    // Rutas absolutas ("//") reemplazan la pila de navegación completa.
    public const string StartupRoute = "//startup";
    public const string WelcomeRoute = "//welcome";
    public const string LoginRoute = "//login";
    public const string HomeRoute = "//home";
    public const string SearchRoute = "//search";
    public const string LibraryRoute = "//library";

    /// <summary>Clave del parámetro con el objeto a mostrar en la página de destino.</summary>
    public const string ItemKey = "Item";

    private readonly SessionService _session;
    private readonly PlaybackService _playback;

    public NavigationService(SessionService session, PlaybackService playback)
    {
        _session = session;
        _playback = playback;
    }

    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);

    public Task OpenCollectionAsync(MediaCollection collection) => GoToWithItemAsync(CollectionRoute, collection);

    public Task OpenArtistAsync(Artist artist) => GoToWithItemAsync(ArtistRoute, artist);

    public Task OpenNowPlayingAsync() => Shell.Current.GoToAsync(NowPlayingRoute);

    /// <summary>Pregunta al usuario y, si confirma, cierra la sesión, detiene la música y vuelve a la bienvenida.</summary>
    public async Task ConfirmSignOutAsync()
    {
        var sourceName = _session.Current?.DisplayName ?? "la fuente actual";
        var confirmed = await Shell.Current.DisplayAlert(
            "Cambiar de fuente",
            $"Se cerrará la sesión con {sourceName} y se borrarán las credenciales guardadas.",
            "Continuar", "Cancelar");
        if (!confirmed) return;

        _playback.Stop();
        _session.SignOut();
        await Shell.Current.GoToAsync(WelcomeRoute);
    }

    private static Task GoToWithItemAsync(string route, object item) =>
        Shell.Current.GoToAsync(route, new ShellNavigationQueryParameters { [ItemKey] = item });
}
