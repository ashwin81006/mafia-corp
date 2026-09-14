using Microsoft.Extensions.Logging;

namespace MafiaCorp.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<Services.ApiService>();
            builder.Services.AddSingleton<Services.GameClientService>();
            
            builder.Services.AddTransient<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<Views.LoginPage>();
            builder.Services.AddTransient<ViewModels.RegisterViewModel>();
            builder.Services.AddTransient<Views.RegisterPage>();
            builder.Services.AddTransient<ViewModels.HomeViewModel>();
            builder.Services.AddTransient<Views.HomePage>();
            builder.Services.AddTransient<ViewModels.LobbyViewModel>();
            builder.Services.AddTransient<Views.LobbyPage>();
            builder.Services.AddTransient<ViewModels.GameViewModel>();
            builder.Services.AddTransient<Views.GamePage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
