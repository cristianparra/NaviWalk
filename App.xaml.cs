namespace NaviWalk;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Tema oscuro fijo: el diseño (gris oscuro + naranja) no tiene variante clara.
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell());
}
