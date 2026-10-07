using System.Text.Json;
using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>
/// Implementa <see cref="IMusicSource"/> sobre un servidor Navidrome, traduciendo
/// el JSON de la API Subsonic a los modelos neutrales de la app.
/// </summary>
public sealed class NavidromeMusicSource : IMusicSource
{
    private const int PageSize = 500;     // Máximo que admite getAlbumList2 por llamada.
    private const int CoverSize = 500;    // Tamaño (px) de carátulas pedidas al servidor.

    private readonly SubsonicClient _api;

    public NavidromeMusicSource(SubsonicClient api, string displayName)
    {
        _api = api;
        DisplayName = displayName;
    }

    public SourceKind Kind => SourceKind.Navidrome;
    public string DisplayName { get; }

    public async Task<IReadOnlyList<Album>> GetAlbumListAsync(AlbumListType type, int count, CancellationToken ct = default)
    {
        var subsonicType = type switch
        {
            AlbumListType.Recent => "recent",
            AlbumListType.Newest => "newest",
            AlbumListType.Frequent => "frequent",
            _ => "random"
        };
        var root = await _api.GetAsync("getAlbumList2", [("type", subsonicType), ("size", count.ToString())], ct);
        return root.GetProperty("albumList2").GetArray("album").Select(MapAlbum).ToList();
    }

    public async Task<IReadOnlyList<Album>> GetAllAlbumsAsync(CancellationToken ct = default)
    {
        // La API pagina; se pide de a PageSize hasta recibir una página incompleta.
        var all = new List<Album>();
        for (var offset = 0; ; offset += PageSize)
        {
            var root = await _api.GetAsync("getAlbumList2",
                [("type", "alphabeticalByName"), ("size", PageSize.ToString()), ("offset", offset.ToString())], ct);
            var page = root.GetProperty("albumList2").GetArray("album").Select(MapAlbum).ToList();
            all.AddRange(page);
            if (page.Count < PageSize) break;
        }
        return all;
    }

