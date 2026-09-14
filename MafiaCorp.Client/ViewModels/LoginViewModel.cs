using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MafiaCorp.Client.Services;
using System.Threading.Tasks;

namespace MafiaCorp.Client.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public LoginViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        var success = await _apiService.LoginAsync(Username, Password);
        
        IsBusy = false;

        if (success)
        {
            await Shell.Current.GoToAsync("//HomePage");
        }
        else
        {
            ErrorMessage = "Invalid credentials. Please try again.";
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.RegisterPage));
    }
}
