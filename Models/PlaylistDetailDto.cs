using System;
using System.Collections.Generic;

namespace MusicPlayerApp.Models;

public class PlaylistDetailDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> TrackPaths { get; set; } = new();
    public int TrackCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsFavorite { get; set; }
}
