using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using MusicPlayerApp.Models;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.Views;

public class TopSongItem
{
    public string Rank { get; set; } = string.Empty;
    public Track Track { get; set; } = null!;
    public int PlayCount { get; set; }
    public int TotalMinutes { get; set; }
}

public partial class StatsView : UserControl
{
    public StatsView()
    {
        InitializeComponent();
    }

    public void LoadStats(string email, DateTime accountCreated, List<Track> tracks, Dictionary<Track, int> playHistory)
    {
        // Account Info
        EmailText.Text = email;
        MemberSinceText.Text = accountCreated.ToString("MMM yyyy");

        // Load listening sessions for accurate stats
        var sessions = StatsService.LoadListeningSessions();
        
        // Calculate actual listening time from sessions
        var totalMinutes = (int)sessions.Sum(s => s.Duration.TotalMinutes);
        var totalHours = totalMinutes / 60.0;
        var tracksPlayed = sessions.Count(s => s.Completed); // Only count completed plays
        var favoriteCount = tracks.Count(t => t.IsFavorite);

        // Update UI
        TotalMinutesText.Text = totalMinutes.ToString("N0");
        HoursText.Text = $"{totalHours:F1} hours";
        TracksPlayedText.Text = tracksPlayed.ToString("N0");
        FavoriteCountText.Text = favoriteCount.ToString();

        // Calculate top songs by actual listening time
        var trackListeningTime = sessions
            .GroupBy(s => s.TrackPath)
            .Select(g => new
            {
                Path = g.Key,
                TotalMinutes = (int)g.Sum(s => s.Duration.TotalMinutes),
                PlayCount = g.Count(s => s.Completed)
            })
            .OrderByDescending(x => x.TotalMinutes)
            .Take(10)
            .ToList();

        // Match with Track objects
        var topSongs = trackListeningTime
            .Select((item, index) =>
            {
                var track = tracks.FirstOrDefault(t => t.Path == item.Path);
                if (track == null) return null;
                
                return new TopSongItem
                {
                    Rank = (index + 1).ToString(),
                    Track = track,
                    PlayCount = item.PlayCount,
                    TotalMinutes = item.TotalMinutes
                };
            })
            .Where(item => item != null)
            .ToList();

        if (topSongs.Any())
        {
            TopSongsItemsControl.ItemsSource = topSongs;
            EmptyState.IsVisible = false;
        }
        else
        {
            EmptyState.IsVisible = true;
        }
    }
}