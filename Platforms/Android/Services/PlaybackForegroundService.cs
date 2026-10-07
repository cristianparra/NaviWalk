using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace NaviWalk.AndroidImpl;

/// <summary>
/// Servicio en primer plano cuyo único fin es mantener viva la app (y mostrar una notificación)
/// mientras suena música con la pantalla apagada o la app en segundo plano.
/// El audio en sí lo maneja <see cref="AndroidAudioPlayer"/>.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class PlaybackForegroundService : Service
{
    private const string ChannelId = "naviwalk_playback";
    private const int NotificationId = 1;
    private const string ExtraTitle = "title";
    private const string ExtraArtist = "artist";

    /// <summary>Inicia (o actualiza) la notificación con la pista actual.</summary>
    public static void Start(Context context, string title, string artist)
    {
        var intent = new Intent(context, typeof(PlaybackForegroundService));
        intent.PutExtra(ExtraTitle, title);
        intent.PutExtra(ExtraArtist, artist);
        context.StartForegroundService(intent);
    }

    public static void Stop(Context context) =>
        context.StopService(new Intent(context, typeof(PlaybackForegroundService)));

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var title = intent?.GetStringExtra(ExtraTitle) ?? "NaviWalk";
        var artist = intent?.GetStringExtra(ExtraArtist) ?? string.Empty;

        EnsureChannel();
        var notification = BuildNotification(title, artist);

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, ForegroundService.TypeMediaPlayback);
        else
            StartForeground(NotificationId, notification);

        // NotSticky: si el sistema mata el servicio, no se reinicia solo sin reproductor.
        return StartCommandResult.NotSticky;
    }

    private Notification BuildNotification(string title, string artist)
    {
        // Al tocar la notificación se vuelve a la app sin crear otra instancia.
        var openApp = new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pending = PendingIntent.GetActivity(this, 0, openApp, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)!
            .SetContentTitle(title)!
            .SetContentText(artist)!
            .SetContentIntent(pending)!
            .SetOngoing(true)!
            .SetVisibility(NotificationVisibility.Public)!
            .Build()!;
    }

    private void EnsureChannel()
    {
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        if (manager.GetNotificationChannel(ChannelId) != null) return;

        var channel = new NotificationChannel(ChannelId, "Reproducción", NotificationImportance.Low)
        {
            Description = "Muestra la canción que está sonando"
        };
        manager.CreateNotificationChannel(channel);
    }
}
