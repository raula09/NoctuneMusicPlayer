using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicPlayerApp.Models;

public class Track : INotifyPropertyChanged
{
    private string _path;
    private string? _title;
    private string? _artist;
    private string? _album;
    private TimeSpan _duration;
    private Bitmap? _art;
    private DateTime _dateAdded;
    private bool _isFavorite;
    private int _index;
    private bool _metadataLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Track(string path)
    {
        _path = path;
        _dateAdded = DateTime.Now;
        _metadataLoaded = false;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string Title
    {
        get
        {
            EnsureMetadataLoaded();
            return _title ?? System.IO.Path.GetFileNameWithoutExtension(_path);
        }
        private set => SetProperty(ref _title, value);
    }

    public string Artist
    {
        get
        {
            EnsureMetadataLoaded();
            return _artist ?? "Unknown Artist";
        }
        private set => SetProperty(ref _artist, value);
    }

    public string Album
    {
        get
        {
            EnsureMetadataLoaded();
            return _album ?? "Unknown Album";
        }
        private set => SetProperty(ref _album, value);
    }

    public TimeSpan Duration
    {
        get
        {
            EnsureMetadataLoaded();
            return _duration;
        }
        private set => SetProperty(ref _duration, value);
    }

    public Bitmap? Art
    {
        get
        {
            EnsureMetadataLoaded();
            return _art;
        }
        private set => SetProperty(ref _art, value);
    }

    public DateTime DateAdded
    {
        get => _dateAdded;
        set => SetProperty(ref _dateAdded, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string DateAddedString => _dateAdded.ToString("yyyy-MM-dd");
    public string DurationString => _duration.ToString(@"m\:ss");

    private void EnsureMetadataLoaded()
    {
        if (_metadataLoaded) return;
        
        try
        {
            using var file = TagLib.File.Create(_path);
            _title = file.Tag.Title;
            _artist = file.Tag.FirstPerformer;
            _album = file.Tag.Album;
            _duration = file.Properties.Duration;

            // Load album art (this is the expensive part)
            if (file.Tag.Pictures.Length > 0)
            {
                var pic = file.Tag.Pictures[0];
                using var ms = new MemoryStream(pic.Data.Data);
                _art = new Bitmap(ms);
            }
        }
        catch { }

        _metadataLoaded = true;
    }

    // Force metadata load for when you're actually playing
    public void LoadMetadata()
    {
        EnsureMetadataLoaded();
    }
}