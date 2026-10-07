using NaviWalk.Helpers;

namespace NaviWalk.Views;

/// <summary>Control de carátula: imagen + ícono de nota como respaldo, con radio de esquina configurable.</summary>
public partial class CoverView : ContentView
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(ImageSource), typeof(CoverView),
        propertyChanged: (b, _, v) => ((CoverView)b).Picture.Source = (ImageSource?)v);

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(CoverView), 8d,
        propertyChanged: (b, _, v) => ((CoverView)b).ApplyCornerRadius((double)v));

    public CoverView()
    {
        InitializeComponent();
        Placeholder.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
            .ConvertFromInvariantString(Icons.MusicNote)!;
        ApplyCornerRadius(CornerRadius);

        SizeChanged += (_, _) =>
        {
            // En celdas de ancho flexible (grilla, pantalla de reproducción) el alto se iguala al ancho.
            if (IsSquare && Width > 0 && Math.Abs(HeightRequest - Width) > 0.5)
                HeightRequest = Width;

            // El ícono ocupa ~40 % del lado de la carátula, sea cual sea su tamaño.
            var side = Math.Min(Width, Height) * 0.4;
            if (side > 0) { Placeholder.WidthRequest = side; Placeholder.HeightRequest = side; }
        };
    }

    /// <summary>Si es true, el alto sigue al ancho (carátula cuadrada de tamaño fluido).</summary>
    public bool IsSquare { get; set; }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private void ApplyCornerRadius(double radius) =>
        Container.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(radius) };
}
