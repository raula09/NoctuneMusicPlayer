using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services;

public static class StatsService
{
    private static string GetStatsPath(string filename)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicPlayerApp",
            "Stats");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, filename);
    }

    // Save listening sessions (new method)
    public static void SaveListeningSessions(List<ListeningSession> sessions)
    {
        try
        {
            var path = GetStatsPath("listening_sessions.json");
            var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    // Load listening sessions (new method)
    public static List<ListeningSession> LoadListeningSessions()
    {
        try
        {
            var path = GetStatsPath("listening_sessions.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ListeningSession>>(json) ?? new List<ListeningSession>();
            }
        }
        catch { }
        return new List<ListeningSession>();
    }

    // Keep old methods for backward compatibility
    public static Dictionary<Track, int> LoadPlayHistory()
    {
        try
        {
            var path = GetStatsPath("play_history.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                return new Dictionary<Track, int>();
            }
        }
        catch { }
        return new Dictionary<Track, int>();
    }

    public static void SavePlayHistory(Dictionary<Track, int> history, IEnumerable<Track> allTracks)
    {
        try
        {
            var path = GetStatsPath("play_history.json");
            var simplified = history.ToDictionary(kvp => kvp.Key.Path, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(simplified, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public static void SaveUserData(string email, DateTime accountCreated)
    {
        try
        {
            var path = GetStatsPath("user_data.json");
            var data = new { Email = email, AccountCreated = accountCreated };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public static string LoadUserEmail()
    {
        try
        {
            var path = GetStatsPath("user_data.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (data != null && data.ContainsKey("Email"))
                    return data["Email"].GetString() ?? "user@example.com";
            }
        }
        catch { }
        return "user@example.com";
    }

    public static DateTime LoadAccountCreatedDate()
    {
        try
        {
            var path = GetStatsPath("user_data.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (data != null && data.ContainsKey("AccountCreated"))
                    return data["AccountCreated"].GetDateTime();
            }
        }
        catch { }
        return DateTime.Now;
    }
}