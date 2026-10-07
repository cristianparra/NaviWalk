using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

public partial class CollectionPage : ContentPage
{
    public CollectionPage(CollectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
