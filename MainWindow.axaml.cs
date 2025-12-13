using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Security.Claims;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Svg.Skia;
using LibVLCSharp.Shared;
using MusicPlayerApp.Audio;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using MusicPlayerApp.Views;
using System;
using MusicPlayerApp.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using TrackModel = MusicPlayerApp.Models.Track;
using LiteDB;
using System.Text.RegularExpressions;
namespace MusicPlayerApp;

public class QueueEntry
{
    public TrackModel Track { get; }
    public bool IsCurrent { get; }

    public QueueEntry(TrackModel track, bool isCurrent)
    {
        Track = track;
        IsCurrent = isCurrent;
    }
}

public partial class MainWindow : UserControl
{
    private class PlaylistApiItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }

        public override string ToString()
        {
            return Name ?? base.ToString();
        }
    }
    public class TrackDto
    {
        public string Path { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public DateTime DateAdded { get; set; }
    }


    const int VisualBarCount = 120;
    private double _currentWidth;
    private double _currentHeight;
    private const double SMALL_SCREEN = 900;
    private const double MEDIUM_SCREEN = 1200;
    private const double LARGE_SCREEN = 1400;
    readonly object _audioLock = new();
    private TrackModel? _lastFullscreenTrack;
    private ObservableCollection<TrackModel> _playlist = new();
    private ObservableCollection<TrackModel> _viewTracks = new();
    private ObservableCollection<QueueEntry> _queuePreview = new();
    private LyricsDisplay? _lyricsDisplay;
    private TrackModel? _currentTrack;
    double _seekMax = 1;
    bool _seeking = false;

    
    public ObservableCollection<TrackModel> Tracks => _viewTracks;

 
    
    FullscreenPlayer? _fullscreen;

    PipeWireCapture? _capture;
    VisualizerService _visualizer = new();
    private bool _isTogglingPlay = false;
    private bool _isPaused = false;
    private bool _isLoadingLikedSongs = false;

    AppSettings _settings = new();
    LibVLC _libVLC;
    MediaPlayer _mp;
    int _index = -1;
    bool _shuffle = false;
    bool _loop = false;
    bool _restoredLastPosition = false;
    bool _albumsViewOpen = false;
    Random _rand = new();
    private int _targetVolume = 50;
    private bool _isCrossfading = false;
    List<int> _staticShuffleQueue = new();
    Stack<int> _history = new();
    DateTime _currentTrackStartTime;
    long _lastKnownPosition = 0;

    private object? _playerContent;

    DispatcherTimer? _visualizerTimer;
    DispatcherTimer? _positionUpdateTimer;
    bool _suppressSelectionPlay = false;

    private Dictionary<TrackModel, int> _playHistory = new Dictionary<TrackModel, int>();
    private List<ListeningSession> _listeningSessions = new List<ListeningSession>();
    private DateTime _accountCreated;
    private string _userEmail = "user@example.com";
    private readonly string? _token;

    DispatcherTimer waveTimer;

   
public MainWindow(string? token)
{
    _token = token;
    InitializeComponent();
    Core.Initialize();
    WireForcedPlaylistUI();
    _ = SyncService.SyncAsync(_token);
    InitWaveAnimation();
    InitializeLyrics();
    OpenPlaylistsButton = this.FindControl<Button>("OpenPlaylistsButton");
    if (OpenPlaylistsButton != null)
        OpenPlaylistsButton.Click += OnPlaylistsButtonClick;

    _playHistory = StatsService.LoadPlayHistory() ?? new Dictionary<TrackModel, int>();
    _listeningSessions = StatsService.LoadListeningSessions() ?? new List<ListeningSession>();
    _accountCreated = StatsService.LoadAccountCreatedDate();
   
    LikedSongsButton = this.FindControl<Button>("LikedSongsButton");
    LikedSongsCount = this.FindControl<TextBlock>("LikedSongsCount");

    if (LikedSongsButton != null)
    {
        LikedSongsButton.Click += OnLikedSongsClick;
    }
    DataContext = this;

    UpdateLikedSongsCount();
    
    if (!string.IsNullOrEmpty(token))
    {
        _userEmail = GetEmailFromToken(token);
        StatsService.SaveUserData(_userEmail, _accountCreated);
    }
    else
    {
        var stored = StatsService.LoadUserEmail();
        if (!string.IsNullOrEmpty(stored))
            _userEmail = stored;
    }

    AttachedToVisualTree += (_, _) => Focus();
    KeyDown += MainWindow_KeyDown;
    var lyricsToggleButton = this.FindControl<Button>("LyricsToggleButton");
    if (lyricsToggleButton != null)
    {
        lyricsToggleButton.Click += OnLyricsToggleClick;
    }

    MiniPrev.Click += (_, _) => Prev(null, new RoutedEventArgs());
    MiniNext.Click += (_, _) => Next(null, new RoutedEventArgs());
    MiniPlayPause.Click += (_, _) => PlayPause(null, new RoutedEventArgs());
    MiniPlayButton.Click += (_, _) => PlayPause(null, new RoutedEventArgs());

    FullscreenButton.Click += (_, _) => ShowFullscreen();

    _libVLC = new LibVLC("--aout=alsa");
    _mp = new MediaPlayer(_libVLC);

    _capture = new PipeWireCapture(
        "alsa_output.pci-0000_00_1f.3.analog-stereo.monitor",
        OnPipeWireSamples);
    _capture.Start();

    PlaylistBox.ItemsSource = _viewTracks;
    QueueItemsControl.ItemsSource = _queuePreview;

    _settings = SettingsService.Load() ?? new AppSettings();
    Width = _settings.WindowWidth;
    Height = _settings.WindowHeight;
    VolumeSlider.Value = _settings.Volume;

    var savedTracks = LoadAllTracks();
    var loadedPaths = new HashSet<string>();

    foreach (var track in savedTracks)
    {
        if (File.Exists(track.Path))
        {
            _playlist.Add(track);
            loadedPaths.Add(track.Path);
        }
    }

    foreach (var p in _settings.Playlist)
    {
        if (File.Exists(p) && !loadedPaths.Contains(p))
        {
            var newTrack = new TrackModel(p);
            _playlist.Add(newTrack);
            SaveTrack(newTrack);
        }
    }
    Console.WriteLine($"✓ Total tracks in playlist from cache: {_playlist.Count}");

    RebuildView();
    UpdateQueuePreview();

    LoadLargestOfflinePlaylist();

    ShuffleButton.Click += ShuffleButton_Click;
    LoopButton.Click += LoopButton_Click;

    _mp.EndReached += MediaPlayer_EndReached;
    _mp.LengthChanged += MediaPlayer_LengthChanged;

    PlaylistBox.SelectionChanged += PlaylistBox_SelectionChanged;

    PlayContextMenuItem.Click += PlayContextMenuItem_Click;
    RemoveContextMenuItem.Click += RemoveContextMenuItem_Click;
    OpenFolderContextMenuItem.Click += OpenFolderContextMenuItem_Click;
    AddToPlaylistMenuItem.Click += AddToPlaylist;
    UploadLyricsMenuItem.Click += UploadLyrics_Click;
    PlayPauseButton.Click += PlayPause;
    PrevButton.Click += Prev;
    NextButton.Click += Next;

    AddToQueueContextMenuItem.Click += AddToQueueContextMenuItem_Click;
    PlayNextContextMenuItem.Click += PlayNextContextMenuItem_Click;

    VolumeSlider.ValueChanged += VolumeChanged;

    PlaylistBox.AddHandler(PointerPressedEvent, PlaylistBox_PointerPressed, RoutingStrategies.Tunnel);

    SearchBox.PropertyChanged += SearchBox_PropertyChanged;
    SortBox.SelectionChanged += SortBox_SelectionChanged;

    AlbumArt.DoubleTapped += AlbumArt_DoubleTapped;

    InitVisualizer();
    InitPositionTimer();
     
    InitializeResponsive();
}

private void InitializeResponsive()
{ 
    
    this.PropertyChanged += OnResponsivePropertyChanged;
    
     
    var hamburger = this.FindControl<Button>("HamburgerButton");
    if (hamburger != null)
    {
        hamburger.Click += OnHamburgerClick;
    }
 
    var mobileLyrics = this.FindControl<Button>("MobileLyricsButton");
    if (mobileLyrics != null)
    {
        mobileLyrics.Click += OnMobileLyricsClick;
    }
     
    Dispatcher.UIThread.Post(() => UpdateResponsiveLayout(), DispatcherPriority.Loaded);
}
    private void InitializeLyrics()
    {
        _lyricsDisplay = this.FindControl<LyricsDisplay>("LyricsDisplay");
    
        if (_lyricsDisplay != null)
        {
            
            _lyricsDisplay.LyricsLoaded += (sender, lyricsContent) =>
            {
                Console.WriteLine("✓ Lyrics loaded and saved to LyricsService");
            };
        
            _lyricsDisplay.SeekRequested += (sender, seekTime) =>
            {
                if (_mp != null && _mp.Media != null && _mp.IsSeekable)
                {
                    long milliseconds = (long)seekTime.TotalMilliseconds;
                    _mp.Time = milliseconds;
                    Console.WriteLine($"Seeking to: {seekTime:mm\\:ss}");
                }
            };
        }
    }

    
  
    

    private string GetEmailFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == JwtRegisteredClaimNames.Email);
            return emailClaim?.Value ?? "user@example.com";
        }
        catch
        {
            return "user@example.com";
        }
    }
    
    private void OnLikedSongsClick(object? sender, RoutedEventArgs e)
    {
        LoadLikedSongsPlaylist();
    }
