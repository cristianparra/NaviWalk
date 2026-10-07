using System.Text.Json;
using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>
/// Persistencia de la sesión: qué fuente se eligió y, para Navidrome, las credenciales.
/// Las credenciales van en <see cref="SecureStorage"/> (cifradas con Android Keystore);
/// la preferencia no sensible de fuente va en <see cref="Preferences"/>.
/// </summary>
public sealed class SessionStore
{
    private const string SourceKey = "session.source";
    private const string CredentialsKey = "session.navidrome.credentials";

    public SourceKind GetSourceKind() =>
        Enum.TryParse<SourceKind>(Preferences.Get(SourceKey, nameof(SourceKind.None)), out var kind) ? kind : SourceKind.None;

    public void SetSourceKind(SourceKind kind) => Preferences.Set(SourceKey, kind.ToString());

    public async Task SaveCredentialsAsync(ServerCredentials credentials) =>
        await SecureStorage.Default.SetAsync(CredentialsKey, JsonSerializer.Serialize(credentials));

    /// <summary>Devuelve las credenciales guardadas o null si no hay (o no se pueden descifrar).</summary>
    public async Task<ServerCredentials?> LoadCredentialsAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(CredentialsKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ServerCredentials>(json);
        }
        catch (Exception)
        {
            // Pasa si cambió la clave del Keystore (restauración de backup, cambio de bloqueo de pantalla...).
            // Lo más seguro es descartar y pedir login de nuevo.
            ClearCredentials();
            return null;
        }
    }

    public void ClearCredentials() => SecureStorage.Default.Remove(CredentialsKey);

    public void Clear()
    {
        ClearCredentials();
        Preferences.Remove(SourceKey);
    }
}

/// <summary>
/// Mantiene la fuente de música activa y gestiona iniciar/restaurar/cerrar sesión.
/// Es singleton: todas las pantallas consultan <see cref="Current"/>.
/// </summary>
public sealed class SessionService
{
    private readonly SessionStore _store;
    private readonly HttpClient _http;
    private readonly ILocalMediaScanner _scanner;

    public SessionService(SessionStore store, HttpClient http, ILocalMediaScanner scanner)
    {
        _store = store;
        _http = http;
        _scanner = scanner;
    }

    /// <summary>Fuente activa; null mientras no se haya iniciado sesión.</summary>
    public IMusicSource? Current { get; private set; }

    /// <summary>
    /// Intenta reanudar la sesión anterior sin interacción del usuario.
    /// Para Navidrome NO se hace ping: así la app abre al instante y funciona aunque no haya red
    /// en ese momento (las pantallas muestran el error y permiten reintentar).
    /// </summary>
    public async Task<bool> TryRestoreAsync()
    {
        switch (_store.GetSourceKind())
        {
            case SourceKind.Local:
                Current = new LocalMusicSource(_scanner);
                return true;

            case SourceKind.Navidrome:
                var credentials = await _store.LoadCredentialsAsync();
                if (credentials is null) return false;
                Current = CreateNavidromeSource(credentials);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Elige el modo "música del dispositivo".</summary>
    public void UseLocal()
    {
        Current = new LocalMusicSource(_scanner);
        _store.SetSourceKind(SourceKind.Local);
    }

    /// <summary>
    /// Valida las credenciales contra el servidor y, si funcionan, las guarda de forma segura.
    /// Lanza <see cref="SubsonicException"/> con un mensaje apto para mostrar al usuario si fallan.
    /// </summary>
    public async Task ConnectNavidromeAsync(ServerCredentials credentials, CancellationToken ct = default)
    {
        var source = CreateNavidromeSource(credentials);
        await new SubsonicClient(_http, credentials).PingAsync(ct);

        await _store.SaveCredentialsAsync(credentials);
        _store.SetSourceKind(SourceKind.Navidrome);
        Current = source;
    }

    /// <summary>Cierra la sesión y borra credenciales; la próxima vez se vuelve a mostrar la bienvenida.</summary>
    public void SignOut()
    {
        _store.Clear();
        Current = null;
    }

    private NavidromeMusicSource CreateNavidromeSource(ServerCredentials credentials)
    {
        var host = new Uri(SubsonicClient.NormalizeServerUrl(credentials.ServerUrl)).Host;
        return new NavidromeMusicSource(new SubsonicClient(_http, credentials), host);
    }
}
