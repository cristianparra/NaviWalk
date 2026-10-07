using Android.Content;
using Android.Media;
using NaviWalk.Services;

namespace NaviWalk.AndroidImpl;

/// <summary>
/// <see cref="IAudioPlayer"/> sobre <see cref="MediaPlayer"/> de Android.
/// Maneja tanto streaming http(s) (Navidrome) como content:// (archivos locales).
/// Se crea un MediaPlayer nuevo por pista: es más simple y evita estados inconsistentes.
/// </summary>
public sealed class AndroidAudioPlayer : IAudioPlayer
{
    private MediaPlayer? _player;
    private bool _prepared;            // true cuando el MediaPlayer ya puede responder Start/Seek/Position.
    private bool _startWhenPrepared;   // El usuario quiere reproducir pero la pista aún se está cargando.
    private bool _notificationPermissionAsked;
    private float _speed = 1f;

    public event EventHandler? Completed;
    public event EventHandler<string>? Failed;

    public bool IsPlaying => _player is not null && (_prepared ? _player.IsPlaying : _startWhenPrepared);

    public TimeSpan Position => _prepared && _player is not null
        ? TimeSpan.FromMilliseconds(_player.CurrentPosition)
        : TimeSpan.Zero;

    public TimeSpan Duration => _prepared && _player is not null
        ? TimeSpan.FromMilliseconds(Math.Max(0, _player.Duration))
        : TimeSpan.Zero;

    public void Play(string uri, string title, string artist)
    {
        ReleasePlayer();
        var context = global::Android.App.Application.Context;

        var player = new MediaPlayer();
        _player = player;
        _startWhenPrepared = true;

        player.SetAudioAttributes(new AudioAttributes.Builder()!
            .SetContentType(AudioContentType.Music)!
            .SetUsage(AudioUsageKind.Media)!
            .Build()!);
        // Mantiene la CPU activa con la pantalla apagada.
        player.SetWakeMode(context, global::Android.OS.WakeLockFlags.Partial);

        // Cada handler comprueba que su reproductor siga siendo el vigente: así los eventos
        // tardíos de una pista ya descartada no afectan a la nueva.
        player.Prepared += (_, _) =>
        {
            if (player != _player) return;
            _prepared = true;
            if (_startWhenPrepared)
            {
                player.Start();
                ApplySpeed();
            }
        };
        player.Completion += (_, _) =>
        {
            if (player == _player) Completed?.Invoke(this, EventArgs.Empty);
        };
        player.Error += (_, e) =>
        {
            if (player != _player) return;
            e.Handled = true;
            Failed?.Invoke(this, "No se pudo reproducir esta pista. Revisa la conexión con el servidor.");
        };

        try
        {
            player.SetDataSource(context, global::Android.Net.Uri.Parse(uri)!);
            player.PrepareAsync();
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, $"No se pudo abrir la pista: {ex.Message}");
            return;
        }

        RequestNotificationPermissionOnce();
        PlaybackForegroundService.Start(context, title, artist);
    }

    public void Pause()
    {
        _startWhenPrepared = false;
        if (_prepared && _player is { IsPlaying: true }) _player.Pause();
    }

    public void Resume()
    {
        _startWhenPrepared = true;
        if (_prepared)
        {
            _player?.Start();
            ApplySpeed();
        }
    }

    public void SetSpeed(float speed)
    {
        _speed = speed;
        // Fijar PlaybackParams en un reproductor pausado lo reanudaría, por eso solo se aplica sonando.
        if (_prepared && _player is { IsPlaying: true }) ApplySpeed();
    }

    private void ApplySpeed()
    {
        try { _player!.PlaybackParams = new PlaybackParams().SetSpeed(_speed)!; }
        catch (Exception) { /* Algunos dispositivos no soportan cambiar la velocidad. */ }
    }

    public void Stop()
    {
        ReleasePlayer();
        PlaybackForegroundService.Stop(global::Android.App.Application.Context);
    }

    public void SeekTo(TimeSpan position)
    {
        if (_prepared) _player?.SeekTo((int)position.TotalMilliseconds);
    }

    /// <summary>Libera el MediaPlayer actual (los recursos nativos no los recoge el GC a tiempo).</summary>
    private void ReleasePlayer()
    {
        _prepared = false;
        _startWhenPrepared = false;
        var old = _player;
        _player = null;
        if (old is null) return;
        try { old.Reset(); old.Release(); } catch (Exception) { /* ya estaba liberado */ }
        old.Dispose();
    }

    /// <summary>
    /// Android 13+ exige permiso para mostrar la notificación del servicio. Se pide al reproducir
    /// por primera vez (momento en que el usuario entiende para qué sirve). Sin él, la música suena igual.
    /// </summary>
    private void RequestNotificationPermissionOnce()
    {
        if (_notificationPermissionAsked || !OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        _notificationPermissionAsked = true;

        var activity = Platform.CurrentActivity;
        if (activity?.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != global::Android.Content.PM.Permission.Granted)
            activity?.RequestPermissions(["android.permission.POST_NOTIFICATIONS"], 1001);
    }
}
