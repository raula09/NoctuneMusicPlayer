using System;
using System.ComponentModel;
using System.IO;
using Avalonia.Media.Imaging;
using TagLib;
using TrackModel = MusicPlayerApp.Models.Track;

namespace MusicPlayerApp.Models
{
    public class Track : INotifyPropertyChanged
    {
        public string Path { get; }
        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public Bitmap? Art { get; }
        public TimeSpan Duration { get; }
        public string DurationString => Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss");

        public DateTime DateAdded { get; }
        public string DateAddedString => DateAdded.ToString("yyyy-MM-dd");

        int _index;
        public int Index
        {
            get { return _index; }
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        bool _isFavorite;
        public bool IsFavorite
        {
            get { return _isFavorite; }
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public Track(string path)
        {
            Path = path;

            var f = TagLib.File.Create(path);

            if (string.IsNullOrWhiteSpace(f.Tag.Title))
                Title = System.IO.Path.GetFileNameWithoutExtension(path);
            else
                Title = f.Tag.Title;

            Artist = string.IsNullOrWhiteSpace(f.Tag.FirstPerformer) ? "Unknown Artist" : f.Tag.FirstPerformer;
            Album = string.IsNullOrWhiteSpace(f.Tag.Album) ? "Unknown Album" : f.Tag.Album;

            if (f.Tag.Pictures != null && f.Tag.Pictures.Length > 0)
            {
                var pic = f.Tag.Pictures[0];
                using var ms = new MemoryStream(pic.Data.Data);
                Art = new Bitmap(ms);
            }

            Duration = f.Properties.Duration;

            try
            {
                DateAdded = System.IO.File.GetCreationTime(path);
            }
            catch
            {
                DateAdded = DateTime.Now;
            }
        }

        public override string ToString()
        {
            return Artist + " – " + Title;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void OnPropertyChanged(string propertyName)
        {
            var h = PropertyChanged;
            if (h != null)
                h(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
