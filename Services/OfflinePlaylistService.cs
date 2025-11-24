using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using MusicPlayerApp.Models;

namespace MusicPlayerApp.Services;

public class OfflinePlaylistService
{
    private readonly string _dbPath;
    private readonly string _coversPath;

    public OfflinePlaylistService()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Noctune");

        Directory.CreateDirectory(basePath);

        _dbPath = Path.Combine(basePath, "playlists.db");

        _coversPath = Path.Combine(basePath, "covers");
        Directory.CreateDirectory(_coversPath);
        Console.WriteLine("LITEDB PATH = " + _dbPath);

    }

    private LiteDatabase GetDb()
    {
        return new LiteDatabase(_dbPath);
    }

    // -------------------------
    // LOAD ALL PLAYLISTS
    // -------------------------
    public List<PlaylistDto> GetPlaylists()
    {
        using var db = GetDb();
        var col = db.GetCollection<PlaylistDto>("playlists");
        return col.FindAll().ToList();
    }

    // -------------------------
    // CREATE / UPDATE PLAYLIST
    // -------------------------
    public PlaylistDto SavePlaylist(PlaylistDto dto)
    {
        using var db = GetDb();
        var col = db.GetCollection<PlaylistDto>("playlists");

        col.Upsert(dto);
        return dto;
    }

    // -------------------------
    // DELETE PLAYLIST
    // -------------------------
    public void DeletePlaylist(string id)
    {
        using var db = GetDb();
        var col = db.GetCollection<PlaylistDto>("playlists");

        var pl = col.FindById(id);
        if (pl != null)
        {
            if (!string.IsNullOrWhiteSpace(pl.ImagePath) && File.Exists(pl.ImagePath))
                File.Delete(pl.ImagePath);
        }

        col.Delete(id);
    }

    // -------------------------
    // SAVE A COVER IMAGE
    // -------------------------
    public string SaveCoverImage(string sourceFilePath)
    {
        var ext = Path.GetExtension(sourceFilePath);
        var fileName = Guid.NewGuid().ToString() + ext;

        var dest = Path.Combine(_coversPath, fileName);

        File.Copy(sourceFilePath, dest, overwrite: true);

        return dest;
    }

    // -------------------------
    // ADD A TRACK TO A PLAYLIST
    // -------------------------
    public void AddTrack(string playlistId, string trackPath)
    {
        using var db = GetDb();
        var col = db.GetCollection<PlaylistDto>("playlists");

        var p = col.FindById(playlistId);
        if (p == null) return;

        if (!p.TrackPaths.Contains(trackPath))
        {
            p.TrackPaths.Add(trackPath);
            p.TrackCount = p.TrackPaths.Count;
            col.Update(p);
        }
    }

    // -------------------------
    // REMOVE TRACK FROM PLAYLIST
    // -------------------------
    public void RemoveTrack(string playlistId, string trackPath)
    {
        using var db = GetDb();
        var col = db.GetCollection<PlaylistDto>("playlists");

        var p = col.FindById(playlistId);
        if (p == null) return;

        if (p.TrackPaths.Contains(trackPath))
        {
            p.TrackPaths.Remove(trackPath);
            p.TrackCount = p.TrackPaths.Count;
            col.Update(p);
        }
    }
}
