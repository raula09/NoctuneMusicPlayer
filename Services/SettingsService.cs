using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services;

public static class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Noctune"
    );

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");
    private static readonly string TokenPath = Path.Combine(SettingsFolder, "token.txt");

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public static AppSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return null;
        }
    }

    public static void SaveToken(string token)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(TokenPath, token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving token: {ex.Message}");
        }
    }

    public static string? LoadToken()
    {
        try
        {
            if (!File.Exists(TokenPath))
                return null;

            var token = File.ReadAllText(TokenPath);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading token: {ex.Message}");
            return null;
        }
    }

    public static void ClearToken()
    {
        try
        {
            if (File.Exists(TokenPath))
            {
                File.Delete(TokenPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing token: {ex.Message}");
        }
    }
}