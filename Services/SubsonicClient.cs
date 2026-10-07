using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaviWalk.Models;

namespace NaviWalk.Services;

/// <summary>Error devuelto por el servidor o problema de conexión con Navidrome.</summary>
public sealed class SubsonicException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Cliente HTTP mínimo de la API Subsonic 1.16.1 (la que expone Navidrome).
/// Autenticación por token: t = md5(contraseña + salt), así la contraseña nunca viaja en claro.
/// Docs: https://www.subsonic.org/pages/api.jsp
/// </summary>
public sealed class SubsonicClient
{
    private const string ApiVersion = "1.16.1";
    private const string ClientName = "NaviWalk";

    private readonly HttpClient _http;
    private readonly ServerCredentials _credentials;
    private readonly string _baseUrl;

    public SubsonicClient(HttpClient http, ServerCredentials credentials)
    {
        _http = http;
        _credentials = credentials;
        _baseUrl = NormalizeServerUrl(credentials.ServerUrl);
    }

    /// <summary>
    /// Acepta "192.168.1.10:4533", "http://host:4533/" o "https://musica.midominio.com"
    /// y devuelve una URL base sin barra final. Si falta el esquema se asume http.
    /// </summary>
    public static string NormalizeServerUrl(string input)
    {
        var url = input.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }
        return url.TrimEnd('/');
    }

    /// <summary>Comprueba conexión y credenciales. Lanza <see cref="SubsonicException"/> si fallan.</summary>
    public async Task PingAsync(CancellationToken ct = default) => await GetAsync("ping", ct: ct);

    /// <summary>
    /// Llama a un endpoint y devuelve el nodo "subsonic-response" ya validado.
    /// </summary>
    public async Task<JsonElement> GetAsync(string endpoint, IEnumerable<(string Key, string Value)>? query = null, CancellationToken ct = default)
    {
        var url = BuildUrl(endpoint, query);
        try
        {
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

            // Se clona el elemento porque el JsonDocument se libera al salir del método.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty("subsonic-response").Clone();

            if (root.GetProperty("status").GetString() != "ok")
            {
                var message = root.TryGetProperty("error", out var err)
                    ? err.GetStringOrEmpty("message")
                    : "Respuesta inválida del servidor.";
                throw new SubsonicException(message);
            }
            return root;
        }
        catch (SubsonicException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex)
        {
            throw new SubsonicException("No se pudo conectar con el servidor. Revisa la dirección y tu conexión.", ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or TaskCanceledException)
        {
            throw new SubsonicException("El servidor no respondió como un Navidrome/Subsonic válido.", ex);
        }
    }

    /// <summary>URL de streaming para una canción (se usa directamente en el reproductor).</summary>
    public string GetStreamUrl(string songId) => BuildUrl("stream", [("id", songId)]);

    /// <summary>URL de la carátula, redimensionada por el servidor al tamaño pedido (px).</summary>
    public string GetCoverArtUrl(string coverArtId, int size) =>
        BuildUrl("getCoverArt", [("id", coverArtId), ("size", size.ToString())]);

    /// <summary>Descarga la carátula en bruto (tamaño pequeño, para calcular el color dominante).</summary>
    public Task<Stream> OpenCoverArtAsync(string coverArtId, int size) =>
        _http.GetStreamAsync(GetCoverArtUrl(coverArtId, size));

    /// <summary>Construye la URL completa con los parámetros de autenticación estándar.</summary>
    private string BuildUrl(string endpoint, IEnumerable<(string Key, string Value)>? query = null)
    {
        // Sal aleatoria nueva por URL: el token resultante sigue siendo válido de forma indefinida.
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var token = Md5Hex(_credentials.Password + salt);

        var sb = new StringBuilder($"{_baseUrl}/rest/{endpoint}?");
        sb.Append("u=").Append(Uri.EscapeDataString(_credentials.UserName));
        sb.Append("&t=").Append(token);
        sb.Append("&s=").Append(salt);
        sb.Append("&v=").Append(ApiVersion);
        sb.Append("&c=").Append(ClientName);
        sb.Append("&f=json");

        if (query != null)
        {
            foreach (var (key, value) in query)
                sb.Append('&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }
        return sb.ToString();
    }

    // MD5 lo exige el protocolo Subsonic; no se usa para proteger nada por sí mismo.
    private static string Md5Hex(string text) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

/// <summary>Atajos para leer JSON sin lanzar excepciones cuando falta un campo.</summary>
internal static class JsonExtensions
{
    public static string GetStringOrEmpty(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public static int GetIntOrDefault(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    /// <summary>Devuelve los elementos de un arreglo hijo, o una secuencia vacía si no existe.</summary>
    public static IEnumerable<JsonElement> GetArray(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : Enumerable.Empty<JsonElement>();
}
