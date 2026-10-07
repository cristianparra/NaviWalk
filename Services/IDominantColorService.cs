namespace NaviWalk.Services;

/// <summary>
/// Calcula el color predominante de una imagen. La pantalla "Reproduciendo" lo usa para
/// teñir el fondo con el color de la carátula. La implementación vive en Platforms/Android.
/// </summary>
public interface IDominantColorService
{
    /// <summary>Devuelve el color medio de la imagen, o null si no se pudo decodificar.</summary>
    Color? GetDominantColor(Stream image);
}