    public async Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken ct = default)
    {
        var root = await _api.GetAsync("getArtists", ct: ct);
        // La respuesta viene agrupada por letra inicial: artists.index[].artist[]
        return root.GetProperty("artists").GetArray("index")
            .SelectMany(index => index.GetArray("artist"))
            .Select(MapArtist)
            .ToList();
    }

    public async Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(CancellationToken ct = default)
    {
        var root = await _api.GetAsync("getPlaylists", ct: ct);
        return root.GetProperty("playlists").GetArray("playlist").Select(p => new Playlist
        {
            Id = p.GetStringOrEmpty("id"),
            Title = p.GetStringOrEmpty("name"),
            Subtitle = $"{p.GetIntOrDefault("songCount")} canciones",
            Cover = CoverOrNull(p.GetStringOrEmpty("coverArt"))
        }).ToList();
    }

    public async Task LoadTracksAsync(MediaCollection collection, CancellationToken ct = default)
    {
        if (collection.Tracks.Count > 0) return;

        if (collection is Playlist)
        {
            var root = await _api.GetAsync("getPlaylist", [("id", collection.Id)], ct);
            collection.Tracks = root.GetProperty("playlist").GetArray("entry").Select(MapTrack).ToList();
        }
        else
        {
            var root = await _api.GetAsync("getAlbum", [("id", collection.Id)], ct);
            collection.Tracks = root.GetProperty("album").GetArray("song").Select(MapTrack).ToList();
        }
    }

    public async Task LoadAlbumsAsync(Artist artist, CancellationToken ct = default)
    {
        if (artist.Albums.Count > 0) return;
        var root = await _api.GetAsync("getArtist", [("id", artist.Id)], ct);
        artist.Albums = root.GetProperty("artist").GetArray("album").Select(MapAlbum).ToList();
    }

    public async Task<SearchResults> SearchAsync(string query, CancellationToken ct = default)
    {
        var root = await _api.GetAsync("search3",
            [("query", query), ("artistCount", "10"), ("albumCount", "20"), ("songCount", "30")], ct);

        // Si no hay coincidencias, Navidrome devuelve searchResult3 vacío (sin arreglos).
        if (!root.TryGetProperty("searchResult3", out var result)) return SearchResults.Empty;

        return new SearchResults(
            result.GetArray("artist").Select(MapArtist).ToList(),
            result.GetArray("album").Select(MapAlbum).ToList(),
            result.GetArray("song").Select(MapTrack).ToList());
    }

    public bool SupportsRatings => true;

    public async Task SetFavoriteAsync(Track track, bool starred, CancellationToken ct = default) =>
        await _api.GetAsync(starred ? "star" : "unstar", [("id", track.Id)], ct);

    public async Task SetRatingAsync(Track track, int rating, CancellationToken ct = default) =>
        await _api.GetAsync("setRating", [("id", track.Id), ("rating", rating.ToString())], ct);

    // ----- Mapeo JSON -> modelos -----

    private Album MapAlbum(JsonElement a)
    {
        var year = a.GetIntOrDefault("year");
        var artist = a.GetStringOrEmpty("artist");
        return new Album
        {
            Id = a.GetStringOrEmpty("id"),
            Title = a.GetStringOrEmpty("name"),
            ArtistId = a.GetStringOrEmpty("artistId"),
            Subtitle = year > 0 ? $"{artist} · {year}" : artist,
            Cover = CoverOrNull(a.GetStringOrEmpty("coverArt"))
        };
    }

    private Artist MapArtist(JsonElement a) => new()
    {
        Id = a.GetStringOrEmpty("id"),
        Name = a.GetStringOrEmpty("name"),
        AlbumCount = a.GetIntOrDefault("albumCount"),
        Cover = CoverOrNull(a.GetStringOrEmpty("coverArt"))
    };

    private Track MapTrack(JsonElement s)
    {
        var id = s.GetStringOrEmpty("id");
        var coverArtId = s.GetStringOrEmpty("coverArt");
        return new Track
        {
            Id = id,
            Title = s.GetStringOrEmpty("title"),
            Artist = s.GetStringOrEmpty("artist"),
            Album = s.GetStringOrEmpty("album"),
            TrackNumber = s.GetIntOrDefault("track"),
            Duration = TimeSpan.FromSeconds(s.GetIntOrDefault("duration")),
            StreamUri = _api.GetStreamUrl(id),
            Year = s.GetIntOrDefault("year"),
            FormatInfo = BuildFormatInfo(s),
            Cover = CoverOrNull(coverArtId),
            OpenCover = string.IsNullOrEmpty(coverArtId)
                ? null
                : async () => await _api.OpenCoverArtAsync(coverArtId, 64),
            // "starred" solo existe (con la fecha) si el usuario la marcó como favorita.
            IsStarred = s.TryGetProperty("starred", out _),
            Rating = s.GetIntOrDefault("userRating")
        };
    }

    /// <summary>Arma "MP3 · 128 kbps · 44.1 kHz" con los datos que el servidor tenga disponibles.</summary>
    private static string BuildFormatInfo(JsonElement s)
    {
        var parts = new List<string>();
        var suffix = s.GetStringOrEmpty("suffix");
        if (suffix.Length > 0) parts.Add(suffix.ToUpperInvariant());
        var bitRate = s.GetIntOrDefault("bitRate");
        if (bitRate > 0) parts.Add($"{bitRate} kbps");
        var sampling = s.GetIntOrDefault("samplingRate");   // Extensión OpenSubsonic; puede faltar.
        if (sampling > 0) parts.Add($"{sampling / 1000.0:0.#} kHz");
        return string.Join(" · ", parts);
    }

    private ImageSource? CoverOrNull(string coverArtId) =>
        string.IsNullOrEmpty(coverArtId) ? null : ImageSource.FromUri(new Uri(_api.GetCoverArtUrl(coverArtId, CoverSize)));
}
