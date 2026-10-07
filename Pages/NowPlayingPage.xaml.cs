using System.ComponentModel;
using NaviWalk.Helpers;
using NaviWalk.Services;
using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

/// <summary>
/// Reproductor a pantalla completa. Hace dos cosas que no se resuelven bien con binding:
/// 1) El Slider no usa binding bidireccional porque la posición cambia 2 veces por segundo y pelearía con
///    el dedo del usuario al arrastrar; se sincroniza a mano y solo se hace seek al soltar.
/// 2) El fondo es un degradado con el color dominante de la carátula, calculado al cambiar de canción.
/// </summary>
public partial class NowPlayingPage : ContentPage
{
    // Tinte cuando la canción no tiene carátula o no se pudo leer: un marrón cálido que combina con el acento.
    private static readonly Color DefaultTint = Color.FromArgb("#4A2008");

    private readonly PlayerViewModel _viewModel;
    private readonly IDominantColorService _colors;
    private bool _isDragging;
    private string? _tintedTrackId;

    public NowPlayingPage(PlayerViewModel viewModel, IDominantColorService colors)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _colors = colors;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Playback.PropertyChanged += OnPlaybackChanged;
        SyncSlider();
        _ = UpdateBackgroundAsync();
    }

    protected override void OnDisappearing()
    {
        // Se desuscribe para que la página no quede retenida por el servicio singleton.
        _viewModel.Playback.PropertyChanged -= OnPlaybackChanged;
        base.OnDisappearing();
    }

    private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackService.PositionSeconds):
            case nameof(PlaybackService.DurationSeconds):
                SyncSlider();
                break;
            case nameof(PlaybackService.CurrentTrack):
                _ = UpdateBackgroundAsync();
                break;
        }
    }

    private void SyncSlider()
    {
        if (_isDragging) return;
        var playback = _viewModel.Playback;
        // El máximo debe fijarse antes que el valor, o el Slider recortaría el valor al máximo anterior.
        ProgressSlider.Maximum = Math.Max(1, playback.DurationSeconds);
        ProgressSlider.Value = Math.Min(playback.PositionSeconds, ProgressSlider.Maximum);
    }

    private void OnDragStarted(object? sender, EventArgs e) => _isDragging = true;

    private void OnDragCompleted(object? sender, EventArgs e)
    {
        _isDragging = false;
        _viewModel.Playback.SeekTo(ProgressSlider.Value);
    }

    // ----- Fondo con el color de la carátula -----

    private async Task UpdateBackgroundAsync()
    {
        var track = _viewModel.Playback.CurrentTrack;
        if (track?.Id == _tintedTrackId && Root.Background is not null) return;
        _tintedTrackId = track?.Id;

        var tint = DefaultTint;
        if (track?.OpenCover is { } openCover)
        {
            try
            {
                // Se copia a memoria primero: decodificar directo desde la red bloquearía la interfaz.
                await using var source = await openCover();
                if (source is not null)
                {
                    using var buffer = new MemoryStream();
                    await source.CopyToAsync(buffer);
                    buffer.Position = 0;
                    if (_colors.GetDominantColor(buffer) is { } dominant) tint = Darken(dominant);
                }
            }
            catch (Exception)
            {
                // Sin red o imagen inválida: se queda con el tinte por defecto.
            }
        }

        if (track?.Id != _tintedTrackId) return;   // La canción cambió mientras se calculaba.
        ApplyBackground(tint);
    }

    /// <summary>Oscurece el color para que el texto blanco siempre se lea bien, conservando su tono.</summary>
    private static Color Darken(Color color) =>
        color.WithLuminosity(Math.Clamp(color.GetLuminosity() * 0.6f, 0.10f, 0.28f));

    /// <summary>Degradado vertical: color de la carátula arriba, casi negro abajo.</summary>
    private void ApplyBackground(Color tint)
    {
        Root.Background = new LinearGradientBrush(
            [new GradientStop(tint, 0f), new GradientStop(tint.WithLuminosity(0.05f), 1f)],
            new Point(0.5, 0), new Point(0.5, 1));
    }
}
