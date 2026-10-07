namespace NaviWalk.Models;

/// <summary>Origen de la música que usa la app.</summary>
public enum SourceKind
{
    /// <summary>Aún no se eligió ninguno (primer uso o sesión cerrada).</summary>
    None,
    /// <summary>Archivos de audio almacenados en el dispositivo.</summary>
    Local,
    /// <summary>Servidor Navidrome (API Subsonic).</summary>
    Navidrome
}

/// <summary>Criterio para listar álbumes en la pantalla de inicio.</summary>
public enum AlbumListType
{
    Recent,    // Reproducidos recientemente
    Newest,    // Añadidos recientemente
    Frequent,  // Más reproducidos
    Random     // Aleatorios (descubrir)
}

/// <summary>
/// Modelos neutrales respecto a la fuente: la UI y el reproductor solo conocen estos tipos,
/// da igual si los datos vienen de Navidrome o del almacenamiento local.
/// </summary>
public sealed partial class Track : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public int TrackNumber { get; init; }
    public TimeSpan Duration { get; init; }

    /// <summary>URL http(s) o content:// desde la que se reproduce el audio.</summary>
    public required string StreamUri { get; init; }

    /// <summary>Carátula; null si no hay (la UI muestra un ícono de nota).</summary>
    public ImageSource? Cover { get; init; }

    public int Year { get; init; }

    /// <summary>Formato de audio listo para mostrar, ej. "MP3 · 128 kbps · 44.1 kHz" (vacío si no se conoce).</summary>
    public string FormatInfo { get; init; } = string.Empty;

    /// <summary>Abre la imagen de carátula en bruto (para calcular su color dominante). Null si no hay.</summary>
    public Func<Task<Stream?>>? OpenCover { get; init; }

    /// <summary>Marcada como favorita (corazón). Solo aplica a fuentes que lo soportan (Navidrome).</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isStarred;

    /// <summary>Calificación de 0 (sin calificar) a 5 estrellas.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _rating;

    /// <summary>Texto "Álbum · 2022" para la pantalla de reproducción.</summary>
    public string AlbumYear => Year > 0 ? $"{Album} · {Year}" : Album;

    /// <summary>Texto "3:45" listo para mostrar.</summary>
    public string DurationText => Duration.ToDisplay();

    /// <summary>Texto "Artista · Álbum" para filas de resultados.</summary>
    public string Subtitle => string.IsNullOrEmpty(Album) ? Artist : $"{Artist} · {Album}";
}

/// <summary>Base común de álbumes y listas de reproducción: ambos tienen portada y pistas.</summary>
public abstract class MediaCollection
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public ImageSource? Cover { get; init; }

    /// <summary>Pistas de la colección. Puede estar vacía hasta llamar a <c>IMusicSource.LoadTracksAsync</c>.</summary>
    public List<Track> Tracks { get; set; } = new();
}

public sealed class Album : MediaCollection
{
    public string ArtistId { get; init; } = string.Empty;
}

public sealed class Playlist : MediaCollection;

public sealed class Artist
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Cover { get; init; }
    public int AlbumCount { get; init; }

    /// <summary>Texto "5 álbumes" listo para mostrar.</summary>
    public string Subtitle => AlbumCount == 1 ? "1 álbum" : $"{AlbumCount} álbumes";

    /// <summary>Álbumes del artista; se completan con <c>IMusicSource.LoadAlbumsAsync</c>.</summary>
    public List<Album> Albums { get; set; } = new();
}

/// <summary>Resultado de una búsqueda global.</summary>
public sealed record SearchResults(
    IReadOnlyList<Artist> Artists,
    IReadOnlyList<Album> Albums,
    IReadOnlyList<Track> Tracks)
{
    public static SearchResults Empty { get; } = new(Array.Empty<Artist>(), Array.Empty<Album>(), Array.Empty<Track>());
    public bool IsEmpty => Artists.Count == 0 && Albums.Count == 0 && Tracks.Count == 0;
}

/// <summary>Datos de acceso a un servidor Navidrome.</summary>
public sealed record ServerCredentials(string ServerUrl, string UserName, string Password);

/// <summary>Utilidades de formato de tiempo.</summary>
public static class TimeFormat
{
    /// <summary>Devuelve "m:ss" (o "h:mm:ss" si dura una hora o más).</summary>
    public static string ToDisplay(this TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
}
