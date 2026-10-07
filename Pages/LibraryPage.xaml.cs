using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage(LibraryViewModel viewModel)
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
