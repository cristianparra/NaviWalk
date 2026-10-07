using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace NaviWalk.Views;

/// <summary>
/// Botón táctil que dibuja un ícono vectorial (ver <c>Icons</c>).
/// <see cref="IsActive"/> lo pinta de naranja (ej. shuffle activado).
/// </summary>
public partial class IconButton : ContentView
{
    public static readonly BindableProperty DataProperty = BindableProperty.Create(
        nameof(Data), typeof(string), typeof(IconButton),
        propertyChanged: (b, _, __) => ((IconButton)b).UpdateGeometry());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(IconButton));

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize), typeof(double), typeof(IconButton), 24d,
        propertyChanged: (b, _, __) => ((IconButton)b).UpdateSize());

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(IconButton), null,
        propertyChanged: (b, _, __) => ((IconButton)b).UpdateColor());

    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive), typeof(bool), typeof(IconButton), false,
        propertyChanged: (b, _, __) => ((IconButton)b).UpdateColor());

    public IconButton()
    {
        InitializeComponent();
        UpdateSize();
        UpdateColor();
    }

    /// <summary>Datos de trazo SVG del ícono (usar constantes de <c>Icons</c>).</summary>
    public string? Data { get => (string?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public Color? IconColor { get => (Color?)GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    private void UpdateGeometry()
    {
        Glyph.Data = string.IsNullOrEmpty(Data)
            ? null
            : (Geometry)new PathGeometryConverter().ConvertFromInvariantString(Data)!;
    }

    private void UpdateSize()
    {
        Glyph.WidthRequest = IconSize;
        Glyph.HeightRequest = IconSize;
    }

    /// <summary>Naranja si está activo; si no, el color indicado; si no, el texto principal del tema.</summary>
    private void UpdateColor()
    {
        Color color;
        if (IsActive) color = ThemeColor("Accent");
        else color = IconColor ?? ThemeColor("TextPrimary");
        Glyph.Fill = new SolidColorBrush(color);
    }

    private static Color ThemeColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c ? c : Colors.White;

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
}
