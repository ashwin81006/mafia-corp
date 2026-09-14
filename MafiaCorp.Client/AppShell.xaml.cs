namespace MafiaCorp.Client
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(Views.RegisterPage), typeof(Views.RegisterPage));
            Routing.RegisterRoute(nameof(Views.LobbyPage), typeof(Views.LobbyPage));
            Routing.RegisterRoute(nameof(Views.GamePage), typeof(Views.GamePage));
        }
    }
}
