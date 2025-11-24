using System;

namespace MusicPlayerApp.Models;

public class ListeningSession
{
    public string TrackPath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Completed { get; set; } // True if user listened to 90%+ of the song
}