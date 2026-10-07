using System.Windows.Input;
using NaviWalk.Helpers;

namespace NaviWalk.Views;

/// <summary>
/// Fila de 5 estrellas. Muestra <see cref="Rating"/> (llenas en naranja) y al tocar una estrella
/// ejecuta <see cref="RateCommand"/> con su número; tocar la estrella ya activa quita la calificación (0).
/// </summary>
public sealed class RatingView : HorizontalStackLayout
{
    private const int StarCount = 5;

    public static readonly BindableProperty RatingProperty = BindableProperty.Create(
        nameof(Rating), typeof(int), typeof(RatingView), 0,
        propertyChanged: (b, _, __) => ((RatingView)b).Refresh());

    public static readonly BindableProperty RateCommandProperty = BindableProperty.Create(
        nameof(RateCommand), typeof(ICommand), typeof(RatingView));

    private readonly IconButton[] _stars = new IconButton[StarCount];

    public RatingView()
    {
        Spacing = 0;
        HorizontalOptions = LayoutOptions.Center;

        for (var i = 0; i < StarCount; i++)
        {
            var value = i + 1;   // Se captura por valor para el handler de cada estrella.
            _stars[i] = new IconButton
            {
                IconSize = 26,
                Padding = new Thickness(6),
                IconColor = (Color)Application.Current!.Resources["TextSecondary"],
                Command = new Command(() => RateCommand?.Execute(Rating == value ? 0 : value))
            };
            Add(_stars[i]);
        }
        Refresh();
    }

    public int Rating { get => (int)GetValue(RatingProperty); set => SetValue(RatingProperty, value); }
    public ICommand? RateCommand { get => (ICommand?)GetValue(RateCommandProperty); set => SetValue(RateCommandProperty, value); }

    /// <summary>Estrellas hasta la calificación: llenas y naranjas; el resto, contorno gris.</summary>
    private void Refresh()
    {
        for (var i = 0; i < StarCount; i++)
        {
            var filled = i < Rating;
            _stars[i].Data = filled ? Icons.Star : Icons.StarOutline;
            _stars[i].IsActive = filled;
        }
    }
}
