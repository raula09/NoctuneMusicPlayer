using System;
using System.IO;
using LiteDB;

namespace MusicPlayerApp.Services;

public class TrackLyrics
{
    public ObjectId Id { get; set; }
    public string TrackPath { get; set; } = "";
    public string? LyricsData { get; set; }
    public DateTime LastUpdated { get; set; }
}

public static class LyricsService
{
    private static string GetDbPath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Noctune");

        Directory.CreateDirectory(basePath);
        return Path.Combine(basePath, "lyrics.db");
    }

    public static void SaveLyrics(string trackPath, string? lyricsData)
    {
        try
        {
            // DEBUG: Show stack trace to see who's calling this
            Console.WriteLine($"📝 SaveLyrics called for: {Path.GetFileName(trackPath)}");
            Console.WriteLine($"   Stack trace: {Environment.StackTrace.Split('\n')[2].Trim()}");
        
            using var db = new LiteDatabase(GetDbPath());
            var lyrics = db.GetCollection<TrackLyrics>("lyrics");
        
            lyrics.EnsureIndex(x => x.TrackPath);
        
            var existing = lyrics.FindOne(l => l.TrackPath == trackPath);
        
            if (existing != null)
            {
                existing.LyricsData = lyricsData;
                existing.LastUpdated = DateTime.Now;
                lyrics.Update(existing);
            }
            else
            {
                var trackLyrics = new TrackLyrics
                {
                    TrackPath = trackPath,
                    LyricsData = lyricsData,
                    LastUpdated = DateTime.Now
                };
            
                lyrics.Insert(trackLyrics);
            }
        
            Console.WriteLine($"✓ Saved lyrics for: {Path.GetFileName(trackPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error saving lyrics: {ex.Message}");
        }
    }
    public static string? GetLyrics(string trackPath)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var lyrics = db.GetCollection<TrackLyrics>("lyrics");
            
            lyrics.EnsureIndex(x => x.TrackPath);
            
            var existing = lyrics.FindOne(l => l.TrackPath == trackPath);
            return existing?.LyricsData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting lyrics: {ex.Message}");
            return null;
        }
    }

    public static bool HasLyrics(string trackPath)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var lyrics = db.GetCollection<TrackLyrics>("lyrics");
            
            lyrics.EnsureIndex(x => x.TrackPath);
            
            var existing = lyrics.FindOne(l => l.TrackPath == trackPath);
            return existing != null && !string.IsNullOrEmpty(existing.LyricsData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error checking lyrics: {ex.Message}");
            return false;
        }
    }

    public static void DeleteLyrics(string trackPath)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var lyrics = db.GetCollection<TrackLyrics>("lyrics");
            
            lyrics.EnsureIndex(x => x.TrackPath);
            
            var existing = lyrics.FindOne(l => l.TrackPath == trackPath);
            
            if (existing != null)
            {
                lyrics.Delete(existing.Id);
                Console.WriteLine($"✓ Deleted lyrics for: {Path.GetFileName(trackPath)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error deleting lyrics: {ex.Message}");
        }
    }

    public static void CleanupMissingTracks()
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var lyrics = db.GetCollection<TrackLyrics>("lyrics");
            
            var allLyrics = lyrics.FindAll();
            
            foreach (var lyric in allLyrics)
            {
                if (!File.Exists(lyric.TrackPath))
                {
                    lyrics.Delete(lyric.Id);
                    Console.WriteLine($"✓ Cleaned up lyrics for missing track: {lyric.TrackPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error cleaning up lyrics: {ex.Message}");
        }
    }
}