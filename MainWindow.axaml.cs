using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using MusicPlayerApp.Audio;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TrackModel = MusicPlayerApp.Models.Track;

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

public partial class MainWindow : Window
{
    const int VisualBarCount = 120;

    readonly object _audioLock = new();

    PipeWireCapture? _capture;
    VisualizerService _visualizer = new();

    ObservableCollection<TrackModel> _playlist = new();
    ObservableCollection<TrackModel> _viewTracks = new();
    ObservableCollection<QueueEntry> _queuePreview = new();

    AppSettings _settings = new();
    LibVLC _libVLC;
    MediaPlayer _mp;
    int _index = -1;
    bool _userIsSeeking = false;
    bool _shuffle = false;
    bool _loop = false;
    bool _restoredLastPosition = false;
    Random _rand = new();
    List<int> _staticShuffleQueue = new();
    Stack<int> _history = new();

    DispatcherTimer? _visualizerTimer;
    DispatcherTimer? _positionUpdateTimer;

    public MainWindow()
    {
        InitializeComponent();
        Core.Initialize();

        _libVLC = new LibVLC("--aout=pulse");
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
        Position = new PixelPoint((int)_settings.WindowX, (int)_settings.WindowY);
        VolumeSlider.Value = _settings.Volume;

        foreach (var p in _settings.Playlist)
            if (File.Exists(p))
                _playlist.Add(new TrackModel(p));

        RebuildView();
        _index = _settings.LastIndex;

        if (_index >= 0 && _index < _playlist.Count)
            PlayIndex();
        else
            UpdateQueuePreview();

        ShuffleButton.Click += ShuffleButton_Click;
        LoopButton.Click += LoopButton_Click;

        _mp.EndReached += MediaPlayer_EndReached;
        _mp.LengthChanged += MediaPlayer_LengthChanged;

        AddButton.Click += AddClicked;
        RemoveButton.Click += RemoveClicked;
      PlaylistBox.SelectionChanged += PlaylistBox_SelectionChanged;


        PlayContextMenuItem.Click += PlayContextMenuItem_Click;
        RemoveContextMenuItem.Click += RemoveContextMenuItem_Click;
        OpenFolderContextMenuItem.Click += OpenFolderContextMenuItem_Click;

        PlayPauseButton.Click += PlayPause;
        PrevButton.Click += Prev;
        NextButton.Click += Next;

        VolumeSlider.ValueChanged += VolumeChanged;

        PositionSlider.PointerPressed += PositionSlider_PointerPressed;
        PositionSlider.PointerReleased += PositionSlider_PointerReleased;
        PositionSlider.PointerMoved += PositionSlider_PointerMoved;
        PositionSlider.PointerCaptureLost += PositionSlider_PointerCaptureLost;

        SearchBox.PropertyChanged += SearchBox_PropertyChanged;
        SortBox.SelectionChanged += SortBox_SelectionChanged;
        FavFilterButton.Click += FavFilterButton_Click;

        AlbumArt.DoubleTapped += AlbumArt_DoubleTapped;
        PlaylistBox.PointerPressed += PlaylistBox_PointerPressed;

        InitVisualizer();
        InitPositionTimer();

        KeyDown += MainWindow_KeyDown;
        MiniPlayButton.Click += (_,__) => PlayPause(null, new RoutedEventArgs());

    }

    void InitPositionTimer()
    {
        _positionUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionUpdateTimer.Tick += PositionUpdateTimer_Tick;
        _positionUpdateTimer.Start();
    }

