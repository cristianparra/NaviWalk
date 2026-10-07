using NaviWalk.ViewModels;

namespace NaviWalk.Pages;

public partial class ServerLoginPage : ContentPage
{
    public ServerLoginPage(ServerLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
