using NaviWalk.Pages;
using NaviWalk.Services;

namespace NaviWalk;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Páginas que se apilan encima de las pestañas principales.
        Routing.RegisterRoute(NavigationService.CollectionRoute, typeof(CollectionPage));
        Routing.RegisterRoute(NavigationService.ArtistRoute, typeof(ArtistPage));
        Routing.RegisterRoute(NavigationService.NowPlayingRoute, typeof(NowPlayingPage));
    }
}
