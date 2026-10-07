using Microsoft.Maui.Graphics;

namespace NaviWalk.Views;

/// <summary>
/// Barra de progreso "ondulada" (estilo Material 3 Expressive): la parte reproducida es una onda
/// que avanza mientras suena, seguida de un círculo y de la pista plana del tiempo restante.
/// Se toca o arrastra para hacer seek. Mantiene la misma API que un Slider
/// (<see cref="Value"/>, <see cref="Maximum"/>, <see cref="DragStarted"/>, <see cref="DragCompleted"/>)
/// para poder reemplazarlo sin cambiar la lógica de la página.
/// </summary>
public sealed class WavySeekBar : GraphicsView
{
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(
        nameof(Maximum), typeof(double), typeof(WavySeekBar), 1d,
        propertyChanged: (b, _, __) => ((WavySeekBar)b).Invalidate());

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(WavySeekBar), 0d,
        propertyChanged: (b, _, __) => ((WavySeekBar)b).Invalidate());

    /// <summary>Si es true la onda se desplaza (usar mientras suena la música).</summary>
    public static readonly BindableProperty IsAnimatingProperty = BindableProperty.Create(
        nameof(IsAnimating), typeof(bool), typeof(WavySeekBar), false,
        propertyChanged: (b, _, __) => ((WavySeekBar)b).UpdateAnimation());

    /// <summary>Se dispara al poner el dedo sobre la barra.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Se dispara al soltar; <see cref="Value"/> ya contiene la posición elegida.</summary>
    public event EventHandler? DragCompleted;

    private readonly WavyDrawable _drawable;
    private IDispatcherTimer? _timer;
    private bool _isDragging;
    private double _dragValue;
    private float _phase;

    public WavySeekBar()
    {
        _drawable = new WavyDrawable(this);
        Drawable = _drawable;

        StartInteraction += (_, e) => { _isDragging = true; MoveTo(e); DragStarted?.Invoke(this, EventArgs.Empty); };
        DragInteraction += (_, e) => MoveTo(e);
        EndInteraction += (_, e) =>
        {
            MoveTo(e);
            _isDragging = false;
            Value = _dragValue;   // La barra se queda donde se soltó hasta el siguiente avance real.
            DragCompleted?.Invoke(this, EventArgs.Empty);
        };
        CancelInteraction += (_, _) => { _isDragging = false; Invalidate(); };

        // El timer solo corre con la vista en pantalla (ahorra batería).
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => _timer?.Stop();
    }

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsAnimating { get => (bool)GetValue(IsAnimatingProperty); set => SetValue(IsAnimatingProperty, value); }

    /// <summary>Valor mostrado: el del dedo mientras se arrastra, o el real de reproducción.</summary>
    private double DisplayValue => _isDragging ? _dragValue : Value;

    /// <summary>Convierte la posición táctil (px) en un valor entre 0 y Maximum.</summary>
    private void MoveTo(TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        var (left, right) = WavyDrawable.Bounds(Width);
        var fraction = Math.Clamp((e.Touches[0].X - left) / (right - left), 0, 1);
        _dragValue = fraction * Maximum;
        Invalidate();
    }

    private void UpdateAnimation()
    {
        if (!IsAnimating || !IsLoaded)
        {
            _timer?.Stop();
            return;
        }

        if (_timer is null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);   // ~30 fps
            _timer.Tick += (_, _) =>
            {
                _phase = (_phase + 0.18f) % (MathF.PI * 2);
                Invalidate();
            };
        }
        _timer.Start();
    }

    /// <summary>Dibuja la onda, el círculo y la pista restante.</summary>
    private sealed class WavyDrawable(WavySeekBar owner) : IDrawable
    {
        private const float StrokeSize = 5f;
        private const float ThumbRadius = 9f;
        private const float Amplitude = 3.5f;
        private const float WaveLength = 30f;
        private const float EaseLength = 24f;   // La onda se aplana al acercarse al círculo.
        private const float Margin = ThumbRadius + 1;

        /// <summary>Límites horizontales útiles (dejan espacio para que el círculo no se corte).</summary>
        public static (double Left, double Right) Bounds(double width) => (Margin, Math.Max(Margin + 1, width - Margin));

        public void Draw(ICanvas canvas, RectF rect)
        {
            var (left, right) = Bounds(rect.Width);
            var centerY = rect.Height / 2f;
            var fraction = owner.Maximum > 0 ? Math.Clamp(owner.DisplayValue / owner.Maximum, 0, 1) : 0;
            var thumbX = (float)(left + (right - left) * fraction);

            var accent = ThemeColor("Accent");
            var trackColor = Colors.White.WithAlpha(0.25f);

            canvas.StrokeSize = StrokeSize;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            // Pista restante (plana), con un punto al final como en el diseño de referencia.
            var trackStart = thumbX + ThumbRadius + 4;
            if (trackStart < right)
            {
                canvas.StrokeColor = trackColor;
                canvas.DrawLine(trackStart, centerY, (float)right, centerY);
                canvas.FillColor = accent;
                canvas.FillCircle((float)right, centerY, 2.5f);
            }

            // Parte reproducida: onda cuya fase avanza con la animación.
            if (thumbX > left)
            {
                var path = new PathF();
                var first = true;
                for (var x = (float)left; x <= thumbX; x += 2f)
                {
                    var ease = Math.Min(1f, (thumbX - x) / EaseLength);   // 1 lejos del círculo, 0 pegado a él
                    var y = centerY + Amplitude * ease * MathF.Sin((x - (float)left) / WaveLength * MathF.PI * 2 - owner._phase);
                    if (first) { path.MoveTo(x, y); first = false; }
                    else path.LineTo(x, y);
                }
                path.LineTo(thumbX, centerY);

                canvas.StrokeColor = accent;
                canvas.DrawPath(path);
            }

            // Círculo (posición actual)
            canvas.FillColor = accent;
            canvas.FillCircle(thumbX, centerY, ThumbRadius);
        }

        private static Color ThemeColor(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c ? c : Colors.Orange;
    }
}
