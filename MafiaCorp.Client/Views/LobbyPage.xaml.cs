using MafiaCorp.Client.ViewModels;

namespace MafiaCorp.Client.Views;

public partial class LobbyPage : ContentPage
{
    private readonly LobbyViewModel _viewModel;
    
    public LobbyPage(LobbyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
