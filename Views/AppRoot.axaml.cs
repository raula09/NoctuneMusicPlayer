using Avalonia.Controls;
using MusicPlayerApp.Views;
using MusicPlayerApp.Services;
using Avalonia.Layout;
using MusicPlayerApp.Models;
namespace MusicPlayerApp;

public partial class AppRoot : Window
{
    private string? _token;
    
    public AppRoot()
    {
        InitializeComponent();
        
        var settings = SettingsService.Load();
        if (settings != null)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }
        
       // TEMPORARY DISABLED
SetContent(new MainWindow("DEV-MODE"));
return;

    }
    
    private void SetContent(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        control.VerticalAlignment = VerticalAlignment.Stretch;
        Content = control;
    }
    
    public void ShowLogin()
    {
        var login = new LoginView();
        login.LoginSucceeded += OnLoginSucceeded;
        login.NavigateToRegister += ShowRegister;
        SetContent(login);
    }
    
    void ShowRegister()
    {
        var register = new RegisterView();
        register.NavigateToLogin += ShowLogin;
        register.LoginSucceeded += OnLoginSucceeded;
        SetContent(register);
    }
    
    void OnLoginSucceeded(string token)
    {
        _token = token;
        SettingsService.SaveToken(token);
        SetContent(new MainWindow(_token));
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Content is MainWindow mw)
        {
            mw.SaveStateBeforeExit();
        }
        
        var settings = SettingsService.Load() ?? new AppSettings();
        settings.WindowWidth = (int)Width;
        settings.WindowHeight = (int)Height;
        SettingsService.Save(settings);
        
        base.OnClosing(e);
    }
}