private void LoadLargestOfflinePlaylist()
{
    try
    {
        Console.WriteLine("🔍 Looking for offline playlists...");
        
        var offlineService = new OfflinePlaylistService();
        var playlists = offlineService.GetPlaylists();
        
        if (playlists == null || playlists.Count == 0)
        {
            Console.WriteLine("⚠️ No offline playlists found");
            return;
        }

        Console.WriteLine($"✓ Found {playlists.Count} offline playlists");

        PlaylistDto? largestPlaylist = null;
        int maxTrackCount = 0;

        foreach (var playlist in playlists)
        {
            int trackCount = playlist.TrackPaths?.Count ?? 0;
            Console.WriteLine($"  - {playlist.Name}: {trackCount} tracks");
            
            if (trackCount > maxTrackCount)
            {
                maxTrackCount = trackCount;
                largestPlaylist = playlist;
            }
        }

        if (largestPlaylist == null || maxTrackCount == 0)
        {
            Console.WriteLine("⚠️ No playlist found with tracks");
            return;
        }

        Console.WriteLine($"\n✓ Loading largest playlist: '{largestPlaylist.Name}' with {maxTrackCount} tracks");
 
        _playlist.Clear();

        int loadedCount = 0;
        int skippedCount = 0;
        int index = 1;
 
        foreach (var path in largestPlaylist.TrackPaths)
        {
            if (string.IsNullOrEmpty(path))
            {
                skippedCount++;
                continue;
            }

            if (File.Exists(path))
            {
                var track = new TrackModel(path);
                track.LoadMetadata();
                track.Index = index++;
                
                _playlist.Add(track);
                loadedCount++;
            }
            else
            {
                Console.WriteLine($"⚠️ Track not found on disk: {path}");
                skippedCount++;
            }
        }

        Console.WriteLine($"✓ Loaded {loadedCount} tracks from playlist '{largestPlaylist.Name}'");
        
        if (skippedCount > 0)
        {
            Console.WriteLine($"⚠️ Skipped {skippedCount} tracks (not found on disk)");
        }
 
        RebuildView();

        if (_playlist.Count > 0)
        {
            _index = 0;
            PlayIndex();
            Console.WriteLine("✓ Started playing first track");
        }
        else
        {
            Console.WriteLine("⚠️ No tracks available to play");
            UpdateQueuePreview();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error loading largest offline playlist: {ex.Message}");
        Console.WriteLine($"   Stack trace: {ex.StackTrace}");
    }
}
private void AddToQueueContextMenuItem_Click(object? sender, RoutedEventArgs e)
{
    if (PlaylistBox.SelectedItem is not TrackModel selectedTrack)
        return;
 
    if (!_playlist.Contains(selectedTrack))
    { 
        _playlist.Add(selectedTrack);
        SaveTrack(selectedTrack);
        
        Console.WriteLine($"✓ Added '{selectedTrack.Title}' to queue");
        
        RebuildView();
        UpdateQueuePreview();
    }
    else
    {
        Console.WriteLine($"⚠️ '{selectedTrack.Title}' is already in queue");
    }
}

private void PlayNextContextMenuItem_Click(object? sender, RoutedEventArgs e)
{
    if (PlaylistBox.SelectedItem is not TrackModel selectedTrack)
        return;

  
    var actualTrack = _playlist.FirstOrDefault(t => t.Path == selectedTrack.Path);
    
    if (actualTrack == null)
    { 
        actualTrack = selectedTrack;
        _playlist.Add(actualTrack);
        SaveTrack(actualTrack);
    }
 
    int existingIndex = _playlist.IndexOf(actualTrack);
    if (existingIndex >= 0 && existingIndex != _index)
    {
        _playlist.RemoveAt(existingIndex);
        if (existingIndex < _index)
            _index--; 
    }
 
    int insertPosition = _index + 1;
    if (insertPosition >= _playlist.Count)
    {
        _playlist.Add(actualTrack);
    }
    else
    {
        _playlist.Insert(insertPosition, actualTrack);
    }

    Console.WriteLine($"✓ '{actualTrack.Title}' will play next");

    RebuildView();
    RebuildShuffleQueue();
    UpdateQueuePreview();
}

    private void UpdateLikedSongsCount()
    {
        if (LikedSongsCount != null)
        {
            int count = LikesService.GetLikedTracksCount();
            LikedSongsCount.Text = $"{count} song{(count != 1 ? "s" : "")}";
        }
    }

    private void OnTrackLikeClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("\n=== LIKE BUTTON CLICKED ===");
    
        if (sender is Button button && button.DataContext is TrackModel track)
        {
            Console.WriteLine($"Track: {track.Title}");
            Console.WriteLine($"Path: {track.Path}");
            Console.WriteLine($"IsLiked BEFORE: {track.IsLiked}");
        
            track.ToggleLike();
        
            Console.WriteLine($"IsLiked AFTER: {track.IsLiked}");
         
            UpdateLikedSongsCount();
        
            Console.WriteLine($"{(track.IsLiked ? "♥" : "♡")} {track.Title}");
            Console.WriteLine("=== LIKE BUTTON DONE ===\n");
        }
    }
    private void OnLyricsToggleClick(object? sender, RoutedEventArgs e)
    {
        var lyricsPanel = this.FindControl<Border>("LyricsPanelContainer");
        var mainContent = this.FindControl<Grid>("MainContentGrid");
    
        if (lyricsPanel != null)
        {
            if (lyricsPanel.IsVisible)
            {
                lyricsPanel.IsVisible = false;
            
                if (mainContent != null)
                {
                    mainContent.ColumnDefinitions[2].Width = new GridLength(0);
                }
            }
            else
            {
                lyricsPanel.IsVisible = true;
            
                if (mainContent != null)
                {
                    mainContent.ColumnDefinitions[2].Width = new GridLength(350);
                }
            }
        }
    }
    
 
public void AddTrackToQueue(TrackModel track)
{
    if (!_playlist.Contains(track))
    {
        _playlist.Add(track);
        RebuildView();
        UpdateQueuePreview();
        Console.WriteLine($"✓ Added '{track.Title}' to queue");
    }
    else
    {
        Console.WriteLine($"⚠️ '{track.Title}' is already in queue");
    }
}

