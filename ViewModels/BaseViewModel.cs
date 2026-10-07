using CommunityToolkit.Mvvm.ComponentModel;
using NaviWalk.Services;

namespace NaviWalk.ViewModels;

/// <summary>
/// Base de los ViewModels: estado de carga/error y un envoltorio que unifica el manejo de excepciones
/// para que cada pantalla muestre un mensaje en vez de cerrarse con un error.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;

    // Gancho generado por el toolkit; lo reenviamos a un método virtual para que las subclases reaccionen.
    partial void OnIsBusyChanged(bool value) => OnBusyChanged();

    /// <summary>Se invoca cuando cambia <see cref="IsBusy"/>. Las subclases pueden refrescar comandos.</summary>
    protected virtual void OnBusyChanged() { }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Ejecuta <paramref name="action"/> marcando <see cref="IsBusy"/> y capturando errores
    /// en <see cref="ErrorMessage"/>.
    /// </summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Cancelación voluntaria (ej. nueva búsqueda): no es un error.
        }
        catch (UnauthorizedAccessException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (SubsonicException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ocurrió un error inesperado: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
