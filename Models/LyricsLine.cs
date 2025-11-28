using System;
namespace MusicPlayerApp.Models;

public class LyricsLine
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsInstrumental { get; set; } = false;
}