public void PlayTrackNext(TrackModel track)
{ 
    int existingIndex = _playlist.IndexOf(_playlist.FirstOrDefault(t => t.Path == track.Path));
    if (existingIndex >= 0 && existingIndex != _index)
    {
        _playlist.RemoveAt(existingIndex);
        if (existingIndex < _index)
            _index--;
    }
     
    int insertPosition = _index + 1;
    if (insertPosition >= _playlist.Count)
    {
        _playlist.Add(track);
    }
    else
    {
        _playlist.Insert(insertPosition, track);
    }
    
    RebuildView();
    RebuildShuffleQueue();
    UpdateQueuePreview();
    
    Console.WriteLine($"✓ '{track.Title}' will play next");
}
 private async void OnPlaylistsButtonClick(object? sender, RoutedEventArgs e)
{
    var playlistsWindow = new Window
    {
        Width = 1200,
        Height = 800,
        Title = "Your Playlists - Noctune",
        Background = new SolidColorBrush(Color.Parse("#000000"))
    };

    void ShowPlaylists()
    {
        var playlistsView = new PlaylistsView();

        playlistsView.BackToPlayerRequested += (s, args) =>
        {
            playlistsWindow.Close();
        };

        playlistsView.PlaylistSelected += (s, playlistId) =>
        {
            var detailView = new PlaylistDetailView(playlistId);

            detailView.BackRequested += (bs, be) =>
            {
                ShowPlaylists();
            };

            detailView.PlayAllRequested += (ps, tracks) =>
            {
                _playlist.Clear();
                foreach (var t in tracks)
                    _playlist.Add(t);

                RebuildView();
                if (_playlist.Count > 0)
                {
                    _index = 0;
                    PlayIndex();
                }
                playlistsWindow.Close();
            };
 
            detailView.AddToQueueRequested += (ds, track) =>
            {
                AddTrackToQueue(track);
            };
 
            detailView.PlayNextRequested += (ds, track) =>
            {
                PlayTrackNext(track);
            };

            playlistsWindow.Content = detailView;
        };

        playlistsWindow.Content = playlistsView;
    }

    ShowPlaylists();

    await playlistsWindow.ShowDialog((Window)VisualRoot!);
}
   
private void OnResponsivePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == BoundsProperty)
    {
        UpdateResponsiveLayout();
    }
}

private void UpdateResponsiveLayout()
{
    var bounds = this.Bounds;
    _currentWidth = bounds.Width;
    _currentHeight = bounds.Height;
 
    if (_currentWidth < SMALL_SCREEN)
    {
        ApplySmallScreenLayout();
    }
    else if (_currentWidth < MEDIUM_SCREEN)
    {
        ApplyMediumScreenLayout();
    }
    else if (_currentWidth < LARGE_SCREEN)
    {
        ApplyLargeScreenLayout();
    }
    else
    {
        ApplyFullScreenLayout();
    }
}
 
private void ApplySmallScreenLayout()
{
    var sidebar = this.FindControl<Border>("SidebarPanel");
    if (sidebar != null)
    {
        sidebar.IsVisible = false;
        sidebar.Tag = "Collapsed";
    }

    var lyrics = this.FindControl<Border>("LyricsPanelContainer");
    if (lyrics != null)
    {
        lyrics.IsVisible = false;
        lyrics.Tag = "Collapsed";
    }

    var hamburger = this.FindControl<Button>("HamburgerButton");
    if (hamburger != null)
    {
        hamburger.IsVisible = true;
    }

    var mobileLyrics = this.FindControl<Button>("MobileLyricsButton");
    if (mobileLyrics != null)
    {
        mobileLyrics.IsVisible = true;
    }

    var mainGrid = this.FindControl<Grid>("MainContentGrid");
    if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
    {
        mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
        mainGrid.ColumnDefinitions[2].Width = new GridLength(0);
    }

    var nowPlaying = this.FindControl<Grid>("NowPlayingSection");
    var coverContainer = this.FindControl<Grid>("MiniCoverContainer");
    var trackInfo = this.FindControl<StackPanel>("TrackInfoPanel");
    
    if (nowPlaying != null && coverContainer != null && trackInfo != null)
    {
        nowPlaying.RowDefinitions.Clear();
        nowPlaying.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        nowPlaying.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        
        Grid.SetRow(coverContainer, 0);
        Grid.SetColumn(coverContainer, 0);
        Grid.SetColumnSpan(coverContainer, 2);
        coverContainer.Width = 140;
        coverContainer.Height = 140;
        coverContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        
        Grid.SetRow(trackInfo, 1);
        Grid.SetColumn(trackInfo, 0);
        Grid.SetColumnSpan(trackInfo, 2);
        trackInfo.Margin = new Thickness(0, 20, 0, 0);
        trackInfo.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        
        if (TrackLabel != null) TrackLabel.FontSize = 22;
        if (ArtistLabel != null) ArtistLabel.FontSize = 14;
        if (AlbumLabel != null) AlbumLabel.FontSize = 12;
    }

    var centerControls = this.FindControl<StackPanel>("CenterControls");
    if (centerControls != null)
    {
        centerControls.Spacing = 6;
    }

    var playbackBar = this.FindControl<Border>("PlaybackBar");
    if (playbackBar != null)
    {
        playbackBar.Padding = new Thickness(12, 10);
    }

    if (_currentWidth < 700)
    {
        var volume = this.FindControl<StackPanel>("VolumeControls");
        if (volume != null)
        {
            volume.IsVisible = false;
        }
    }

    var mainContent = this.FindControl<Grid>("MainContentArea");
    if (mainContent != null)
    {
        mainContent.Margin = new Thickness(12, 12, 12, 12);
    }

    if (SearchBox != null)
    {
        SearchBox.MaxWidth = double.PositiveInfinity;
        SearchBox.Width = double.NaN;
    }
}
private void ApplyMediumScreenLayout()
{ 
    var sidebar = this.FindControl<Border>("SidebarPanel");
    if (sidebar != null)
    {
        sidebar.IsVisible = true;
        sidebar.Tag = null;
    }

    var lyrics = this.FindControl<Border>("LyricsPanelContainer");
    if (lyrics != null)
    {
        lyrics.IsVisible = false;
        lyrics.Tag = "Collapsed";
    }
 
    var hamburger = this.FindControl<Button>("HamburgerButton");
    if (hamburger != null)
    {
        hamburger.IsVisible = false;
    }
 
    var mobileLyrics = this.FindControl<Button>("MobileLyricsButton");
    if (mobileLyrics != null)
    {
        mobileLyrics.IsVisible = false;
    } 
    var mainGrid = this.FindControl<Grid>("MainContentGrid");
    if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
    {
        mainGrid.ColumnDefinitions[0].Width = GridLength.Auto; 
        mainGrid.ColumnDefinitions[2].Width = new GridLength(0);  
    }

    ResetNowPlayingLayout();
    ResetDefaultSizes();
 
    var volume = this.FindControl<StackPanel>("VolumeControls");
    if (volume != null)
    {
        volume.IsVisible = true;
    }
}

private void ApplyLargeScreenLayout()
{ 
    var sidebar = this.FindControl<Border>("SidebarPanel");
    if (sidebar != null)
    {
        sidebar.IsVisible = true;
        sidebar.Tag = null;
    }

    var lyrics = this.FindControl<Border>("LyricsPanelContainer");
    if (lyrics != null)
    {
        lyrics.IsVisible = false;
        lyrics.Tag = "Collapsed";
    }
 
    var mainGrid = this.FindControl<Grid>("MainContentGrid");
    if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
    {
        mainGrid.ColumnDefinitions[0].Width = GridLength.Auto;  
        mainGrid.ColumnDefinitions[2].Width = new GridLength(0);  
    }

    ResetDefaultLayout();
}

