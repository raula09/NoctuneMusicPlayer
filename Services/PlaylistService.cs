using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services;

public static class PlaylistService
{
    private static readonly string PlaylistsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicPlayerApp",
        "Playlists");

    static PlaylistService()
    {
        Directory.CreateDirectory(PlaylistsFolder);
    }

    public static void SavePlaylists(List<Playlist> playlists)
    {
        try
        {
            var data = playlists.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.TrackPaths,
                p.CreatedAt,
                p.IsFavorite
            }).ToList();

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(PlaylistsFolder, "playlists.json");
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving playlists: {ex.Message}");
        }
    }

    public static List<Playlist> LoadPlaylists()
    {
        try
        {
            var path = Path.Combine(PlaylistsFolder, "playlists.json");
            if (!File.Exists(path))
                return new List<Playlist>();

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

            var playlists = new List<Playlist>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    var playlist = new Playlist(
                        item["Name"].GetString() ?? "Untitled",
                        item.ContainsKey("Description") ? item["Description"].GetString() : null
                    )
                    {
                        Id = item["Id"].GetString() ?? Guid.NewGuid().ToString(),
                        CreatedAt = item["CreatedAt"].GetDateTime(),
                        IsFavorite = item.ContainsKey("IsFavorite") && item["IsFavorite"].GetBoolean()
                    };

                    if (item.ContainsKey("TrackPaths"))
                    {
                        var tracks = item["TrackPaths"].EnumerateArray()
                            .Select(t => t.GetString() ?? "")
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                        playlist.TrackPaths = tracks;
                    }

                    playlists.Add(playlist);
                }
            }

            return playlists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading playlists: {ex.Message}");
            return new List<Playlist>();
        }
    }

    public static void DeletePlaylist(string playlistId)
    {
        try
        {
            var playlists = LoadPlaylists();
            playlists.RemoveAll(p => p.Id == playlistId);
            SavePlaylists(playlists);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting playlist: {ex.Message}");
        }
    }
}
