using Microsoft.Maui.Controls.Shapes;
using NaviWalk.Helpers;
using Path = Microsoft.Maui.Controls.Shapes.Path; // Evita ambigüedad con System.IO.Path.
using NaviWalk.Services;
using NaviWalk.ViewModels;

namespace NaviWalk.Views;

/// <summary>
/// Mini reproductor + barra de navegación inferior. Cada pantalla principal lo incluye al fondo,
/// indicando su pestaña con <see cref="ActiveTab"/> para resaltarla en naranja.
/// </summary>
public partial class BottomChrome : ContentView
{
    /// <summary>Pestañas de la barra inferior.</summary>
    public enum Tab { None, Home, Search, Library }

    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab), typeof(Tab), typeof(BottomChrome), Tab.None,
        propertyChanged: (b, _, __) => ((BottomChrome)b).UpdateTabColors());

    public BottomChrome()
    {
        InitializeComponent();

        // Control creado desde XAML: no admite inyección por constructor, se usa el localizador.
        BindingContext = ServiceHelper.Get<PlayerViewModel>();

        HomeIcon.Data = ToGeometry(Icons.Home);
        SearchIcon.Data = ToGeometry(Icons.Search);
        LibraryIcon.Data = ToGeometry(Icons.Library);
        UpdateTabColors();
    }

    public Tab ActiveTab
    {
        get => (Tab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    /// <summary>Resalta la pestaña activa; si es <see cref="Tab.None"/> (pantallas de detalle) oculta la barra.</summary>
    private void UpdateTabColors()
    {
        NavBar.IsVisible = ActiveTab != Tab.None;
        Paint(HomeIcon, HomeLabel, ActiveTab == Tab.Home);
        Paint(SearchIcon, SearchLabel, ActiveTab == Tab.Search);
        Paint(LibraryIcon, LibraryLabel, ActiveTab == Tab.Library);
    }

    private static void Paint(Path icon, Label label, bool active)
    {
        var color = (Color)Application.Current!.Resources[active ? "Accent" : "TextSecondary"];
        icon.Fill = new SolidColorBrush(color);
        label.TextColor = color;
    }

    private static Geometry ToGeometry(string data) => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;

    private static Task GoTo(string route) => ServiceHelper.Get<NavigationService>().GoToAsync(route);

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await GoTo(NavigationService.HomeRoute);
    private async void OnSearchTapped(object? sender, TappedEventArgs e) => await GoTo(NavigationService.SearchRoute);
    private async void OnLibraryTapped(object? sender, TappedEventArgs e) => await GoTo(NavigationService.LibraryRoute);
}