private void ApplyFullScreenLayout()
{ 
    var sidebar = this.FindControl<Border>("SidebarPanel");
    if (sidebar != null)
    {
        sidebar.IsVisible = true;
        sidebar.Tag = null;
    }

    var lyrics = this.FindControl<Border>("LyricsPanelContainer");
    if (lyrics != null)
    {
        lyrics.IsVisible = true;
        lyrics.Tag = null;
    }
 
    var mainGrid = this.FindControl<Grid>("MainContentGrid");
    if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
    {
        mainGrid.ColumnDefinitions[0].Width = GridLength.Auto; 
        mainGrid.ColumnDefinitions[2].Width = new GridLength(380);  
    }

    ResetDefaultLayout();
}

private void ResetDefaultLayout()
{
    var hamburger = this.FindControl<Button>("HamburgerButton");
    if (hamburger != null)
    {
        hamburger.IsVisible = false;
    }

    var mobileLyrics = this.FindControl<Button>("MobileLyricsButton");
    if (mobileLyrics != null)
    {
        mobileLyrics.IsVisible = false;
    }

    ResetNowPlayingLayout();
    ResetDefaultSizes();

    var volume = this.FindControl<StackPanel>("VolumeControls");
    if (volume != null)
    {
        volume.IsVisible = true;
    }

    var centerControls = this.FindControl<StackPanel>("CenterControls");
    if (centerControls != null)
    {
        centerControls.Spacing = 8;
    }

    var playbackBar = this.FindControl<Border>("PlaybackBar");
    if (playbackBar != null)
    {
        playbackBar.Padding = new Thickness(20, 12);
    }

    var mainContent = this.FindControl<Grid>("MainContentArea");
    if (mainContent != null)
    {
        mainContent.Margin = new Thickness(16, 16, 16, 16);
    }

    if (SearchBox != null)
    {
        SearchBox.MaxWidth = 500;
        SearchBox.Width = 350;
    }
}

private void ResetNowPlayingLayout()
{
    var nowPlaying = this.FindControl<Grid>("NowPlayingSection");
    var coverContainer = this.FindControl<Grid>("MiniCoverContainer");
    var trackInfo = this.FindControl<StackPanel>("TrackInfoPanel");
    
    if (nowPlaying != null && coverContainer != null && trackInfo != null)
    {
        nowPlaying.RowDefinitions.Clear();
        
        Grid.SetRow(coverContainer, 0);
        Grid.SetColumn(coverContainer, 0);
        Grid.SetColumnSpan(coverContainer, 1);
        coverContainer.Width = 180;
        coverContainer.Height = 180;
        coverContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        
        Grid.SetRow(trackInfo, 0);
        Grid.SetColumn(trackInfo, 1);
        Grid.SetColumnSpan(trackInfo, 1);
        trackInfo.Margin = new Thickness(30, 0, 0, 0);
        trackInfo.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
    }
}
private void ResetDefaultSizes()
{
    if (TrackLabel != null) TrackLabel.FontSize = 28;
    if (ArtistLabel != null) ArtistLabel.FontSize = 16;
    if (AlbumLabel != null) AlbumLabel.FontSize = 13;
}

private void OnHamburgerClick(object? sender, RoutedEventArgs e)
{
    var sidebar = this.FindControl<Border>("SidebarPanel");
    if (sidebar != null)
    {
        sidebar.IsVisible = !sidebar.IsVisible;
         
        var mainGrid = this.FindControl<Grid>("MainContentGrid");
        if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 1)
        {
            mainGrid.ColumnDefinitions[0].Width = sidebar.IsVisible ? GridLength.Auto : new GridLength(0);
        }
    }
}

