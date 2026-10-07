using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NaviWalk.Models;
using NaviWalk.Services;

namespace NaviWalk.ViewModels;

/// <summary>
/// Pantalla de arranque: decide a dónde ir. Con sesión guardada entra directo a la biblioteca
/// (sin pedir login); si no, muestra la pantalla de bienvenida.
/// </summary>
public sealed class StartupViewModel(SessionService session, NavigationService navigation) : BaseViewModel
{
    public async Task InitializeAsync()
    {
        var restored = await session.TryRestoreAsync();
        await navigation.GoToAsync(restored ? NavigationService.HomeRoute : NavigationService.WelcomeRoute);
    }
}

/// <summary>Bienvenida: el usuario elige entre música local o servidor Navidrome.</summary>
public sealed partial class WelcomeViewModel(SessionService session, NavigationService navigation) : BaseViewModel
{
    [RelayCommand]
    private Task UseLocal()
    {
        session.UseLocal();
        return navigation.GoToAsync(NavigationService.HomeRoute);
    }

    [RelayCommand]
    private Task UseServer() => navigation.GoToAsync(NavigationService.LoginRoute);
}

/// <summary>Formulario de conexión a Navidrome: dirección, usuario y contraseña.</summary>
public sealed partial class ServerLoginViewModel(SessionService session, NavigationService navigation) : BaseViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _userName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _password = string.Empty;

    private bool CanConnect() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(ServerUrl) &&
        !string.IsNullOrWhiteSpace(UserName) &&
        !string.IsNullOrEmpty(Password);

    // Se vuelve a evaluar CanConnect cuando cambia IsBusy para deshabilitar el botón mientras conecta.
    protected override void OnBusyChanged() => ConnectCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task Connect() => RunAsync(async () =>
    {
        var credentials = new ServerCredentials(ServerUrl.Trim(), UserName.Trim(), Password);
        await session.ConnectNavidromeAsync(credentials);
        Password = string.Empty;   // No se conserva en memoria del formulario.
        await navigation.GoToAsync(NavigationService.HomeRoute);
    });

    [RelayCommand]
    private Task Back() => navigation.GoToAsync(NavigationService.WelcomeRoute);
}
