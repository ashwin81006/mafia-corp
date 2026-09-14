using System.Collections.Specialized;
using System.Threading.Tasks;
using MafiaCorp.Client.ViewModels;

namespace MafiaCorp.Client.Views;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;

    public GamePage(GameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.ChatLog.CollectionChanged += OnChatLogChanged;
    }

    private void OnChatLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.ChatLog.Count > 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(50);
                try
                {
                    ChatCollectionView.ScrollTo(_viewModel.ChatLog.Count - 1, position: ScrollToPosition.End, animate: false);
                }
                catch { }
            });
        }
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _viewModel.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ChatLog.CollectionChanged -= OnChatLogChanged;
        _viewModel.Dispose();
    }
}
