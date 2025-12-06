using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services;

public class LikedTrack
{
    public ObjectId Id { get; set; }
    public string TrackPath { get; set; } = "";
    public DateTime LikedAt { get; set; }
}

public static class LikesService
{
    private static string GetDbPath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Noctune");

        Directory.CreateDirectory(basePath);
        return Path.Combine(basePath, "likes.db");
    }

    public static bool IsLiked(string trackPath)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            likes.EnsureIndex(x => x.TrackPath);
            
            return likes.FindOne(l => l.TrackPath == trackPath) != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error checking if track is liked: {ex.Message}");
            return false;
        }
    }

    public static void ToggleLike(Track track)
    {
        try
        {
            Console.WriteLine($"🔄 ToggleLike called for: {System.IO.Path.GetFileName(track.Path)}");
        
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
        
            likes.EnsureIndex(x => x.TrackPath);
        
            var existing = likes.FindOne(l => l.TrackPath == track.Path);
        
            if (existing != null)
            {
                likes.Delete(existing.Id);
                Console.WriteLine($"✗ Unliked: {System.IO.Path.GetFileName(track.Path)}");
            }
            else
            {
                var liked = new LikedTrack
                {
                    TrackPath = track.Path,
                    LikedAt = DateTime.Now
                };
            
                likes.Insert(liked);
                Console.WriteLine($"♥ Liked: {System.IO.Path.GetFileName(track.Path)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error toggling like: {ex.Message}");
        }
    }
    

    public static void Like(Track track)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            likes.EnsureIndex(x => x.TrackPath);
            
            var existing = likes.FindOne(l => l.TrackPath == track.Path);
            
            if (existing == null)
            {
                var liked = new LikedTrack
                {
                    TrackPath = track.Path,
                    LikedAt = DateTime.Now
                };
                
                likes.Insert(liked);
                Console.WriteLine($"♥ Liked: {System.IO.Path.GetFileName(track.Path)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error liking track: {ex.Message}");
        }
    }

    public static void Unlike(string trackPath)
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            likes.EnsureIndex(x => x.TrackPath);
            
            var existing = likes.FindOne(l => l.TrackPath == trackPath);
            
            if (existing != null)
            {
                likes.Delete(existing.Id);
                Console.WriteLine($"✗ Unliked: {Path.GetFileName(trackPath)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error unliking track: {ex.Message}");
        }
    }

    public static List<Track> GetAllLikedTracks()
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            return likes.FindAll()
                .OrderByDescending(l => l.LikedAt)
                .Where(l => File.Exists(l.TrackPath))
                .Select(l => {
                    var track = new Track(l.TrackPath)
                    {
                        DateAdded = l.LikedAt
                    };
                    track.LoadMetadata();
                    return track;
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting liked tracks: {ex.Message}");
            return new List<Track>();
        }
    }

    public static List<string> GetAllLikedTrackPaths()
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            return likes.FindAll()
                .OrderByDescending(l => l.LikedAt)
                .Select(l => l.TrackPath)
                .Where(path => File.Exists(path))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting liked tracks: {ex.Message}");
            return new List<string>();
        }
    }

    public static int GetLikedTracksCount()
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            return likes.Count();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error getting liked tracks count: {ex.Message}");
            return 0;
        }
    }

    public static void CleanupMissingTracks()
    {
        try
        {
            using var db = new LiteDatabase(GetDbPath());
            var likes = db.GetCollection<LikedTrack>("likes");
            
            var allLikes = likes.FindAll().ToList();
            
            foreach (var like in allLikes)
            {
                if (!File.Exists(like.TrackPath))
                {
                    likes.Delete(like.Id);
                    Console.WriteLine($"✓ Cleaned up missing track: {like.TrackPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error cleaning up liked tracks: {ex.Message}");
        }
    }
}