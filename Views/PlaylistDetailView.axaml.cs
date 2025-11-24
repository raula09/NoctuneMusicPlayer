using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicPlayerApp.Views;

public partial class PlaylistDetailView : UserControl
{
    private readonly OfflinePlaylistService _offline = new();
private bool LastPointerWasRightClick = false;

    private readonly ObservableCollection<Track> _tracks = new();
    private PlaylistDto? _playlist;
    private string _playlistId = "";

    public event EventHandler? BackRequested;
    public event EventHandler<List<Track>>? PlayAllRequested;

    public PlaylistDetailView(string playlistId)
    {
        _playlistId = playlistId;

        InitializeComponent();
        LoadControls();
TracksListBox.PointerPressed += (s, e) =>
{
    if (e.GetCurrentPoint(TracksListBox).Properties.IsRightButtonPressed)
    {
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item != null)
        {
            TracksListBox.SelectedItem = item.DataContext;
        }
    }
};

        TracksListBox.ItemsSource = _tracks;

        BackButton.Click += OnBackClick;
        PlayAllButton.Click += OnPlayAllClick;
        AddTracksButton.Click += OnAddTracksClick;
        EditButton.Click += OnEditClick;

        PlayTrackMenuItem.Click += OnPlayTrackClick;
        RemoveTrackMenuItem.Click += OnRemoveTrackClick;

        Loaded += async (_, _) => await LoadPlaylistAsync();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void LoadControls()
    {
        TracksListBox = this.FindControl<ListBox>("TracksListBox");
        BackButton = this.FindControl<Button>("BackButton");
        PlayAllButton = this.FindControl<Button>("PlayAllButton");
        AddTracksButton = this.FindControl<Button>("AddTracksButton");
        EditButton = this.FindControl<Button>("EditButton");

        PlayTrackMenuItem = this.FindControl<MenuItem>("PlayTrackMenuItem");
        RemoveTrackMenuItem = this.FindControl<MenuItem>("RemoveTrackMenuItem");

        PlaylistNameText = this.FindControl<TextBlock>("PlaylistNameText");
        PlaylistDescText = this.FindControl<TextBlock>("PlaylistDescText");
        PlaylistInfoText = this.FindControl<TextBlock>("PlaylistInfoText");
    }

    // ---------------------------------
    // LOAD PLAYLIST (Offline)
    // ---------------------------------
    private Task LoadPlaylistAsync()
    {
        _playlist = _offline.GetPlaylists().FirstOrDefault(x => x.Id == _playlistId);
        if (_playlist == null)
            return Task.CompletedTask;

        PlaylistNameText.Text = _playlist.Name;
        PlaylistDescText.Text = _playlist.Description ?? "";
        PlaylistInfoText.Text = $"{_playlist.TrackCount} tracks";

        _tracks.Clear();
        int idx = 1;

        foreach (var path in _playlist.TrackPaths)
        {
            if (File.Exists(path))
                _tracks.Add(new Track(path) { Index = idx++ });
        }

        return Task.CompletedTask;
    }

    // ---------------------------------
    // BUTTON: Back
    // ---------------------------------
    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------
    // BUTTON: Play All
    // ---------------------------------
    private void OnPlayAllClick(object? sender, RoutedEventArgs e)
    {
        PlayAllRequested?.Invoke(this, _tracks.ToList());
    }

    // ---------------------------------
    // BUTTON: Add Tracks
    // ---------------------------------
    private async void OnAddTracksClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio") { Patterns = new[] { "*.mp3", "*.wav", "*.flac" } }
            }
        });

        if (files == null || files.Count == 0)
            return;

        foreach (var f in files)
            _offline.AddTrack(_playlistId, f.Path.LocalPath);

        await LoadPlaylistAsync();
    }

    // ---------------------------------
    // MENU: Play selected track
    // ---------------------------------
    private void OnPlayTrackClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
            PlayAllRequested?.Invoke(this, new List<Track> { track });
    }

    // ---------------------------------
    // MENU: Remove selected track
    // ---------------------------------
    private async void OnRemoveTrackClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
        {
            _offline.RemoveTrack(_playlistId, track.Path);
            await LoadPlaylistAsync();
        }
    }

    public async void OnRemoveButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _offline.RemoveTrack(_playlistId, path);
            await LoadPlaylistAsync();
        }
    }

    // ---------------------------------
    // BUTTON: Edit Playlist (Name + Desc)
    // ---------------------------------
    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (_playlist == null)
            return;

        var dialog = new Window
        {
            Width = 400,
            Height = 260,
            Title = "Edit Playlist"
        };

        var nameBox = new TextBox
        {
            Text = _playlist.Name,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var descBox = new TextBox
        {
            Text = _playlist.Description,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var saveBtn = new Button
        {
            Content = "Save",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        saveBtn.Click += (_, _) =>
        {
            _playlist.Name = nameBox.Text ?? "";
            _playlist.Description = descBox.Text;

            _offline.SavePlaylist(_playlist);
            dialog.Close();
            _ = LoadPlaylistAsync();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Children = { nameBox, descBox, saveBtn }
        };

        await dialog.ShowDialog((Window)VisualRoot!);
    }
}
