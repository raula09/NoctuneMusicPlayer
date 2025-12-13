using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.Views;

public partial class RegisterView : UserControl
{
    readonly AuthService _authService;
    public event Action? NavigateToLogin;
    public event Action<string>? LoginSucceeded;
    private string? _registeredEmail;
    private string? _registeredPassword;

    public RegisterView()
    {
        InitializeComponent();
        _authService = new AuthService();
    }

    async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text;
        var password = PasswordTextBox.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await ShowMessageAsync("Please fill in all fields.");
            return;
        }

        var ok = await _authService.RegisterAsync(email, password);
        if (!ok)
        {
            await ShowMessageAsync("Registration failed. Email may already be in use.");
            return;
        }

        _registeredEmail = email;
        _registeredPassword = password;
         
        CredentialsPanel.IsVisible = false;
        VerificationPanel.IsVisible = true;
        VerifyButton.IsVisible = true;
        RegisterButton.IsVisible = false;
    }

    async void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        var code = VerificationCodeTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            await ShowMessageAsync("Please enter the verification code.");
            return;
        }

        var (success, message) = await _authService.VerifyAsync(code);
        if (!success)
        {
            await ShowMessageAsync($"Verification failed: {message}");
            return;
        }
 
        var token = await _authService.LoginAsync(_registeredEmail, _registeredPassword);
        if (token != null)
        {
            LoginSucceeded?.Invoke(token);
        }
        else
        {
            await ShowMessageAsync("Email verified! Please log in.");
            NavigateToLogin?.Invoke();
        }
    }

    void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigateToLogin?.Invoke();
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