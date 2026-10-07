using Android.App;
using Android.Content.PM;
using Android.OS;

namespace NaviWalk;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Barras del sistema del mismo gris que el fondo de la app.
#pragma warning disable CA1422 // Obsoleto desde Android 15, pero necesario en versiones anteriores.
        Window?.SetStatusBarColor(global::Android.Graphics.Color.ParseColor("#121212"));
        Window?.SetNavigationBarColor(global::Android.Graphics.Color.ParseColor("#121212"));
#pragma warning restore CA1422
    }
}