    void PositionUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_mp.IsPlaying && !_userIsSeeking)
        {
            var time = _mp.Time;
            var length = _mp.Length;

            if (length > 0)
            {
                PositionSlider.Maximum = length;
                PositionSlider.Value = time;

                CurrentTimeText.Text = TimeSpan.FromMilliseconds(time).ToString(@"m\:ss");
                TotalTimeText.Text = TimeSpan.FromMilliseconds(length).ToString(@"m\:ss");
            }
        }
    }

    void PositionSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _userIsSeeking = true;

        var point = e.GetPosition(PositionSlider);
        var percentage = Math.Clamp(point.X / PositionSlider.Bounds.Width, 0, 1);
        var newPosition = (long)(percentage * PositionSlider.Maximum);

        if (newPosition >= 0 && newPosition <= PositionSlider.Maximum)
        {
            PositionSlider.Value = newPosition;
            CurrentTimeText.Text = TimeSpan.FromMilliseconds(newPosition).ToString(@"m\:ss");
        }
    }

    void PositionSlider_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_userIsSeeking && e.GetCurrentPoint(PositionSlider).Properties.IsLeftButtonPressed)
        {
            var point = e.GetPosition(PositionSlider);
            var percentage = Math.Clamp(point.X / PositionSlider.Bounds.Width, 0, 1);
            var newPosition = (long)(percentage * PositionSlider.Maximum);

            if (newPosition >= 0 && newPosition <= PositionSlider.Maximum)
            {
                PositionSlider.Value = newPosition;
                CurrentTimeText.Text = TimeSpan.FromMilliseconds(newPosition).ToString(@"m\:ss");
            }
        }
    }

    void PositionSlider_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        HandleSeek();
    }

    void PositionSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleSeek();
    }

    void HandleSeek()
    {
        if (_userIsSeeking && _mp.Media != null && _mp.IsSeekable)
        {
            var seekPosition = (long)PositionSlider.Value;
            _mp.Time = seekPosition;
        }

        _userIsSeeking = false;
    }

    void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space || e.Key == Key.MediaPlayPause)
        {
            e.Handled = true;
            PlayPause(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.MediaNextTrack || (e.Key == Key.Right && e.KeyModifiers == KeyModifiers.Control))
        {
            e.Handled = true;
            Next(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.MediaPreviousTrack || (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Control))
        {
            e.Handled = true;
            Prev(null, new RoutedEventArgs());
            return;
        }

        if (e.Key == Key.Up && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            VolumeSlider.Value = Math.Min(VolumeSlider.Maximum, VolumeSlider.Value + 5);
            return;
        }

        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            VolumeSlider.Value = Math.Max(VolumeSlider.Minimum, VolumeSlider.Value - 5);
            return;
        }

        if (e.Key == Key.L && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            SearchBox.Focus();
            return;
        }
    }

    void OnPipeWireSamples(float[] samples)
    {
        lock (_audioLock)
            _visualizer.AddSamples(samples);
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

        _visualizerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
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

        int count = Math.Min(VisualizerPanel.Children.Count, bars.Length);
        double sum = 0;

        for (int i = 0; i < count; i++)
        {
            double value = bars[i];
            double h = 6 + value * 60;
            if (VisualizerPanel.Children[i] is Border b)
                b.Height = h;
            sum += value;
        }

        if (BackgroundGradient != null && count > 0)
        {
            double avg = sum / count;
            double opacity = 0.6 + Math.Min(0.4, avg * 2);
            BackgroundGradient.Opacity = opacity;
        }
    }

    void ShuffleButton_Click(object? sender, RoutedEventArgs e)
    {
        _shuffle = ShuffleButton.IsChecked == true;
        RebuildShuffleQueue();
        UpdateQueuePreview();
    }

    void LoopButton_Click(object? sender, RoutedEventArgs e)
    {
        _loop = LoopButton.IsChecked == true;
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

    void SearchBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
            RebuildView();
    }

    void SortBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => RebuildView();

    void FavFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        FavFilterButton.IsChecked = !(FavFilterButton.IsChecked ?? false);
        RebuildView();
    }
void PlaylistBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (PlaylistBox.SelectedItem is not TrackModel track)
        return;

    int i = _playlist.IndexOf(track);
    if (i < 0)
        return;

    _index = i;
    RebuildShuffleQueue();
    PlayIndex();
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

        if (FavFilterButton.IsChecked == true)
            q = q.Where(t => t.IsFavorite);

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
            var current = _playlist[_index];
            int viewIndex = _viewTracks.IndexOf(current);
            if (viewIndex >= 0)
                PlaylistBox.SelectedIndex = viewIndex;
        }

        RebuildShuffleQueue();
        UpdateQueuePreview();
    }

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

    void UpdateQueuePreview()
    {
        _queuePreview.Clear();

        if (_playlist.Count == 0)
            return;

        if (_index < 0 || _index >= _playlist.Count)
            _index = 0;

        int max = Math.Min(10, _playlist.Count);

        if (_shuffle)
        {
            if (_staticShuffleQueue.Count == 0)
                RebuildShuffleQueue();

            _queuePreview.Add(new QueueEntry(_playlist[_index], true));

            foreach (int i in _staticShuffleQueue.Take(max - 1))
                _queuePreview.Add(new QueueEntry(_playlist[i], false));

            return;
        }

        for (int offset = 0; offset < max; offset++)
        {
            int idx = (_index + offset) % _playlist.Count;
            _queuePreview.Add(new QueueEntry(_playlist[idx], idx == _index));
        }
    }

    void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PositionSlider.Maximum = e.Length;
            TotalTimeText.Text = TimeSpan.FromMilliseconds(e.Length)
                .ToString(@"m\:ss");
        });
    }

    async Task CrossfadeTo(int nextIndex)
    {
        if (_playlist.Count == 0)
            return;

        int fadeMs = 400;
        int steps = 15;
        int delay = fadeMs / steps;
        int startVol = _mp.Volume;

        for (int i = 0; i < steps; i++)
        {
            _mp.Volume = Math.Max(0, _mp.Volume - startVol / steps);
            await Task.Delay(delay);
        }

        _index = nextIndex;
        PlayIndex();

        for (int i = 0; i < steps; i++)
        {
            _mp.Volume = Math.Min((int)VolumeSlider.Value, _mp.Volume + startVol / steps);
            await Task.Delay(delay);
        }
    }

    async void AddClicked(object? s, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
        if (PlaylistBox.SelectedItem is not TrackModel track)
            return;

        int i = _playlist.IndexOf(track);
        if (i < 0)
            return;

        _index = i;
        RebuildShuffleQueue();
        PlayIndex();
    }

    void PlayContextMenuItem_Click(object? sender, RoutedEventArgs e)
        => PlaylistDouble(null, e);

    void RemoveContextMenuItem_Click(object? sender, RoutedEventArgs e)
        => RemoveClicked(null, e);

    void OpenFolderContextMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is not TrackModel track)
            return;

        TryOpenFolderForTrack(track.Path);
    }

    void AlbumArt_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_index < 0 || _index >= _playlist.Count)
            return;

        var track = _playlist[_index];
        TryOpenFolderForTrack(track.Path);
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
        catch { }
    }

    void PlaylistBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(PlaylistBox).Properties.IsMiddleButtonPressed)
        {
            if (PlaylistBox.SelectedItem is TrackModel track)
                track.IsFavorite = !track.IsFavorite;
        }
    }

    void UpdateBackgroundFromAlbum(Bitmap? art)
    {
        if (art == null)
            return;

        try
        {
            var size = new PixelSize(24, 24);
            var rtb = new RenderTargetBitmap(size);

            using (var dc = rtb.CreateDrawingContext())
                dc.DrawImage(art, new Rect(art.Size), new Rect(0, 0, size.Width, size.Height));

            int pixels = size.Width * size.Height;
            int stride = size.Width * 4;
            int bufSize = pixels * 4;
            byte[] buffer = new byte[bufSize];

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                rtb.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height),
                    handle.AddrOfPinnedObject(),
                    bufSize,
                    stride);
            }
            finally { handle.Free(); }

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
        catch (Exception ex)
        {
            Console.WriteLine("BG extract error: " + ex.Message);
        }
    }

    void PlayIndex()
    {
        _mp.Stop();

        if (_index < 0 || _index >= _playlist.Count)
        {
            UpdateQueuePreview();
            return;
        }

        var track = _playlist[_index];

        TrackLabel.Text = track.Title;
        AlbumLabel.Text = track.Album;
        ArtistLabel.Text = track.Artist;
        MiniCover.Source = track.Art;
MiniTitle.Text = track.Title;
MiniArtist.Text = track.Artist;


        AlbumArt.Source = track.Art;
        UpdateBackgroundFromAlbum(track.Art as Bitmap);

        var media = new Media(_libVLC, new Uri(track.Path));
        _mp.Media = media;
        _mp.Play();

        PlayPauseIcon.Text = "⏸";

        int viewIndex = _viewTracks.IndexOf(track);
        if (viewIndex >= 0)
            PlaylistBox.SelectedIndex = viewIndex;

        UpdateQueuePreview();

        PositionSlider.Value = 0;
        PositionSlider.Maximum = _mp.Length > 0 ? _mp.Length : 1;

        if (!_restoredLastPosition && _settings.LastIndex == _index && _settings.LastPosition > 0)
        {
            long pos = _settings.LastPosition;
            if (pos > 0)
            {
                _mp.Time = pos;
                PositionSlider.Maximum = _mp.Length > 0 ? _mp.Length : 1;
                PositionSlider.Value = Math.Min(PositionSlider.Maximum, pos);
            }
            _restoredLastPosition = true;
        }
    }

    void PlayPause(object? s, RoutedEventArgs e)
    {
        if (!_mp.IsPlaying && _mp.Media == null && _playlist.Count > 0)
        {
            _index = _index >= 0 ? _index : 0;
            PlayIndex();
            return;
        }

        if (_mp.IsPlaying)
        {
            _mp.Pause();
            PlayPauseIcon.Text = "▶";
        }
        else
        {
            _mp.Play();
            PlayPauseIcon.Text = "⏸";
        }
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

            await CrossfadeTo(next);
        }
        else
        {
            int next = (_index + 1) % _playlist.Count;
            await CrossfadeTo(next);
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
                await CrossfadeTo(prevIndex);
            }
            else
            {
                int next = (_index - 1 + _playlist.Count) % _playlist.Count;
                await CrossfadeTo(next);
            }
        }
        else
        {
            int next = (_index - 1 + _playlist.Count) % _playlist.Count;
            await CrossfadeTo(next);
        }
    }

    void VolumeChanged(object? s, RangeBaseValueChangedEventArgs e)
        => _mp.Volume = (int)e.NewValue;

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
                _playlist.Add(new TrackModel(path));
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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _mp.Stop();
        _capture?.Stop();
        _positionUpdateTimer?.Stop();

        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowX = Position.X;
        _settings.WindowY = Position.Y;
        _settings.Volume = (int)VolumeSlider.Value;
        _settings.Playlist = _playlist.Select(x => x.Path).ToList();
        _settings.LastIndex = _index;
        _settings.LastPosition = (long)PositionSlider.Value;

        SettingsService.Save(_settings);
        base.OnClosing(e);
    }
}