using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NaviWalk.Models;
using NaviWalk.Services;

namespace NaviWalk.ViewModels;

/// <summary>Fila horizontal de álbumes en la pantalla de inicio (ej. "Añadidos recientemente").</summary>
public sealed record AlbumSection(string Title, IReadOnlyList<Album> Albums);

/// <summary>Pantalla de inicio: saludo y filas de álbumes para descubrir.</summary>
public sealed partial class HomeViewModel(SessionService session, NavigationService navigation) : BaseViewModel
{
    private const int AlbumsPerSection = 15;

    public ObservableCollection<AlbumSection> Sections { get; } = new();

    public string Greeting => DateTime.Now.Hour switch
    {
        < 12 => "Buenos días",
        < 20 => "Buenas tardes",
        _ => "Buenas noches"
    };

    public string SourceName => session.Current?.DisplayName ?? string.Empty;

    // Fuente para la que ya se cargó. Las pestañas viven en caché, así que si el usuario cambia de
    // fuente (cerrar sesión + nuevo login) hay que recargar aunque la página ya se haya mostrado antes.
    private IMusicSource? _loadedSource;

    /// <summary>Lo llama la página al aparecer: carga solo si aún no se cargó para la fuente activa.</summary>
    public void LoadIfNeeded()
    {
        if (session.Current is not null && !ReferenceEquals(_loadedSource, session.Current))
            LoadCommand.Execute(null);
    }

    [RelayCommand]
    private Task Load() => RunAsync(async () =>
    {
        var source = session.Current ?? throw new InvalidOperationException("No hay una fuente de música activa.");
        _loadedSource = source;
        OnPropertyChanged(nameof(Greeting));
        OnPropertyChanged(nameof(SourceName));

        // Las cuatro consultas se lanzan en paralelo para que la pantalla cargue más rápido.
        var requests = new (string Title, AlbumListType Type)[]
        {
            ("Añadidos recientemente", AlbumListType.Newest),
            ("Escuchados recientemente", AlbumListType.Recent),
            ("Tus favoritos", AlbumListType.Frequent),
            ("Descubre algo nuevo", AlbumListType.Random)
        };
        var results = await Task.WhenAll(requests.Select(r => source.GetAlbumListAsync(r.Type, AlbumsPerSection)));

        Sections.Clear();
        for (var i = 0; i < requests.Length; i++)
        {
            // Las filas vacías (ej. "favoritos" en modo local) simplemente no se muestran.
            if (results[i].Count > 0) Sections.Add(new AlbumSection(requests[i].Title, results[i]));
        }
    });

    [RelayCommand] private Task OpenAlbum(Album album) => navigation.OpenCollectionAsync(album);
    [RelayCommand] private Task SwitchSource() => navigation.ConfirmSignOutAsync();
}

/// <summary>Pestaña "Biblioteca": álbumes, artistas y listas de reproducción.</summary>
public sealed partial class LibraryViewModel(SessionService session, NavigationService navigation) : BaseViewModel
{
    public ObservableCollection<Album> Albums { get; } = new();
    public ObservableCollection<Artist> Artists { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAlbums), nameof(ShowArtists), nameof(ShowPlaylists))]
    private LibraryTab _selectedTab = LibraryTab.Albums;

    public bool ShowAlbums => SelectedTab == LibraryTab.Albums;
    public bool ShowArtists => SelectedTab == LibraryTab.Artists;
    public bool ShowPlaylists => SelectedTab == LibraryTab.Playlists;

    [RelayCommand]
    private void SelectTab(LibraryTab tab) => SelectedTab = tab;

    private IMusicSource? _loadedSource;

    /// <summary>Lo llama la página al aparecer: carga solo si aún no se cargó para la fuente activa.</summary>
    public void LoadIfNeeded()
    {
        if (session.Current is not null && !ReferenceEquals(_loadedSource, session.Current))
            LoadCommand.Execute(null);
    }

    [RelayCommand]
    private Task Load() => RunAsync(async () =>
    {
        var source = session.Current ?? throw new InvalidOperationException("No hay una fuente de música activa.");
        _loadedSource = source;

        // Con la fuente local, tirar para refrescar vuelve a escanear el dispositivo.
        if (source is LocalMusicSource local) await local.RefreshAsync();

        var albums = source.GetAllAlbumsAsync();
        var artists = source.GetArtistsAsync();
        var playlists = source.GetPlaylistsAsync();
        await Task.WhenAll(albums, artists, playlists);

        Replace(Albums, albums.Result);
        Replace(Artists, artists.Result);
        Replace(Playlists, playlists.Result);
    });

    [RelayCommand] private Task OpenAlbum(Album album) => navigation.OpenCollectionAsync(album);
    [RelayCommand] private Task OpenArtist(Artist artist) => navigation.OpenArtistAsync(artist);
    [RelayCommand] private Task OpenPlaylist(Playlist playlist) => navigation.OpenCollectionAsync(playlist);
    [RelayCommand] private Task SwitchSource() => navigation.ConfirmSignOutAsync();

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}

