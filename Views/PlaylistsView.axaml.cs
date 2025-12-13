using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MusicPlayerApp.Views;

public partial class PlaylistsView : UserControl
{
    private readonly ObservableCollection<PlaylistDto> _playlists = new();
    private readonly OfflinePlaylistService _offline = new();

    private string? _selectedImage;

    public event EventHandler<string>? PlaylistSelected;
    public event EventHandler? BackToPlayerRequested;

    public PlaylistsView()
    {
        InitializeComponent();

        PlaylistsItemsControl = this.FindControl<ItemsControl>("PlaylistsItemsControl");
        CreatePlaylistButton = this.FindControl<Button>("CreatePlaylistButton");
        RefreshButton = this.FindControl<Button>("RefreshButton");
        BackToPlayerButton = this.FindControl<Button>("BackToPlayerButton");

        ForceCreateOverlay = this.FindControl<Border>("ForceCreateOverlay");
        ForceNameBox = this.FindControl<TextBox>("ForceNameBox");
        ForceDescBox = this.FindControl<TextBox>("ForceDescBox");
        ForceCreateButton = this.FindControl<Button>("ForceCreateButton");

        PlaylistsItemsControl.ItemsSource = _playlists;

        CreatePlaylistButton.Click += OnCreatePlaylistClick;
        RefreshButton.Click += OnRefreshClick;
        BackToPlayerButton.Click += (_, _) => BackToPlayerRequested?.Invoke(this, EventArgs.Empty);
        ForceCreateButton.Click += ForceCreateButton_Click;

        this.Loaded += (_, _) => _ = LoadPlaylistsAsync();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
 
    private Task LoadPlaylistsAsync()
    {
        _playlists.Clear();

        var list = _offline.GetPlaylists();
        foreach (var p in list)
            _playlists.Add(p);

        if (_playlists.Count == 0)
            ShowForcedCreateUI();

        return Task.CompletedTask;
    }

    private void ShowForcedCreateUI()
    {
        ForceCreateOverlay.IsVisible = true;
        ForceNameBox.Focus();
    }
 
    private async void ForceCreateButton_Click(object? sender, RoutedEventArgs e)
    {
        await CreatePlaylistInternal(ForceNameBox.Text, ForceDescBox.Text);
        ForceCreateOverlay.IsVisible = false;
    }
 
    private async void OnCreatePlaylistClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Width = 420,
            Height = 340,
            Title = "Create Playlist",
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var nameBox = new TextBox { Watermark = "Name", Margin = new Thickness(0, 0, 0, 12) };
        var descBox = new TextBox { Watermark = "Description", Margin = new Thickness(0, 0, 0, 12) };
        var imgBox = new TextBox { Watermark = "No image selected", IsReadOnly = true, Margin = new Thickness(0, 0, 0, 12) };

        var chooseImgBtn = new Button { Content = "Choose Image", Margin = new Thickness(0, 0, 0, 12) };

        chooseImgBtn.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(this);
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
                }
            });

            if (files != null && files.Count > 0)
            {
                _selectedImage = files[0].Path.LocalPath;
                imgBox.Text = _selectedImage;
            }
        };

        var createBtn = new Button { Content = "Create", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

        createBtn.Click += async (_, _) =>
        {
            await CreatePlaylistInternal(nameBox.Text, descBox.Text);
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children = { nameBox, descBox, imgBox, chooseImgBtn, createBtn }
        };

        await dialog.ShowDialog((Window)VisualRoot!);
    }
 
    private async Task CreatePlaylistInternal(string? name, string? desc)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var dto = new PlaylistDto
        {
            Name = name.Trim(),
            Description = desc?.Trim()
        };

        if (!string.IsNullOrWhiteSpace(_selectedImage))
            dto.ImagePath = _offline.SaveCoverImage(_selectedImage);

        _offline.SavePlaylist(dto);

        _selectedImage = null;

        await LoadPlaylistsAsync();
    }
 
    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
        => await LoadPlaylistsAsync();
 
    public void OnOpenPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
            PlaylistSelected?.Invoke(this, id);
    }
 
    public async void OnDeletePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            _offline.DeletePlaylist(id);
            await LoadPlaylistsAsync();
        }
    }
}
