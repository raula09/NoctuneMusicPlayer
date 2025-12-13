using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    private readonly ObservableCollection<Track> _tracks = new();
    private PlaylistDto? _playlist;
    private string _playlistId = "";

    public event EventHandler? BackRequested;
    public event EventHandler<List<Track>>? PlayAllRequested;
    public event EventHandler<Track>? AddToQueueRequested;
    public event EventHandler<Track>? PlayNextRequested;

    public PlaylistDetailView(string playlistId)
    {
        _playlistId = playlistId;

        InitializeComponent();
        LoadControls();
 
        TracksListBox.AddHandler(PointerPressedEvent, OnTracksListBoxPointerPressed, RoutingStrategies.Tunnel);

        TracksListBox.ItemsSource = _tracks;

        BackButton.Click += OnBackClick;
        PlayAllButton.Click += OnPlayAllClick;
        AddTracksButton.Click += OnAddTracksClick;
        EditButton.Click += OnEditClick;

        PlayTrackMenuItem.Click += OnPlayTrackClick;
        RemoveTrackMenuItem.Click += OnRemoveTrackClick;
        AddToQueueMenuItem.Click += OnAddToQueueClick;
        PlayNextMenuItem.Click += OnPlayNextClick;

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
        AddToQueueMenuItem = this.FindControl<MenuItem>("AddToQueueMenuItem");
        PlayNextMenuItem = this.FindControl<MenuItem>("PlayNextMenuItem");

        PlaylistNameText = this.FindControl<TextBlock>("PlaylistNameText");
        PlaylistDescText = this.FindControl<TextBlock>("PlaylistDescText");
        PlaylistInfoText = this.FindControl<TextBlock>("PlaylistInfoText");
    }
 
    private void OnTracksListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(TracksListBox);
        
        if (point.Properties.IsRightButtonPressed)
        { 
            var visual = e.Source as Visual;
            var listBoxItem = visual?.FindAncestorOfType<ListBoxItem>();
            
            if (listBoxItem?.DataContext is Track track)
            { TracksListBox.SelectedItem = track;
                e.Handled = true;
            }
        }
    }
 
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
 
    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
 
    private void OnPlayAllClick(object? sender, RoutedEventArgs e)
    {
        PlayAllRequested?.Invoke(this, _tracks.ToList());
    }
 
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
 
    private void OnPlayTrackClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
            PlayAllRequested?.Invoke(this, new List<Track> { track });
    }
 
    private async void OnRemoveTrackClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
        {
            _offline.RemoveTrack(_playlistId, track.Path);
            await LoadPlaylistAsync();
        }
    }
 
    private void OnAddToQueueClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
        {
            AddToQueueRequested?.Invoke(this, track);
        }
    }
 
    private void OnPlayNextClick(object? sender, RoutedEventArgs e)
    {
        if (TracksListBox.SelectedItem is Track track)
        {
            PlayNextRequested?.Invoke(this, track);
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
