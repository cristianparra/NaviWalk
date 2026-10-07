using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>Carga al mostrarse por primera vez (o tras cambiar de fuente); luego, pull-to-refresh.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadIfNeeded();
    }
}
