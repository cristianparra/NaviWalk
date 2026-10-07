using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

public partial class ArtistPage : ContentPage
{
    public ArtistPage(ArtistViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
