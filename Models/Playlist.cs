using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicPlayerApp.Models;

public class Playlist : INotifyPropertyChanged
{
    private string _id;
    private string _name;
    private string _description;
    private List<string> _trackPaths = new();
    private DateTime _createdAt;
    private bool _isFavorite;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Playlist(string name, string? description = null)
    {
        _id = Guid.NewGuid().ToString();
        _name = name;
        _description = description;
        _createdAt = DateTime.Now;
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

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public List<string> TrackPaths
    {
        get => _trackPaths;
        set => SetProperty(ref _trackPaths, value);
    }

    public int TrackCount => _trackPaths.Count;

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public void AddTrack(string trackPath)
    {
        if (!_trackPaths.Contains(trackPath))
        {
            _trackPaths.Add(trackPath);
            OnPropertyChanged(nameof(TrackCount));
        }
    }

    public void RemoveTrack(string trackPath)
    {
        if (_trackPaths.Remove(trackPath))
        {
            OnPropertyChanged(nameof(TrackCount));
        }
    }

    public bool ContainsTrack(string trackPath) => _trackPaths.Contains(trackPath);
}