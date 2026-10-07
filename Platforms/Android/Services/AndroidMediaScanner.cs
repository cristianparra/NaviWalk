using Android.Content;
using Android.Graphics;
using Android.Provider;
using NaviWalk.Services;

namespace NaviWalk.AndroidImpl;

/// <summary>Permiso de lectura de audio: READ_MEDIA_AUDIO en Android 13+, READ_EXTERNAL_STORAGE antes.</summary>
public sealed class AudioReadPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? [("android.permission.READ_MEDIA_AUDIO", true)]
            : [("android.permission.READ_EXTERNAL_STORAGE", true)];
}

/// <summary><see cref="ILocalMediaScanner"/> sobre el MediaStore de Android.</summary>
public sealed class AndroidMediaScanner : ILocalMediaScanner
{
    private const string UnknownArtist = "Artista desconocido";
    private const string UnknownAlbum = "Álbum desconocido";

    public async Task<IReadOnlyList<LocalTrackInfo>> ScanAsync()
    {
        var status = await Permissions.CheckStatusAsync<AudioReadPermission>();
        if (status != PermissionStatus.Granted)
            status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<AudioReadPermission>);

        if (status != PermissionStatus.Granted)
            throw new UnauthorizedAccessException("Se necesita permiso para leer la música del dispositivo.");

        // La consulta puede tardar con bibliotecas grandes: fuera del hilo de UI.
        return await Task.Run(QueryMediaStore);
    }

    private static IReadOnlyList<LocalTrackInfo> QueryMediaStore()
    {
        var resolver = global::Android.App.Application.Context.ContentResolver!;
        var collection = MediaStore.Audio.Media.ExternalContentUri!;

        string[] projection =
        [
            MediaStore.Audio.Media.InterfaceConsts.Id,
            MediaStore.Audio.Media.InterfaceConsts.Title,
            MediaStore.Audio.Media.InterfaceConsts.Artist,
            MediaStore.Audio.Media.InterfaceConsts.Album,
            MediaStore.Audio.Media.InterfaceConsts.AlbumId,
            MediaStore.Audio.Media.InterfaceConsts.Track,
            MediaStore.Audio.Media.InterfaceConsts.Duration,
            MediaStore.Audio.Media.InterfaceConsts.DateAdded,
            MediaStore.Audio.Media.InterfaceConsts.MimeType
        ];

        // Solo música (excluye tonos, notas de voz, podcasts marcados como tal, etc.).
        var selection = $"{MediaStore.Audio.Media.InterfaceConsts.IsMusic} != 0";

        var result = new List<LocalTrackInfo>();
        using var cursor = resolver.Query(collection, projection, selection, null, null);
        if (cursor is null) return result;

        while (cursor.MoveToNext())
        {
            var id = cursor.GetLong(0);
            result.Add(new LocalTrackInfo(
                Id: id,
                Title: cursor.GetString(1) ?? "Sin título",
                Artist: CleanName(cursor.GetString(2), UnknownArtist),
                Album: CleanName(cursor.GetString(3), UnknownAlbum),
                AlbumId: cursor.GetLong(4),
                // Algunos archivos codifican disco*1000 + pista; nos quedamos con la pista.
                TrackNumber: cursor.GetInt(5) % 1000,
                DurationMs: cursor.GetLong(6),
                DateAddedSeconds: cursor.GetLong(7),
                ContentUri: ContentUris.WithAppendedId(collection, id)!.ToString()!,
                MimeType: cursor.GetString(8) ?? string.Empty));
        }
        return result;
    }

    /// <summary>MediaStore usa el literal "&lt;unknown&gt;" cuando falta la etiqueta.</summary>
    private static string CleanName(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value == "<unknown>" ? fallback : value;

    public Stream? OpenArtwork(string contentUri)
    {
        // LoadThumbnail existe desde Android 10 y lee la carátula incrustada o de la carpeta.
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return null;
        try
        {
            var resolver = global::Android.App.Application.Context.ContentResolver!;
            using var bitmap = resolver.LoadThumbnail(global::Android.Net.Uri.Parse(contentUri)!, new global::Android.Util.Size(500, 500), null);
            var stream = new MemoryStream();
            bitmap?.Compress(Bitmap.CompressFormat.Jpeg!, 88, stream);
            stream.Position = 0;
            return stream;
        }
        catch (Exception)
        {
            return null; // Sin carátula: la UI muestra el ícono por defecto.
        }
    }
}
