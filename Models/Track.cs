using Avalonia.Media.Imaging;
using LiteDB;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MusicPlayerApp.Services;

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
    private bool _lyricsLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Track()
    {
        _id = ObjectId.NewObjectId();
        _path = string.Empty;
        _dateAdded = DateTime.Now;
        _metadataLoaded = false;
        _lyricsLoaded = false;
    }

    public Track(string path)
    {
        _id = ObjectId.NewObjectId();
        _path = path;
        _dateAdded = DateTime.Now;
        _metadataLoaded = false;
        _lyricsLoaded = false;
        
        // Load lyrics from permanent storage
        LoadLyricsFromDatabase();
    }

    private void LoadLyricsFromDatabase()
    {
        if (_lyricsLoaded) return;
    
        try
        {
            var savedLyrics = LyricsService.GetLyrics(_path);
            if (savedLyrics != null)
            {
                // ✅ Set backing field directly - DON'T use the property setter
                _lyricsData = savedLyrics;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading lyrics: {ex.Message}");
        }
    
        _lyricsLoaded = true;
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
        get
        {
            if (!_lyricsLoaded)
            {
                LoadLyricsFromDatabase();
            }
            return _lyricsData;
        }
        set 
        { 
            Console.WriteLine($"🎵 LyricsData setter called for: {System.IO.Path.GetFileName(_path)}");
            Console.WriteLine($"   Old value length: {_lyricsData?.Length ?? 0}");
            Console.WriteLine($"   New value length: {value?.Length ?? 0}");
        
            if (SetProperty(ref _lyricsData, value))
            {
                Console.WriteLine($"   → Property changed, saving to LyricsService");
                LyricsService.SaveLyrics(_path, value);
                _lyricsLoaded = true;
                OnPropertyChanged(nameof(HasLyrics));
            }
            else
            {
                Console.WriteLine($"   → Property unchanged, not saving");
            }
        }
    }

    [BsonIgnore]
    public bool HasLyrics
    {
        get
        {
            if (!_lyricsLoaded)
            {
                LoadLyricsFromDatabase();
            }
            return !string.IsNullOrEmpty(_lyricsData);
        }
    }

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

    public bool IsLiked
    {
        get => LikesService.IsLiked(Path);
        set
        {
            if (value)
                LikesService.Like(this);
            else
                LikesService.Unlike(Path);
            
            OnPropertyChanged(nameof(IsLiked));
        }
    }

    public void ToggleLike()
    {
        LikesService.ToggleLike(this);
        OnPropertyChanged(nameof(IsLiked));
    }

    public void LoadMetadata()
    {
        EnsureMetadataLoaded();
    }
}