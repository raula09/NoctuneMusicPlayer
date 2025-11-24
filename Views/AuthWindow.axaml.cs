using Avalonia.Controls;

namespace MusicPlayerApp.Views;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
        ShowLogin();
    }

    void ShowLogin()
    {
        var login = new LoginView();
        login.LoginSucceeded += OnLoginSucceeded;
        login.NavigateToRegister += ShowRegister;
        AuthContent.Content = login;
    }

    void ShowRegister()
    {
        var reg = new RegisterView();
        reg.NavigateToLogin += ShowLogin;
        AuthContent.Content = reg;
    }

  void OnLoginSucceeded(string token)
{
    var main = new MainWindow(token);
    AuthContent.Content = main;
}

}
