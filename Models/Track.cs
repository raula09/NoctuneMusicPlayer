using Avalonia.Media.Imaging;
using LiteDB;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicPlayerApp.Models;

public class Track : INotifyPropertyChanged
{
    private ObjectId _id;
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
    private string? _lyricsData;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Track()
    {
        _id = ObjectId.NewObjectId();
        _path = string.Empty;
        _dateAdded = DateTime.Now;
        _metadataLoaded = false;
    }

    public Track(string path)
    {
        _id = ObjectId.NewObjectId();
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

    [BsonId]
    public ObjectId Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
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
        set => SetProperty(ref _title, value);
    }

    public string Artist
    {
        get
        {
            EnsureMetadataLoaded();
            return _artist ?? "Unknown Artist";
        }
        set => SetProperty(ref _artist, value);
    }

    public string Album
    {
        get
        {
            EnsureMetadataLoaded();
            return _album ?? "Unknown Album";
        }
        set => SetProperty(ref _album, value);
    }

    public TimeSpan Duration
    {
        get
        {
            EnsureMetadataLoaded();
            return _duration;
        }
        set => SetProperty(ref _duration, value);
    }

    [BsonIgnore]
    public Bitmap? Art
    {
        get
        {
            EnsureMetadataLoaded();
            return _art;
        }
        set => SetProperty(ref _art, value);
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

    [BsonIgnore]
    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string? LyricsData
    {
        get => _lyricsData;
        set => SetProperty(ref _lyricsData, value);
    }

    [BsonIgnore]
    public bool HasLyrics => !string.IsNullOrEmpty(_lyricsData);

    [BsonIgnore]
    public string DateAddedString => _dateAdded.ToString("yyyy-MM-dd");
    
    [BsonIgnore]
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

    public void LoadMetadata()
    {
        EnsureMetadataLoaded();
    }
}