private void OnMobileLyricsClick(object? sender, RoutedEventArgs e)
{ 
    var lyrics = this.FindControl<Border>("LyricsPanelContainer");
    if (lyrics != null)
    {
        lyrics.IsVisible = !lyrics.IsVisible;
         
        var mainGrid = this.FindControl<Grid>("MainContentGrid");
        if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 3)
        {
            mainGrid.ColumnDefinitions[2].Width = lyrics.IsVisible ? new GridLength(380) : new GridLength(0);
        }
            if (lyrics.IsVisible && _currentWidth < MEDIUM_SCREEN)
        {
            var sidebar = this.FindControl<Border>("SidebarPanel");
            if (sidebar != null)
            {
                sidebar.IsVisible = false;
                if (mainGrid != null && mainGrid.ColumnDefinitions.Count >= 1)
                {
                    mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
                }
            }
        }
    }
}


    
    private void LoadLikedSongsPlaylist()
    {
        if (_isLoadingLikedSongs) 
        {
            Console.WriteLine("⚠️ Already loading liked songs, skipping...");
            return;
        }
    
        _isLoadingLikedSongs = true;
    
        try
        {
            var likedTrackPaths = LikesService.GetAllLikedTrackPaths();
            var originalPlaylist = _playlist.ToList();
        
            _playlist.Clear();
        
            foreach (var path in likedTrackPaths)
            {
                var existingTrack = originalPlaylist.FirstOrDefault(t => t.Path == path);
            
                if (existingTrack != null)
                {
                    _playlist.Add(existingTrack);
                }
                else
                {
                    var newTrack = new TrackModel(path);
                    newTrack.LoadMetadata();
                    _playlist.Add(newTrack);
                }
            }
        
            RebuildView();
        
            if (_playlist.Count > 0)
            {
                _index = 0;
                PlayIndex();
            }
        
            Console.WriteLine($"✓ Loaded {_playlist.Count} liked songs");
        }
        finally
        {
            _isLoadingLikedSongs = false;
        }
    }
    private void QueueItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var visual = e.Source as Visual;
        var presenter = visual?.FindAncestorOfType<ContentPresenter>();

        if (presenter?.DataContext is QueueEntry entry)
        {
            var track = entry.Track;
            int index = _playlist.IndexOf(track);

            if (index >= 0)
            {
                _index = index;
                RebuildShuffleQueue();
                PlayIndex();

                var viewIndex = _viewTracks.IndexOf(track);
                if (viewIndex >= 0)
                    PlaylistBox.SelectedIndex = viewIndex;
            }
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Focusable = true;
        Focus();
    }

    private void WireForcedPlaylistUI()
    {
        MainForceCreateOverlay = this.FindControl<Border>("MainForceCreateOverlay");
        MainForceNameBox = this.FindControl<TextBox>("MainForceNameBox");
        MainForceDescBox = this.FindControl<TextBox>("MainForceDescBox");
        MainForceCreateButton = this.FindControl<Button>("MainForceCreateButton");

        MainForceCreateButton.Click += MainForceCreateButton_Click;
    }
    
   
    private async void UploadLyrics_Click(object? sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel selectedTrack)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Lyrics File (.lrc)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Lyrics Files")
                {
                    Patterns = new[] { "*.lrc", "*.txt" }
                }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            string lyricsContent = await reader.ReadToEndAsync();
  selectedTrack.LyricsData = lyricsContent;

            Console.WriteLine($"✓ Uploaded lyrics for: {selectedTrack.Title}");
  if (_currentTrack != null && _currentTrack.Path == selectedTrack.Path)
            {
                _lyricsDisplay?.SetCurrentTrack(selectedTrack);
            }
        }
    }

    private async void MainForceCreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MainForceNameBox.Text))
            return;

        try
        {
            var http = new HttpClient();
            if (!string.IsNullOrEmpty(_token))
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);
            }

            var payload = new
            {
                Name = MainForceNameBox.Text.Trim(),
                Description = MainForceDescBox.Text?.Trim()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var body = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await http.PostAsync($"{ApiConfig.BaseUrl}/playlists", body);

            if (res.IsSuccessStatusCode)
            {
                MainForceCreateOverlay.IsVisible = false;
            }
        }
        catch
        {
        }
    }

    public void ShowMainForcedCreate()
    {
        MainForceCreateOverlay.IsVisible = true;
        MainForceNameBox.Focus();
    }

    public void ShowPlayer()
    {
        Content = _playerContent;
    }

    private async void OnStatsButtonClick(object? sender, RoutedEventArgs e)
    {
        var statsWindow = new Window
        {
            Width = 900,
            Height = 700,
            Title = "Your Stats - Noctune",
            Background = new SolidColorBrush(Color.Parse("#000000")),
            Content = new StatsView()
        };

        var statsView = (StatsView)statsWindow.Content;
        statsView.LoadStats(_userEmail, _accountCreated, _playlist.ToList(), _playHistory);

        await statsWindow.ShowDialog((Window)VisualRoot!);
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.ClearToken();

        var top = TopLevel.GetTopLevel(this);
        if (top is Window w)
        {
            w.Content = new LoginView();
            var loginView = (LoginView)w.Content;
            loginView.LoginSucceeded += token =>
            {
                w.Content = new MainWindow(token);
            };
            loginView.NavigateToRegister += () =>
            {
                w.Content = new RegisterView();
                var registerView = (RegisterView)w.Content;
                registerView.NavigateToLogin += () =>
                {
                    w.Content = new LoginView();
                };
            };
        }

        _ = SyncService.SyncAsync(_token);
    }

    private void TrackSongPlay(TrackModel track)
    {
        if (_playHistory.ContainsKey(track))
            _playHistory[track]++;
        else
            _playHistory[track] = 1;

        StatsService.SavePlayHistory(_playHistory, _playlist);
    }

    
    void PlayIndex()
    {
        _mp.Stop();

        if (_index < 0 || _index >= _playlist.Count)
        {
            UpdateQueuePreview();
            return;
        }

        UpdateTrackListPlayingState();
        SaveCurrentListeningSession();

        var t = _playlist[_index];
        SaveTrack(t);

        t.LoadMetadata();

        _currentTrack = t;
    
      _lyricsDisplay?.SetCurrentTrack(t);

        _currentTrackStartTime = DateTime.Now;
        _lastKnownPosition = 0;

        TrackSongPlay(t);

        TrackLabel.Text = t.Title;
        AlbumLabel.Text = t.Album;
        ArtistLabel.Text = t.Artist;
        MiniCover.Source = t.Art;
        MiniTitle.Text = t.Title;
        MiniArtist.Text = t.Artist;

        AlbumArt.Source = t.Art;
        UpdateBackgroundFromAlbum(t.Art as Bitmap);

        var media = new Media(_libVLC, new Uri(t.Path));
        _mp.Media = media;
        _mp.Play();

        int vi = _viewTracks.IndexOf(t);
        if (vi >= 0)
            PlaylistBox.SelectedIndex = vi;

        UpdateTrackListPlayingState();
        UpdateQueuePreview();

        _seekMax = _mp.Length > 0 ? _mp.Length : 1;
        if (SeekBarContainer.Bounds.Width > 0)
            SeekBarFill.Width = 0;

        if (!_restoredLastPosition && _settings.LastIndex == _index && _settings.LastPosition > 0)
        {
            long pos = _settings.LastPosition;
            if (pos > 0)
            {
                _mp.Time = pos;
                _seekMax = _mp.Length > 0 ? _mp.Length : _seekMax;
                if (SeekBarContainer.Bounds.Width > 0 && _seekMax > 0)
                {
                    double pct = Math.Min(_seekMax, pos) / (double)_seekMax;
                    SeekBarFill.Width = pct * SeekBarContainer.Bounds.Width;
                }
            }
            _restoredLastPosition = true;
        }
    }



    void UpdateTrackListPlayingState()
    {
        if (PlaylistBox == null)
            return;

        var generator = PlaylistBox.ItemContainerGenerator;
        if (generator == null)
            return;

        int currentViewIndex = -1;
        if (_index >= 0 && _index < _playlist.Count)
        {
            var currentTrack = _playlist[_index];
            currentViewIndex = _viewTracks.IndexOf(currentTrack);
        }

        int itemCount = PlaylistBox.ItemCount;
        for (int i = 0; i < itemCount; i++)
        {
            var container = generator.ContainerFromIndex(i) as Control;
            if (container == null)
                continue;

            var wave = container.FindControl<ContentControl>("WaveIcon");
            var indexText = container.FindControl<TextBlock>("TrackIndexText");

            if (wave == null || indexText == null)
                continue;

            if (i == currentViewIndex && _mp.IsPlaying && !_isPaused)
            {
                wave.IsVisible = true;
                indexText.IsVisible = false;
            }
            else
            {
                wave.IsVisible = false;
                indexText.IsVisible = true;
            }
        }
    }

    void SeekBarPressed(object? sender, PointerPressedEventArgs e)
    {
        _seeking = true;
        UpdateSeekbar(e);
    }

    void SeekBarMoved(object? sender, PointerEventArgs e)
    {
        if (_seeking && e.GetCurrentPoint(SeekBarContainer).Properties.IsLeftButtonPressed)
            UpdateSeekbar(e);
    }

    void SeekBarReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_seeking && _mp.IsSeekable && SeekBarContainer.Bounds.Width > 0 && _seekMax > 0)
        {
            var pct = SeekBarFill.Width / SeekBarContainer.Bounds.Width;
            long ms = (long)(pct * _seekMax);
            _mp.Time = ms;
        }

        _seeking = false;
    }

    void UpdateSeekbar(PointerEventArgs e)
    {
        var pos = e.GetPosition(SeekBarContainer).X;
        pos = Math.Clamp(pos, 0, SeekBarContainer.Bounds.Width);

        SeekBarFill.Width = pos;

        if (_seekMax > 0 && SeekBarContainer.Bounds.Width > 0)
        {
            double pct = pos / SeekBarContainer.Bounds.Width;
            long ms = (long)(pct * _seekMax);
            CurrentTimeText.Text = TimeSpan.FromMilliseconds(ms).ToString(@"m\:ss");
        }
    }

  private async void AddToPlaylist(object? sender, RoutedEventArgs e)
{
    if (PlaylistBox.SelectedItem is not TrackModel currentTrack)
    {
        Console.WriteLine("⚠️ No track selected");
        return;
    }

    if (string.IsNullOrEmpty(_token))
    {
        Console.WriteLine("⚠️ No authentication token available");
        return;
    }

    var dialog = new Window
    {
        Width = 400,
        Height = 500,
        Title = "Add to Playlist",
        Background = new SolidColorBrush(Color.Parse("#121212")),
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };

    var playlistsList = new ListBox
    {
        Background = new SolidColorBrush(Color.Parse("#181818")),
        Margin = new Thickness(16)
    };

    try
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        var response = await httpClient.GetAsync($"{ApiConfig.BaseUrl}/playlists");
        if (response.IsSuccessStatusCode)
        {
            var playlists = await response.Content.ReadFromJsonAsync<List<PlaylistApiItem>>();
            if (playlists != null)
            {
                playlistsList.ItemsSource = playlists;
                Console.WriteLine($"✓ Loaded {playlists.Count} playlists");
            }
        }
        else
        {
            Console.WriteLine($"⚠️ Failed to load playlists: {response.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error loading playlists: {ex.Message}");
    }

    playlistsList.SelectionChanged += async (s, args) =>
    {
        if (playlistsList.SelectedItem is PlaylistApiItem selectedPlaylist)
        {
            if (string.IsNullOrWhiteSpace(selectedPlaylist.Id))
            {
                Console.WriteLine("⚠️ Invalid playlist ID");
                dialog.Close();
                return;
            }

            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);

                var payload = new { TrackPath = currentTrack.Path };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(
                    $"{ApiConfig.BaseUrl}/playlists/{selectedPlaylist.Id}/tracks",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Added '{currentTrack.Title}' to playlist '{selectedPlaylist.Name}'");
                }
                else
                {
                    Console.WriteLine($"⚠️ Failed to add track: {response.StatusCode}");
                }

                dialog.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error adding track to playlist: {ex.Message}");
                dialog.Close();
            }
        }
    };

    dialog.Content = playlistsList;
    
    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel is Window parentWindow)
    {
        await dialog.ShowDialog(parentWindow);
    }
    else
    {
        dialog.Show();
    }
}
    void PlayPause(object? s, RoutedEventArgs e)
    {
        if (_isTogglingPlay)
            return;

        _isTogglingPlay = true;

        try
        {
            if (_mp.Media == null)
            {
                if (_playlist.Count > 0)
                {
                    if (_index < 0)
                        _index = 0;

                    PlayIndex();
                }
                _isPaused = false;
                return;
            }

            if (_mp.IsPlaying && !_isPaused)
            {
                _mp.Pause();
                _isPaused = true;

                PlayPauseIcon.Source = new SvgImage
                {
                    Source = SvgSource.Load("avares://MusicPlayerApp/Images/play-solid-full.svg", null)
                };
            }
            else
            {
                _mp.Play();
                _isPaused = false;

                PlayPauseIcon.Source = new SvgImage
                {
                    Source = SvgSource.Load("avares://MusicPlayerApp/Images/pause-solid-full.svg", null)
                };
            }
        }
        finally
        {
            _isTogglingPlay = false;
        }

        UpdateTrackListPlayingState();
    }

    async Task NextInternal()
    {
        if (_playlist.Count == 0)
            return;

        if (_shuffle)
        {
            if (_staticShuffleQueue.Count == 0)
                RebuildShuffleQueue();

            if (_staticShuffleQueue.Count == 0)
                return;

            if (_index >= 0 && _index < _playlist.Count)
                _history.Push(_index);

            int next = _staticShuffleQueue[0];
            _staticShuffleQueue.RemoveAt(0);
            _index = next;
            PlayIndex();
        }
        else
        {
            int next = (_index + 1) % _playlist.Count;
            _index = next;
            PlayIndex();
        }
    }

    async void Next(object? s, RoutedEventArgs e)
        => await NextInternal();

    async void Prev(object? s, RoutedEventArgs e)
    {
        if (_playlist.Count == 0)
            return;

        if (_shuffle)
        {
            if (_history.Count > 0)
            {
                int prevIndex = _history.Pop();
                _index = prevIndex;
                PlayIndex();
            }
            else
            {
                int next = (_index - 1 + _playlist.Count) % _playlist.Count;
                _index = next;
                PlayIndex();
            }
        }
        else
        {
            int next = (_index - 1 + _playlist.Count) % _playlist.Count;
            _index = next;
            PlayIndex();
        }
    }

    async Task CrossfadeTo(int nextIndex)
    {
        if (_playlist.Count == 0 || _isCrossfading)
            return;

        _isCrossfading = true;
        int fadeMs = 300;
        int steps = 12;
        int delay = fadeMs / steps;

        int targetVol = (int)VolumeSlider.Value;
        _targetVolume = targetVol;

        for (int i = 0; i < steps; i++)
        {
            _mp.Volume = Math.Max(0, targetVol - (targetVol * i / steps));
            await Task.Delay(delay);
        }

        _index = nextIndex;
        PlayIndex();

        _mp.Volume = 0;

        for (int i = 0; i < steps; i++)
        {
            _mp.Volume = Math.Min(_targetVolume, (_targetVolume * i / steps));
            await Task.Delay(delay);
        }

        _mp.Volume = _targetVolume;
        _isCrossfading = false;
    }

    void InitPositionTimer()
    {
        _positionUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _positionUpdateTimer.Tick += PositionUpdateTimer_Tick;
        _positionUpdateTimer.Start();
    }

    void PositionUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_mp.IsPlaying && !_seeking)
        {
            var t = _mp.Time;
            var l = _mp.Length;
            if (l > 0)
            {
                _seekMax = l;
                _lastKnownPosition = t;
                CurrentTimeText.Text = TimeSpan.FromMilliseconds(t).ToString(@"m\:ss");
                TotalTimeText.Text = TimeSpan.FromMilliseconds(l).ToString(@"m\:ss");

                if (SeekBarContainer.Bounds.Width > 0 && _seekMax > 0)
                {
                    double pct = t / (double)_seekMax;
                    SeekBarFill.Width = pct * SeekBarContainer.Bounds.Width;
                }
            
                if (_lyricsDisplay != null && _lyricsDisplay.HasLyrics)
                {
                    _lyricsDisplay.UpdatePosition(TimeSpan.FromMilliseconds(t));
                }
              
            }
        }
        SyncFullscreen();
    }

   
    void SyncFullscreen()
    {
        if (_fullscreen == null || !_fullscreen.IsVisible)
            return;

        double cur = _mp.Time;
        double max = _seekMax > 0 ? _seekMax : _mp.Length;

        _fullscreen.UpdatePlayback(
            cur,
            max,
            CurrentTimeText.Text,
            TotalTimeText.Text,
            _mp.IsPlaying);

        if (_index >= 0 && _index < _playlist.Count)
        {
            var t = _playlist[_index];
        
            if (_lastFullscreenTrack != t)
            {
                _fullscreen.UpdateTrack(
                    t.Art as Bitmap,
                    t.Title,
                    t.Artist,
                    t.Album,
                    t.DateAdded.Year.ToString());
            
                LoadFullscreenLyrics(t);
                _lastFullscreenTrack = t;
            }
        }
    }

    void VolumeChanged(object? s, RangeBaseValueChangedEventArgs e)
    {
        _targetVolume = (int)e.NewValue;
        if (!_isCrossfading)
            _mp.Volume = _targetVolume;
    }

    void InitVisualizer()
    {
        VisualizerPanel.Children.Clear();

        for (int i = 0; i < VisualBarCount; i++)
        {
            var b = new Border
            {
                Width = 2,
                Height = 6,
                Margin = new Thickness(1, 0, 1, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.Parse("#1DB954"))
            };
            VisualizerPanel.Children.Add(b);
        }

        _visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _visualizerTimer.Tick += VisualizerTimer_Tick;
        _visualizerTimer.Start();
    }

    void VisualizerTimer_Tick(object? sender, EventArgs e)
    {
        double[] bars;
        lock (_audioLock)
            bars = _visualizer.GetSpectrum(VisualBarCount);

        if (!_mp.IsPlaying || bars.Length == 0)
        {
            foreach (var c in VisualizerPanel.Children.OfType<Border>())
                c.Height = 4;

            if (BackgroundGradient != null)
                BackgroundGradient.Opacity = 0.7;

            return;
        }

        int cnt = Math.Min(VisualizerPanel.Children.Count, bars.Length);
        double sum = 0;
        for (int i = 0; i < cnt; i++)
        {
            double v = bars[i];
            double h = 6 + v * 60;
            if (VisualizerPanel.Children[i] is Border b)
                b.Height = h;
            sum += v;
        }

        if (BackgroundGradient != null)
        {
            double avg = sum / cnt;
            BackgroundGradient.Opacity = 0.6 + Math.Min(0.4, avg * 2);
        }
    }

    void OnPipeWireSamples(float[] samples)
    {
        lock (_audioLock)
            _visualizer.AddSamples(samples);
    }

    void ShowFullscreen()
    {
        if (_index < 0 || _index >= _playlist.Count)
            return;

        var t = _playlist[_index];
        _fullscreen = new FullscreenPlayer(
            t.Art as Bitmap,
            t.Title,
            t.Artist,
            t.Album,
            t.DateAdded.Year.ToString());

        _lastFullscreenTrack = t;
        LoadFullscreenLyrics(t);

        _fullscreen.PrevRequested += (_, _) => Prev(null, new RoutedEventArgs());
        _fullscreen.NextRequested += (_, _) => Next(null, new RoutedEventArgs());
        _fullscreen.PlayPauseRequested += (_, _) => PlayPause(null, new RoutedEventArgs());
        _fullscreen.SeekRequested += (_, pos) =>
        {
            if (_mp.Media != null && _mp.IsSeekable)
                _mp.Time = pos;
        };

        _fullscreen.VolumeRequested += (_, delta) =>
        {
            VolumeSlider.Value = Math.Clamp(
                VolumeSlider.Value + delta,
                VolumeSlider.Minimum,
                VolumeSlider.Maximum);
        };

        _fullscreen.Closed += (_, _) =>
        {
            _fullscreen = null;
            _lastFullscreenTrack = null; 
            Focus();
        };

        SyncFullscreen();
        _fullscreen.Show();
    }
    
    private void LoadFullscreenLyrics(TrackModel track)
    {
        if (_fullscreen == null) return;

        if (track.HasLyrics && !string.IsNullOrEmpty(track.LyricsData))
        {
            var lyricsLines = ParseLyricsForFullscreen(track.LyricsData);
            _fullscreen.LoadLyrics(lyricsLines);
        }
        else
        {
            _fullscreen.LoadLyrics(new List<LyricsLine>());
        }
    }
