using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MafiaCorp.Client.Services;
using System.Threading.Tasks;

namespace MafiaCorp.Client.ViewModels;

public partial class RegisterViewModel : ObservableObject
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

    public RegisterViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        var success = await _apiService.RegisterAsync(Username, Password);
        
        IsBusy = false;

        if (success)
        {
            await Shell.Current.GoToAsync(".."); // Go back to login
        }
        else
        {
            ErrorMessage = "Registration failed. Username may already exist.";
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
