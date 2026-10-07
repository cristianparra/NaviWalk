using CommunityToolkit.Mvvm.ComponentModel;
using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>
/// Motor de audio de la plataforma (Android: MediaPlayer). Se abstrae para que
/// <see cref="PlaybackService"/> contenga solo la lógica de cola y no dependa de Android.
/// </summary>
public interface IAudioPlayer
{
    /// <summary>La pista actual terminó de forma natural.</summary>
    event EventHandler? Completed;

    /// <summary>Ocurrió un error al cargar o reproducir; el argumento es un mensaje legible.</summary>
    event EventHandler<string>? Failed;

    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    /// <summary>Carga y comienza a reproducir la URI (http(s):// o content://).</summary>
    void Play(string uri, string title, string artist);
    void Pause();
    void Resume();
    void Stop();
    void SeekTo(TimeSpan position);

    /// <summary>Velocidad de reproducción (1.0 = normal). Se conserva al cambiar de pista.</summary>
    void SetSpeed(float speed);
}

public enum RepeatMode { Off, All, One }

/// <summary>
/// Estado y control de la reproducción: cola, orden aleatorio, repetición y posición.
/// Es singleton y observable; el mini reproductor y la pantalla "Reproduciendo" se enlazan a él.
/// </summary>
public sealed partial class PlaybackService : ObservableObject
{
    private readonly IAudioPlayer _player;
    private IDispatcherTimer? _timer;

    private List<Track> _queue = new();      // Cola en el orden original.
    private List<int> _order = new();        // Índices de _queue en el orden de reproducción (cambia con shuffle).
    private int _orderPosition = -1;         // Posición actual dentro de _order.

    private static readonly float[] SpeedSteps = [1f, 1.25f, 1.5f, 2f, 0.75f];
    private static readonly int[] SleepMinutesSteps = [0, 15, 30, 45, 60];
    private int _sleepStepIndex;             // Índice en SleepMinutesSteps (0 = desactivado).
    private DateTime? _sleepEndUtc;          // Momento en que se pausa la música.

    public PlaybackService(IAudioPlayer player)
    {
        _player = player;
        _player.Completed += (_, _) => MainThread.BeginInvokeOnMainThread(OnTrackCompleted);
        _player.Failed += (_, message) => MainThread.BeginInvokeOnMainThread(() => OnPlayerFailed(message));
    }

    // ----- Estado observable (la UI se enlaza a estas propiedades) -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrack))]
    private Track? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isShuffleOn;

    [ObservableProperty]
    private RepeatMode _repeatMode = RepeatMode.Off;

    /// <summary>Posición actual en segundos (para el Slider).</summary>
    [ObservableProperty]
    private double _positionSeconds;

    /// <summary>Duración en segundos (máximo del Slider).</summary>
    [ObservableProperty]
    private double _durationSeconds = 1;

    [ObservableProperty]
    private string _positionText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    /// <summary>Último error de reproducción (null si no hay).</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Velocidad de reproducción actual (1.0 = normal).</summary>
    [ObservableProperty]
    private float _speed = 1f;

    /// <summary>Texto del temporizador de apagado, ej. "30 min"; vacío si está desactivado.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSleepTimerActive))]
    private string _sleepTimerText = string.Empty;

    public bool IsSleepTimerActive => SleepTimerText.Length > 0;

    public bool HasTrack => CurrentTrack is not null;

    /// <summary>Hay una pista siguiente (o la cola se repite).</summary>
    private bool CanGoNext => _orderPosition + 1 < _order.Count || RepeatMode == RepeatMode.All;

    // ----- Comandos de reproducción -----

    /// <summary>Reemplaza la cola y empieza a reproducir desde <paramref name="startIndex"/>.</summary>
    public void PlayQueue(IReadOnlyList<Track> tracks, int startIndex = 0)
    {
        if (tracks.Count == 0) return;
        _queue = tracks.ToList();
        RebuildOrder(startIndex);
        StartCurrent();
    }

