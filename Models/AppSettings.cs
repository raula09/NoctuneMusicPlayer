using System.Collections.Generic;

namespace MusicPlayerApp.Models
{
    public class AppSettings
    {
        public double WindowWidth { get; set; } = 520;
        public double WindowHeight { get; set; } = 500;
        public double WindowX { get; set; } = 100;
        public double WindowY { get; set; } = 100;
        public int Volume { get; set; } = 80;
        public List<string> Playlist { get; set; } = new();
        public int LastIndex { get; set; } = -1;
        public long LastPosition { get; set; } = 0;
    }
}