private List<LyricsLine> ParseLyricsForFullscreen(string lrcContent)
{
    var lines = new List<LyricsLine>();
    
    try
    {
        var lrcLines = lrcContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var lrcPattern = @"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)";
        var parsedLines = new List<LyricsLine>();

        foreach (var line in lrcLines)
        {
            var match = Regex.Match(line, lrcPattern);
            if (match.Success)
            {
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                int milliseconds = int.Parse(match.Groups[3].Value.PadRight(3, '0'));
                string text = match.Groups[4].Value.Trim();

                bool isInstrumental = string.IsNullOrWhiteSpace(text) || 
                                    text == "♪" || 
                                    text == "..." ||
                                    text == "🎵";

                parsedLines.Add(new LyricsLine
                {
                    StartTime = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                    Text = text,
                    IsInstrumental = isInstrumental
                });
            }
        }

        for (int i = 0; i < parsedLines.Count; i++)
        {
            if (i < parsedLines.Count - 1)
            {
                parsedLines[i].EndTime = parsedLines[i + 1].StartTime;
            }
            else
            {
                parsedLines[i].EndTime = parsedLines[i].StartTime.Add(TimeSpan.FromSeconds(5));
            }
        }

        return parsedLines;
    }
    catch
    {
        return new List<LyricsLine>();
    }
}
    void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        var focused = top?.FocusManager?.GetFocusedElement();

        if (focused is TextBox)
            return;

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            PlayPause(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.MediaPlayPause)
        {
            e.Handled = true;
            PlayPause(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.MediaNextTrack ||
            (e.Key == Key.Right && e.KeyModifiers == KeyModifiers.Control))
        {
            e.Handled = true;
            Next(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.MediaPreviousTrack ||
            (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Control))
        {
            e.Handled = true;
            Prev(null, new RoutedEventArgs());
            return;
        }
    }

    async void AddClicked(object? s, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider == null) return;

        var result = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio")
                { Patterns = new[] { "*.mp3", "*.wav", "*.flac" } }
            }
        });

        if (result != null)
            foreach (var file in result)
                _playlist.Add(new TrackModel(file.Path.LocalPath));

        RebuildView();

        if (_index == -1 && _playlist.Count > 0)
        {
            _index = 0;
            PlayIndex();
        }
        else
        {
            UpdateQueuePreview();
        }
    }

    void RemoveClicked(object? s, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel track)
            return;

        int i = _playlist.IndexOf(track);
        if (i < 0)
            return;

        _playlist.RemoveAt(i);

        if (i == _index)
        {
            _mp.Stop();
            _index = -1;

            if (_playlist.Count > 0)
            {
                _index = 0;
                RebuildView();
                PlayIndex();
                return;
            }
        }
        else if (i < _index)
        {
            _index--;
        }

        RebuildView();
        UpdateQueuePreview();
    }

    void PlaylistDouble(object? s, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel t)
            return;

        int i = _playlist.IndexOf(t);
        if (i < 0)
            return;

        _index = i;
        RebuildShuffleQueue();
        PlayIndex();
    }

    void PlayContextMenuItem_Click(object? sender, RoutedEventArgs e)
        => PlaylistDouble(null, e);

    private void RemoveContextMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel track)
        {
            Console.WriteLine("⚠️ No track selected for removal");
            return;
        }

        int i = _playlist.IndexOf(track);
        if (i < 0)
        {
            Console.WriteLine($"⚠️ Track '{track.Title}' not found in playlist");
            return;
        }

        Console.WriteLine($"🗑️ Removing '{track.Title}' from playlist");
 
        _playlist.RemoveAt(i);

           DeleteTrackFromDatabase(track);

        if (i == _index)
        {
            _mp.Stop();
            _index = -1;

            if (_playlist.Count > 0)
            {
                _index = 0;
                RebuildView();
                PlayIndex();
                Console.WriteLine($"✓ Removed and playing next track");
                return;
            }
            else
            {
                Console.WriteLine("⚠️ Playlist is now empty");
            }
        }
        else if (i < _index)
        {
            _index--;
        }

        RebuildView();
        UpdateQueuePreview();
        Console.WriteLine($"✓ Track removed successfully");
    }

    private void DeleteTrackFromDatabase(TrackModel track)
    {
        try
        {
            using var db = new LiteDatabase(GetTrackDbPath());
            var tracks = db.GetCollection<TrackModel>("tracks");
            tracks.Delete(track.Id);
            Console.WriteLine($"✓ Deleted '{track.Title}' from database");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error deleting track from database: {ex.Message}");
        }
    }
    void OpenFolderContextMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel t)
            return;
        TryOpenFolderForTrack(t.Path);
    }

    void TryOpenFolderForTrack(string path)
    {
        try
        {
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
        }
    }

    void InitWaveAnimation()
    {
        waveTimer = new DispatcherTimer();
        waveTimer.Interval = TimeSpan.FromMilliseconds(120);
        waveTimer.Tick += (_, _) =>
        {
            if (!_mp.IsPlaying || _isPaused)
                return;

            if (PlaylistBox == null)
                return;

            if (_index < 0 || _index >= _playlist.Count)
                return;

            var currentTrack = _playlist[_index];
            int viewIndex = _viewTracks.IndexOf(currentTrack);
            if (viewIndex < 0 || viewIndex >= PlaylistBox.ItemCount)
                return;

            var generator = PlaylistBox.ItemContainerGenerator;
            if (generator == null)
                return;

            var container = generator.ContainerFromIndex(viewIndex) as Control;
            if (container == null)
                return;

            var bars = container.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => Math.Abs(b.Width - 3) < 0.01)
                .ToList();

            if (bars.Count == 0)
                return;

            var rand = new Random();
            foreach (var b in bars)
                b.Height = rand.Next(5, 18);
        };
        waveTimer.Start();
    }

    async void FileDrop(object? s, DragEventArgs e)
    {
        if (e.Data is not IDataObject data)
            return;

        var files = data.GetFiles();
        if (files == null)
            return;

        foreach (var f in files)
        {
            string path = f.Path.LocalPath;
            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            {
                var newTrack = new TrackModel(path);
                _playlist.Add(newTrack);
                SaveTrack(newTrack); 
            }
        }

        RebuildView();

        if (_index == -1 && _playlist.Count > 0)
        {
            _index = 0;
            PlayIndex();
        }
        else
        {
            UpdateQueuePreview();
        }
    }
    string GetTrackDbPath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Noctune");

        Directory.CreateDirectory(basePath);
        return Path.Combine(basePath, "music.db");
    }


    void RebuildView()
    {
        _viewTracks.Clear();

        IEnumerable<TrackModel> q = _playlist;

        string search = SearchBox.Text ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(t =>
                (t.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Artist?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Album?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        string sortTag = (SortBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Title";

        q = sortTag switch
        {
            "Date" => q.OrderByDescending(t => t.DateAdded),
            "Artist" => q.OrderBy(t => t.Artist),
            "Album" => q.OrderBy(t => t.Album),
            _ => q.OrderBy(t => t.Title),
        };

        int idx = 1;
        foreach (var t in q)
        {
            t.Index = idx++;
            _viewTracks.Add(t);
        }

        if (_index >= 0 && _index < _playlist.Count)
        {
            var cur = _playlist[_index];
            int vi = _viewTracks.IndexOf(cur);
            if (vi >= 0)
                PlaylistBox.SelectedIndex = vi;
        }

        RebuildShuffleQueue();
        UpdateQueuePreview();
        UpdateTrackListPlayingState();
    }

    void SearchBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
            RebuildView();
    }

    void SortBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => RebuildView();

    void PlaylistBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel track)
            return;

        int i = _playlist.IndexOf(track);
        if (i < 0)
            return;

        if (_suppressSelectionPlay)
        {
            _suppressSelectionPlay = false;
            _index = i;
            RebuildShuffleQueue();
            UpdateQueuePreview();
            UpdateTrackListPlayingState();
            return;
        }

        _index = i;
        RebuildShuffleQueue();
        PlayIndex();
    }

    void PlaylistBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(PlaylistBox);
        var props = point.Properties;

        if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            return;
        }

        if (props.IsMiddleButtonPressed)
        {
            var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
            if (item?.DataContext is TrackModel track)
                track.IsFavorite = !track.IsFavorite;

            e.Handled = true;
            return;
        }
    }

    void AlbumArt_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_index < 0 || _index >= _playlist.Count)
            return;
        TryOpenFolderForTrack(_playlist[_index].Path);
    }

    void ShuffleButton_Click(object? sender, RoutedEventArgs e)
    {
        _shuffle = ShuffleButton.IsChecked == true;
        RebuildShuffleQueue();
        UpdateQueuePreview();
    }

    void LoopButton_Click(object? sender, RoutedEventArgs e)
        => _loop = LoopButton.IsChecked == true;

    void RebuildShuffleQueue()
    {
        _staticShuffleQueue.Clear();
        _history.Clear();

        if (!_shuffle)
            return;

        if (_playlist.Count <= 1)
            return;

        if (_index < 0 || _index >= _playlist.Count)
            _index = 0;

        var shuffled = Enumerable.Range(0, _playlist.Count)
            .Where(i => i != _index)
            .OrderBy(i => _rand.Next());

        foreach (int i in shuffled)
            _staticShuffleQueue.Add(i);
    }

    private void SaveTrack(TrackModel track)
    {
        try
        {
            using var db = new LiteDatabase(GetTrackDbPath());
            var tracks = db.GetCollection<TrackModel>("tracks");
        
            tracks.EnsureIndex(x => x.Path);
        
            var existing = tracks.FindOne(t => t.Path == track.Path);

            if (existing != null)
            {
                track.Id = existing.Id;
         
                tracks.Update(track);
            }
            else
            {
                track.Id = ObjectId.NewObjectId();
                tracks.Insert(track);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error saving track: {ex.Message}");
        }
    }

    private List<TrackModel> LoadAllTracks()
    {
        try
        {
            using var db = new LiteDatabase(GetTrackDbPath());
            var tracks = db.GetCollection<TrackModel>("tracks");
            var allTracks = tracks.FindAll().ToList();
            Console.WriteLine($"✓ Loaded {allTracks.Count} tracks from database");
            return allTracks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error loading tracks: {ex.Message}");
            return new List<TrackModel>();
        }
    }


    void UpdateQueuePreview()
    {
        _queuePreview.Clear();

        if (_playlist.Count == 0)
            return;

        if (_index < 0 || _index >= _playlist.Count)
            _index = 0;

        int max = Math.Min(25, _playlist.Count);

        if (_shuffle)
        {
            if (_staticShuffleQueue.Count == 0)
                RebuildShuffleQueue();

            _queuePreview.Add(new QueueEntry(_playlist[_index], true));
            foreach (int i in _staticShuffleQueue.Take(max - 1))
                _queuePreview.Add(new QueueEntry(_playlist[i], false));
            return;
        }

        for (int o = 0; o < max; o++)
        {
            int id = (_index + o) % _playlist.Count;
            _queuePreview.Add(new QueueEntry(_playlist[id], id == _index));
        }
    }

    void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (_loop && _index >= 0)
                await CrossfadeTo(_index);
            else
                await NextInternal();
        });
    }

    void SeekBarCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_seeking && _mp.IsSeekable && SeekBarContainer.Bounds.Width > 0 && _seekMax > 0)
        {
            var pct = SeekBarFill.Width / SeekBarContainer.Bounds.Width;
            long ms = (long)(pct * _seekMax);
            _mp.Time = ms;
        }

        _seeking = false;
    }

    void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _seekMax = e.Length;
            TotalTimeText.Text = TimeSpan.FromMilliseconds(e.Length)
                .ToString(@"m\:ss");
        });
    }

    void UpdateBackgroundFromAlbum(Bitmap? art)
    {
        if (art == null)
            return;

        try
        {
            var size = new PixelSize(24, 24);
            var bmp = new RenderTargetBitmap(size);

            using (var ctx = bmp.CreateDrawingContext())
                ctx.DrawImage(art, new Rect(art.Size), new Rect(0, 0, size.Width, size.Height));

            int pixels = size.Width * size.Height;
            int stride = size.Width * 4;
            int bufSize = pixels * 4;
            byte[] buffer = new byte[bufSize];

            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                bmp.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), h.AddrOfPinnedObject(), bufSize, stride);
            }
            finally
            {
                h.Free();
            }

            long rs = 0, gs = 0, bs = 0;
            for (int i = 0; i < bufSize; i += 4)
            {
                bs += buffer[i + 0];
                gs += buffer[i + 1];
                rs += buffer[i + 2];
            }

            byte r = (byte)(rs / pixels);
            byte g = (byte)(gs / pixels);
            byte b = (byte)(bs / pixels);

            Color c1 = Color.FromRgb(r, g, b);
            Color c2 = Color.FromRgb(
                (byte)Math.Min(255, r + 35),
                (byte)Math.Min(255, g + 35),
                (byte)Math.Min(255, b + 35));

            BackgroundGradient.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop { Color = c1, Offset = 0 },
                    new GradientStop { Color = c2, Offset = 1 }
                }
            };
        }
        catch
        {
        }
    }

    private void SaveCurrentListeningSession()
    {
        if (_index < 0 || _index >= _playlist.Count)
            return;

        var currentTrack = _playlist[_index];
        var currentPosition = _mp.Time;

        var listeningDuration = TimeSpan.FromMilliseconds(currentPosition - _lastKnownPosition);

        if (listeningDuration.TotalSeconds >= 3)
        {
            var session = new ListeningSession
            {
                TrackPath = currentTrack.Path,
                StartTime = _currentTrackStartTime,
                Duration = listeningDuration,
                Completed = currentPosition >= (currentTrack.Duration.TotalMilliseconds * 0.9)
            };

            _listeningSessions.Add(session);
            StatsService.SaveListeningSessions(_listeningSessions);
        }
    }

    public void SaveStateBeforeExit()
    {
        SaveCurrentListeningSession();

        _mp?.Stop();
        _capture?.Stop();
        _positionUpdateTimer?.Stop();

        _settings.Volume = VolumeSlider != null ? (int)VolumeSlider.Value : 50;
        _settings.Playlist = _playlist.Select(x => x.Path).ToList();
        _settings.LastIndex = _index;
        _settings.LastPosition = _mp != null ? _mp.Time : 0;

        SettingsService.Save(_settings);
        StatsService.SavePlayHistory(_playHistory, _playlist);
        StatsService.SaveListeningSessions(_listeningSessions);
    }
}