public enum LibraryTab { Albums, Artists, Playlists }

/// <summary>Búsqueda global con "debounce": espera a que el usuario deje de escribir antes de consultar.</summary>
public sealed partial class SearchViewModel(SessionService session, PlaybackService playback, NavigationService navigation) : BaseViewModel
{
    private const int DebounceMilliseconds = 400;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<Artist> Artists { get; } = new();
    public ObservableCollection<Album> Albums { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private bool _hasSearched;

    public bool ShowNoResults => HasSearched && Artists.Count + Albums.Count + Tracks.Count == 0 && !IsBusy && !HasError;

    private IMusicSource? _activeSource;

    /// <summary>Lo llama la página al aparecer: si cambió la fuente, descarta la búsqueda anterior.</summary>
    public void ResetIfSourceChanged()
    {
        if (ReferenceEquals(_activeSource, session.Current)) return;
        _activeSource = session.Current;
        QueryText = string.Empty;   // Dispara OnQueryTextChanged, que limpia los resultados.
    }

    // CommunityToolkit genera este gancho para la propiedad _queryText.
    partial void OnQueryTextChanged(string value) => _ = SearchAsync(value);

    private async Task SearchAsync(string text)
    {
        // Cada pulsación cancela la búsqueda pendiente anterior.
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(text))
        {
            Clear();
            return;
        }

        try
        {
            await Task.Delay(DebounceMilliseconds, ct);
            var source = session.Current;
            if (source is null) return;

            IsBusy = true;
            ErrorMessage = null;
            var results = await source.SearchAsync(text.Trim(), ct);
            ct.ThrowIfCancellationRequested();

            Fill(Artists, results.Artists);
            Fill(Albums, results.Albums);
            Fill(Tracks, results.Tracks);
            HasSearched = true;
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is SubsonicException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (!ct.IsCancellationRequested) IsBusy = false;
            OnPropertyChanged(nameof(ShowNoResults));
        }
    }

    private void Clear()
    {
        Artists.Clear();
        Albums.Clear();
        Tracks.Clear();
        HasSearched = false;
        ErrorMessage = null;
        IsBusy = false;
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    [RelayCommand] private Task OpenAlbum(Album album) => navigation.OpenCollectionAsync(album);
    [RelayCommand] private Task OpenArtist(Artist artist) => navigation.OpenArtistAsync(artist);

    /// <summary>Al tocar una canción de los resultados suena esa lista de resultados desde ella.</summary>
    [RelayCommand]
    private void PlayTrack(Track track)
    {
        playback.PlayQueue(Tracks.ToList(), Tracks.IndexOf(track));
    }
}

/// <summary>Detalle de álbum o lista de reproducción: portada, acciones y pistas.</summary>
public sealed partial class CollectionViewModel(SessionService session, PlaybackService playback) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    private MediaCollection? _collection;

    public ObservableCollection<Track> Tracks { get; } = new();

    /// <summary>Shell entrega aquí el objeto enviado por <see cref="NavigationService"/>.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationService.ItemKey, out var item) && item is MediaCollection collection)
        {
            Collection = collection;
            _ = LoadAsync();
        }
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        if (Collection is null || session.Current is null) return;
        await session.Current.LoadTracksAsync(Collection);

        Tracks.Clear();
        foreach (var track in Collection.Tracks) Tracks.Add(track);
    });

    [RelayCommand] private void PlayAll() => playback.PlayQueue(Collection!.Tracks);
    [RelayCommand] private void Shuffle() => playback.PlayShuffled(Collection!.Tracks);

    [RelayCommand]
    private void PlayTrack(Track track)
    {
        if (Collection is null) return;
        playback.PlayQueue(Collection.Tracks, Collection.Tracks.IndexOf(track));
    }

    [RelayCommand] private Task Back() => Shell.Current.GoToAsync("..");
}

/// <summary>Detalle de artista: sus álbumes.</summary>
public sealed partial class ArtistViewModel(SessionService session, NavigationService navigation) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    private Artist? _artist;

    public ObservableCollection<Album> Albums { get; } = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationService.ItemKey, out var item) && item is Artist artist)
        {
            Artist = artist;
            _ = LoadAsync();
        }
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        if (Artist is null || session.Current is null) return;
        await session.Current.LoadAlbumsAsync(Artist);

        Albums.Clear();
        foreach (var album in Artist.Albums) Albums.Add(album);
    });

    [RelayCommand] private Task OpenAlbum(Album album) => navigation.OpenCollectionAsync(album);
    [RelayCommand] private Task Back() => Shell.Current.GoToAsync("..");
}
