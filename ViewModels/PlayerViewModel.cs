using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NaviWalk.Helpers;
using NaviWalk.Services;

namespace NaviWalk.ViewModels;

/// <summary>
/// ViewModel compartido por el mini reproductor y la pantalla "Reproduciendo".
/// Envuelve a <see cref="PlaybackService"/> agregando comandos y datos derivados (ícono según estado, progreso).
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject
{
    private const int MaxQueueItems = 50;

    private readonly NavigationService _navigation;
    private readonly SessionService _session;

    public PlayerViewModel(PlaybackService playback, NavigationService navigation, SessionService session)
    {
        Playback = playback;
        _navigation = navigation;
        _session = session;

        // Cuando cambia el estado del servicio, se refrescan las propiedades derivadas de este ViewModel.
        playback.PropertyChanged += OnPlaybackChanged;
    }

    /// <summary>Estado de reproducción (pista actual, posición, etc.) para enlazar directamente en XAML.</summary>
    public PlaybackService Playback { get; }

    public string PlayPauseIcon => Playback.IsPlaying ? Icons.Pause : Icons.Play;
    public string RepeatIcon => Playback.RepeatMode == RepeatMode.One ? Icons.RepeatOne : Icons.Repeat;
    public bool IsRepeatActive => Playback.RepeatMode != RepeatMode.Off;

    /// <summary>Favoritos y estrellas solo se muestran si la fuente activa los soporta (Navidrome).</summary>
    public bool SupportsRatings => _session.Current?.SupportsRatings == true;

    public string SpeedText => Playback.Speed == 1f ? string.Empty : $"{Playback.Speed:0.##}x";
    public bool IsSpeedActive => Playback.Speed != 1f;

    /// <summary>Avance de 0 a 1, para la barrita fina del mini reproductor.</summary>
    public double Progress => Playback.DurationSeconds <= 0 ? 0 : Math.Clamp(Playback.PositionSeconds / Playback.DurationSeconds, 0, 1);

    [RelayCommand] private void TogglePlayPause() => Playback.TogglePlayPause();
    [RelayCommand] private void Next() => Playback.Next();
    [RelayCommand] private void Previous() => Playback.Previous();
    [RelayCommand] private void ToggleShuffle() => Playback.ToggleShuffle();
    [RelayCommand] private void CycleRepeat() => Playback.CycleRepeat();
    [RelayCommand] private void CycleSpeed() => Playback.CycleSpeed();
    [RelayCommand] private void CycleSleepTimer() => Playback.CycleSleepTimer();
    [RelayCommand] private Task OpenNowPlaying() => _navigation.OpenNowPlayingAsync();
    [RelayCommand] private Task Close() => _navigation.GoToAsync("..");

    /// <summary>Marca/desmarca favorita. Se actualiza la UI al instante y se revierte si el servidor falla.</summary>
    [RelayCommand]
    private async Task ToggleFavorite()
    {
        var track = Playback.CurrentTrack;
        var source = _session.Current;
        if (track is null || source is null) return;

        var newValue = !track.IsStarred;
        track.IsStarred = newValue;
        try
        {
            await source.SetFavoriteAsync(track, newValue);
        }
        catch (Exception ex)
        {
            track.IsStarred = !newValue;
            await Shell.Current.DisplayAlert("No se pudo actualizar el favorito", ex.Message, "OK");
        }
    }

    /// <summary>Califica la canción actual (0 a 5). Mismo patrón optimista que los favoritos.</summary>
    [RelayCommand]
    private async Task Rate(int rating)
    {
        var track = Playback.CurrentTrack;
        var source = _session.Current;
        if (track is null || source is null) return;

        var previous = track.Rating;
        track.Rating = rating;
        try
        {
            await source.SetRatingAsync(track, rating);
        }
        catch (Exception ex)
        {
            track.Rating = previous;
            await Shell.Current.DisplayAlert("No se pudo guardar la calificación", ex.Message, "OK");
        }
    }

    /// <summary>Muestra las canciones que vienen a continuación; tocar una salta directamente a ella.</summary>
    [RelayCommand]
    private async Task ShowQueue()
    {
        var upcoming = Playback.GetUpcoming().Take(MaxQueueItems).ToList();
        if (upcoming.Count == 0)
        {
            await Shell.Current.DisplayAlert("Cola", "No hay más canciones en la cola.", "OK");
            return;
        }

        // Se numeran para distinguir canciones repetidas y poder mapear la elección a su posición.
        var items = upcoming.Select((t, i) => $"{i + 1}. {t.Title} — {t.Artist}").ToArray();
        var choice = await Shell.Current.DisplayActionSheet("A continuación", "Cerrar", null, items);

        var index = Array.IndexOf(items, choice);
        if (index >= 0) Playback.PlayUpcoming(index);
    }

    private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackService.IsPlaying):
                OnPropertyChanged(nameof(PlayPauseIcon));
                break;
            case nameof(PlaybackService.RepeatMode):
                OnPropertyChanged(nameof(RepeatIcon));
                OnPropertyChanged(nameof(IsRepeatActive));
                break;
            case nameof(PlaybackService.Speed):
                OnPropertyChanged(nameof(SpeedText));
                OnPropertyChanged(nameof(IsSpeedActive));
                break;
            case nameof(PlaybackService.CurrentTrack):
                OnPropertyChanged(nameof(SupportsRatings));
                break;
            case nameof(PlaybackService.PositionSeconds):
            case nameof(PlaybackService.DurationSeconds):
                OnPropertyChanged(nameof(Progress));
                break;
        }
    }
}