    /// <summary>Reproduce la colección completa en orden aleatorio.</summary>
    public void PlayShuffled(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0) return;
        IsShuffleOn = true;
        PlayQueue(tracks, Random.Shared.Next(tracks.Count));
    }

    public void TogglePlayPause()
    {
        if (CurrentTrack is null) return;
        if (_player.IsPlaying)
        {
            _player.Pause();
            IsPlaying = false;
        }
        else
        {
            _player.Resume();
            IsPlaying = true;
        }
    }

    public void Next()
    {
        if (_order.Count == 0) return;
        if (_orderPosition + 1 < _order.Count) _orderPosition++;
        else if (RepeatMode == RepeatMode.All) _orderPosition = 0;
        else return;
        StartCurrent();
    }

    /// <summary>
    /// Convención habitual: si ya van más de 3 s se reinicia la pista; si no, se vuelve a la anterior.
    /// </summary>
    public void Previous()
    {
        if (_order.Count == 0) return;
        if (_player.Position > TimeSpan.FromSeconds(3) || _orderPosition == 0)
        {
            _player.SeekTo(TimeSpan.Zero);
            return;
        }
        _orderPosition--;
        StartCurrent();
    }

    /// <summary>Canciones que vienen después de la actual, en el orden en que sonarán.</summary>
    public IReadOnlyList<Track> GetUpcoming() =>
        _orderPosition < 0
            ? Array.Empty<Track>()
            : _order.Skip(_orderPosition + 1).Select(i => _queue[i]).ToList();

    /// <summary>Salta a una canción de la cola; <paramref name="offset"/> 0 es la inmediatamente siguiente.</summary>
    public void PlayUpcoming(int offset)
    {
        var target = _orderPosition + 1 + offset;
        if (target < 0 || target >= _order.Count) return;
        _orderPosition = target;
        StartCurrent();
    }

    /// <summary>Recorre las velocidades 1x, 1.25x, 1.5x, 2x, 0.75x.</summary>
    public void CycleSpeed()
    {
        var index = Array.IndexOf(SpeedSteps, Speed);
        Speed = SpeedSteps[(index + 1) % SpeedSteps.Length];
        _player.SetSpeed(Speed);
    }

    /// <summary>Recorre el temporizador de apagado: desactivado, 15, 30, 45 y 60 minutos.</summary>
    public void CycleSleepTimer()
    {
        _sleepStepIndex = (_sleepStepIndex + 1) % SleepMinutesSteps.Length;
        var minutes = SleepMinutesSteps[_sleepStepIndex];
        _sleepEndUtc = minutes == 0 ? null : DateTime.UtcNow.AddMinutes(minutes);
        SleepTimerText = minutes == 0 ? string.Empty : $"{minutes} min";
    }

    public void SeekTo(double seconds) => _player.SeekTo(TimeSpan.FromSeconds(seconds));

    public void ToggleShuffle()
    {
        IsShuffleOn = !IsShuffleOn;
        if (_queue.Count == 0) return;

        // Se reconstruye el orden dejando la pista actual donde está, sin cortar la reproducción.
        var currentIndex = _order[_orderPosition];
        RebuildOrder(currentIndex);
    }

    public void CycleRepeat() =>
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };

    /// <summary>Detiene todo y vacía la cola (por ejemplo al cerrar sesión).</summary>
    public void Stop()
    {
        _player.Stop();
        _queue.Clear();
        _order.Clear();
        _orderPosition = -1;
        CurrentTrack = null;
        IsPlaying = false;
        StopTimer();
        ResetProgress();
    }

    // ----- Internos -----

    /// <summary>
    /// Calcula el orden de reproducción. Con shuffle, la pista <paramref name="currentIndex"/> queda primera
    /// y el resto se mezcla; sin shuffle se respeta el orden original.
    /// </summary>
    private void RebuildOrder(int currentIndex)
    {
        if (IsShuffleOn)
        {
            var rest = Enumerable.Range(0, _queue.Count).Where(i => i != currentIndex).OrderBy(_ => Random.Shared.Next());
            _order = new[] { currentIndex }.Concat(rest).ToList();
            _orderPosition = 0;
        }
        else
        {
            _order = Enumerable.Range(0, _queue.Count).ToList();
            _orderPosition = currentIndex;
        }
    }

    /// <summary>Carga en el motor la pista que indica <see cref="_orderPosition"/>.</summary>
    private void StartCurrent()
    {
        var track = _queue[_order[_orderPosition]];
        CurrentTrack = track;
        ErrorMessage = null;
        DurationSeconds = Math.Max(1, track.Duration.TotalSeconds);
        DurationText = track.Duration.ToDisplay();
        PositionSeconds = 0;
        PositionText = "0:00";

        _player.Play(track.StreamUri, track.Title, track.Artist);
        _player.SetSpeed(Speed);
        IsPlaying = true;
        StartTimer();
    }

    private void OnTrackCompleted()
    {
        if (RepeatMode == RepeatMode.One) StartCurrent();
        else if (CanGoNext) Next();
        else
        {
            // Fin de la cola: se queda en la última pista, en pausa y al inicio.
            IsPlaying = false;
            StopTimer();
            ResetProgress();
        }
    }

    private void OnPlayerFailed(string message)
    {
        ErrorMessage = message;
        IsPlaying = false;
        StopTimer();
    }

    /// <summary>Actualiza la barra de progreso 2 veces por segundo mientras se reproduce.</summary>
    private void StartTimer()
    {
        _timer ??= CreateTimer();
        if (!_timer.IsRunning) _timer.Start();
    }

    private void StopTimer() => _timer?.Stop();

    private IDispatcherTimer CreateTimer()
    {
        var timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += (_, _) => UpdateProgress();
        return timer;
    }

    private void UpdateProgress()
    {
        if (_sleepEndUtc is { } end && DateTime.UtcNow >= end) FireSleepTimer();

        var position = _player.Position;
        PositionSeconds = position.TotalSeconds;
        PositionText = position.ToDisplay();

        // Algunos streams no informan duración hasta estar preparados: se corrige cuando el motor la conoce.
        var duration = _player.Duration;
        if (duration > TimeSpan.Zero)
        {
            DurationSeconds = duration.TotalSeconds;
            DurationText = duration.ToDisplay();
        }
        IsPlaying = _player.IsPlaying || (IsPlaying && position == TimeSpan.Zero);
    }

    /// <summary>Se acabó el tiempo del temporizador: pausa y lo desactiva.</summary>
    private void FireSleepTimer()
    {
        _sleepEndUtc = null;
        _sleepStepIndex = 0;
        SleepTimerText = string.Empty;
        if (_player.IsPlaying) TogglePlayPause();
    }

    private void ResetProgress()
    {
        PositionSeconds = 0;
        PositionText = "0:00";
    }
}
