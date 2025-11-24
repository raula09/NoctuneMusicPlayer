using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.Views;

public partial class LoginView : UserControl
{
    readonly AuthService _authService;
    public event Action<string>? LoginSucceeded;
    public event Action? NavigateToRegister;

    public LoginView()
    {
        InitializeComponent();
        _authService = new AuthService();
    }

    async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text;
        var password = PasswordTextBox.Text;
        var token = await _authService.LoginAsync(email, password);
        if (token == null)
        {
            await ShowMessageAsync("Login failed.");
            return;
        }
        LoginSucceeded?.Invoke(token);
    }

    void OnGoRegisterClick(object sender, RoutedEventArgs e)
    {
        NavigateToRegister?.Invoke();
    }

    async Task ShowMessageAsync(string message)
    {
        var window = VisualRoot as Window;
        if (window == null)
            return;

        var dialog = new Window
        {
            Width = 320,
            Height = 140,
            Title = "Info",
            Content = new TextBlock
            {
                Text = message,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };
        await dialog.ShowDialog(window);
    }
}