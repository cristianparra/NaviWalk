using Android.Graphics;
using NaviWalk.Services;

namespace NaviWalk.AndroidImpl;

/// <summary>Color medio de la imagen: se reduce el bitmap a 1x1 píxel y se lee ese píxel.</summary>
public sealed class AndroidDominantColorService : IDominantColorService
{
    public Microsoft.Maui.Graphics.Color? GetDominantColor(Stream image)
    {
        try
        {
            using var bitmap = BitmapFactory.DecodeStream(image);
            if (bitmap is null) return null;

            // "filter: true" promedia los píxeles al reducir, en vez de tomar uno solo.
            using var onePixel = Bitmap.CreateScaledBitmap(bitmap, 1, 1, true);
            var pixel = onePixel!.GetPixel(0, 0);
            return Microsoft.Maui.Graphics.Color.FromRgb(Android.Graphics.Color.GetRedComponent(pixel), Android.Graphics.Color.GetGreenComponent(pixel), Android.Graphics.Color.GetBlueComponent(pixel));
        }
        catch (Exception)
        {
            return null;   // La pantalla usa un color por defecto.
        }
    }
}
