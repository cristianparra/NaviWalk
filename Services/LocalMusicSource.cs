using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>Datos crudos de un archivo de audio del dispositivo (los entrega el escáner de la plataforma).</summary>
public sealed record LocalTrackInfo(
    long Id,
    string Title,
    string Artist,
    string Album,
    long AlbumId,
    int TrackNumber,
    long DurationMs,
    long DateAddedSeconds,
    string ContentUri,
    string MimeType);

/// <summary>Acceso al almacén de medios del dispositivo. La implementación vive en Platforms/Android.</summary>
public interface ILocalMediaScanner
{
    /// <summary>
    /// Pide el permiso de lectura de audio (si hace falta) y devuelve todas las pistas de música.
    /// Lanza <see cref="UnauthorizedAccessException"/> si el usuario rechaza el permiso.
    /// </summary>
    Task<IReadOnlyList<LocalTrackInfo>> ScanAsync();

    /// <summary>Abre la carátula (incrustada o de carpeta) de una pista; null si no tiene.</summary>
    Stream? OpenArtwork(string contentUri);
}

/// <summary>
/// <see cref="IMusicSource"/> sobre los archivos del dispositivo. Escanea una vez, agrupa en
/// álbumes y artistas en memoria, y responde todo desde ese caché.
/// </summary>
public sealed class LocalMusicSource : IMusicSource
{
    private readonly ILocalMediaScanner _scanner;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private List<Track> _tracks = new();
    private List<Album> _albums = new();
    private List<Artist> _artists = new();
    private Dictionary<string, long> _dateAdded = new();
    private bool _loaded;

    public LocalMusicSource(ILocalMediaScanner scanner) => _scanner = scanner;

    public SourceKind Kind => SourceKind.Local;
    public string DisplayName => "Este dispositivo";

    public async Task<IReadOnlyList<Album>> GetAlbumListAsync(AlbumListType type, int count, CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        IEnumerable<Album> list = type switch
        {
            // En local no hay historial de reproducción, por eso "Recent" y "Frequent" no aplican.
            AlbumListType.Newest => _albums.OrderByDescending(a => _dateAdded.GetValueOrDefault(a.Id)),
            AlbumListType.Random => _albums.OrderBy(_ => Random.Shared.Next()),
            _ => Enumerable.Empty<Album>()
        };
        return list.Take(count).ToList();
    }

    public async Task<IReadOnlyList<Album>> GetAllAlbumsAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        return _albums;
    }

    public async Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        return _artists;
    }

    // El dispositivo no tiene listas de reproducción propias de la app.
    public Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Playlist>>(Array.Empty<Playlist>());

    // Las pistas y álbumes ya vienen cargados al agrupar, no hay nada que pedir.
    public Task LoadTracksAsync(MediaCollection collection, CancellationToken ct = default) => Task.CompletedTask;
    public Task LoadAlbumsAsync(Artist artist, CancellationToken ct = default) => Task.CompletedTask;

    // Sin favoritos ni calificaciones en local: la pantalla de reproducción oculta esos controles.
    public bool SupportsRatings => false;
    public Task SetFavoriteAsync(Track track, bool starred, CancellationToken ct = default) => Task.CompletedTask;
    public Task SetRatingAsync(Track track, int rating, CancellationToken ct = default) => Task.CompletedTask;

    public async Task<SearchResults> SearchAsync(string query, CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        bool Match(string text) => text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

        return new SearchResults(
            _artists.Where(a => Match(a.Name)).Take(10).ToList(),
            _albums.Where(a => Match(a.Title) || Match(a.Subtitle)).Take(20).ToList(),
            _tracks.Where(t => Match(t.Title) || Match(t.Artist)).Take(30).ToList());
    }

    /// <summary>Fuerza un nuevo escaneo (ej. al hacer pull-to-refresh tras copiar música nueva).</summary>
    public async Task RefreshAsync()
    {
        _loaded = false;
        await EnsureLoadedAsync();
    }

    /// <summary>Escanea el dispositivo la primera vez; las siguientes llamadas usan el caché.</summary>
    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return;
            var infos = await _scanner.ScanAsync();
            BuildLibrary(infos);
            _loaded = true;
        }
        finally { _loadLock.Release(); }
    }

    /// <summary>"audio/mpeg" -> "MP3", "audio/flac" -> "FLAC"... Lo no reconocido se muestra en mayúsculas.</summary>
    private static string FormatFromMime(string mime) => mime switch
    {
        "audio/mpeg" => "MP3",
        "audio/mp4" or "audio/aac" or "audio/x-m4a" => "AAC",
        "audio/x-wav" or "audio/wav" => "WAV",
        "audio/ogg" or "application/ogg" => "OGG",
        "" => string.Empty,
        _ => mime[(mime.LastIndexOf('/') + 1)..].Replace("x-", "").ToUpperInvariant()
    };

    /// <summary>Convierte la lista plana de archivos en pistas, álbumes y artistas.</summary>
    private void BuildLibrary(IReadOnlyList<LocalTrackInfo> infos)
    {
        var tracks = new List<Track>();
        var albums = new List<Album>();
        var dateAdded = new Dictionary<string, long>();

        foreach (var group in infos.GroupBy(i => i.AlbumId))
        {
            var items = group.OrderBy(i => i.TrackNumber).ThenBy(i => i.Title).ToList();
            var first = items[0];

            // Una sola instancia de carátula compartida por el álbum y todas sus pistas.
            var cover = ImageSource.FromStream(() => _scanner.OpenArtwork(first.ContentUri)!);
            var albumTracks = items.Select(i => new Track
            {
                Id = i.Id.ToString(),
                Title = i.Title,
                Artist = i.Artist,
                Album = i.Album,
                TrackNumber = i.TrackNumber,
                Duration = TimeSpan.FromMilliseconds(i.DurationMs),
                StreamUri = i.ContentUri,
                FormatInfo = FormatFromMime(i.MimeType),
                Cover = cover,
                OpenCover = () => Task.FromResult(_scanner.OpenArtwork(i.ContentUri))
            }).ToList();

            var album = new Album
            {
                Id = group.Key.ToString(),
                Title = first.Album,
                Subtitle = first.Artist,
                ArtistId = first.Artist,
                Cover = cover,
                Tracks = albumTracks
            };
            albums.Add(album);
            tracks.AddRange(albumTracks);
            dateAdded[album.Id] = items.Max(i => i.DateAddedSeconds);
        }

        // En local el "id" de un artista es su nombre, no hay otra clave disponible.
        var artists = albums.GroupBy(a => a.ArtistId)
            .Select(g => new Artist
            {
                Id = g.Key,
                Name = g.Key,
                AlbumCount = g.Count(),
                Cover = g.First().Cover,
                Albums = g.OrderBy(a => a.Title).ToList()
            })
            .OrderBy(a => a.Name)
            .ToList();

        _tracks = tracks;
        _albums = albums.OrderBy(a => a.Title).ToList();
        _artists = artists;
        _dateAdded = dateAdded;
    }
}
