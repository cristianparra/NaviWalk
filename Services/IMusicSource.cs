using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>
/// Contrato que cumple cualquier origen de música (Navidrome o local).
/// Las pantallas dependen solo de esta interfaz, de modo que agregar otro origen
/// (por ejemplo Jellyfin) no obliga a tocar la UI.
/// </summary>
public interface IMusicSource
{
    SourceKind Kind { get; }

    /// <summary>Nombre para mostrar en la UI (ej. "Mi servidor" o "Este dispositivo").</summary>
    string DisplayName { get; }

    /// <summary>Álbumes para las filas de la pantalla de inicio.</summary>
    Task<IReadOnlyList<Album>> GetAlbumListAsync(AlbumListType type, int count, CancellationToken ct = default);

    /// <summary>Todos los álbumes, ordenados por nombre (pestaña Biblioteca).</summary>
    Task<IReadOnlyList<Album>> GetAllAlbumsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(CancellationToken ct = default);

    /// <summary>Completa <see cref="MediaCollection.Tracks"/> (álbum o lista) si aún está vacío.</summary>
    Task LoadTracksAsync(MediaCollection collection, CancellationToken ct = default);

    /// <summary>Completa <see cref="Artist.Albums"/> si aún está vacío.</summary>
    Task LoadAlbumsAsync(Artist artist, CancellationToken ct = default);

    Task<SearchResults> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>true si la fuente permite marcar favoritos y calificar (Navidrome sí; local no).</summary>
    bool SupportsRatings { get; }

    /// <summary>Marca o desmarca una canción como favorita en la fuente.</summary>
    Task SetFavoriteAsync(Track track, bool starred, CancellationToken ct = default);

    /// <summary>Califica la canción de 1 a 5 (0 quita la calificación).</summary>
    Task SetRatingAsync(Track track, int rating, CancellationToken ct = default);
}
