using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicPlayerApp.Models;

namespace MusicPlayerApp
{
    public partial class LyricsDisplay : UserControl
    {
        private List<LyricsLine> _lines = new List<LyricsLine>();
        private ScrollViewer? _scrollViewer;
        private StackPanel? _lyricsPanel;
        private StackPanel? _headerPanel;
        private Button? _loadButton;
        private int _currentLineIndex = -1;
        private int _lastScrolledIndex = -1;
        private int _dotAnimationFrame = 0;
        private DispatcherTimer? _dotAnimationTimer;
        private Track? _currentTrack;  // Track the current track

        public event EventHandler<string>? LyricsLoaded;
        public event EventHandler<TimeSpan>? SeekRequested;

        public LyricsDisplay()
        {
            InitializeComponent();
            SetupControls();
            InitializeDotAnimation();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupControls()
        {
            _scrollViewer = this.FindControl<ScrollViewer>("LyricsScrollViewer");
            _lyricsPanel = this.FindControl<StackPanel>("LyricsPanel");
            _headerPanel = this.FindControl<StackPanel>("HeaderPanel");
            _loadButton = this.FindControl<Button>("LoadLyricsButton");
        }

        private void InitializeDotAnimation()
        {
            _dotAnimationTimer = new DispatcherTimer 
            { 
                Interval = TimeSpan.FromMilliseconds(500) 
            };
            _dotAnimationTimer.Tick += (_, _) =>
            {
                _dotAnimationFrame = (_dotAnimationFrame + 1) % 4;
            };
            _dotAnimationTimer.Start();
        }

        public void SetCurrentTrack(Track? track)
        {
            _currentTrack = track;
            
            // Load lyrics from track if available
            if (_currentTrack != null && !string.IsNullOrEmpty(_currentTrack.LyricsData))
            {
                LoadLyricsFromString(_currentTrack.LyricsData);
            }
            else
            {
                ClearLyrics();
            }
        }

        private async void LoadLyricsButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Lyrics File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Lyrics Files")
                    {
                        Patterns = new[] { "*.lrc", "*.txt" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                string lyricsContent = await reader.ReadToEndAsync();

                LoadLyricsFromString(lyricsContent);
                
                // Save lyrics to current track
                if (_currentTrack != null)
                {
                    _currentTrack.LyricsData = lyricsContent;
                }
                
                LyricsLoaded?.Invoke(this, lyricsContent);
            }
        }

        public void LoadLyricsFromString(string lrcContent)
        {
            _lines.Clear();
            _lyricsPanel?.Children.Clear();
            _currentLineIndex = -1;
            _lastScrolledIndex = -1;

            if (string.IsNullOrWhiteSpace(lrcContent))
            {
                ShowNoLyricsMessage();
                return;
            }

            try
            {
                var lines = lrcContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var lrcPattern = @"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)";
                var parsedLines = new List<LyricsLine>();

                foreach (var line in lines)
                {
                    var match = Regex.Match(line, lrcPattern);
                    if (match.Success)
                    {
                        int minutes = int.Parse(match.Groups[1].Value);
                        int seconds = int.Parse(match.Groups[2].Value);
                        int milliseconds = int.Parse(match.Groups[3].Value.PadRight(3, '0'));
                        string text = match.Groups[4].Value.Trim();

                        bool isInstrumental = string.IsNullOrWhiteSpace(text) || 
                                            text == "♪" || 
                                            text == "..." ||
                                            text == "🎵";

                        parsedLines.Add(new LyricsLine
                        {
                            StartTime = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                            Text = text,
                            IsInstrumental = isInstrumental
                        });
                    }
                }

                for (int i = 0; i < parsedLines.Count; i++)
                {
                    if (i < parsedLines.Count - 1)
                    {
                        parsedLines[i].EndTime = parsedLines[i + 1].StartTime;
                    }
                    else
                    {
                        parsedLines[i].EndTime = parsedLines[i].StartTime.Add(TimeSpan.FromSeconds(5));
                    }
                }

                _lines = parsedLines;
                BuildLyricsUI();
                
                if (_headerPanel != null)
                {
                    _headerPanel.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading lyrics: {ex.Message}");
                ShowNoLyricsMessage();
            }
        }

        private void ShowNoLyricsMessage()
        {
            _lyricsPanel?.Children.Clear();
            _lyricsPanel?.Children.Add(new TextBlock
            {
                Text = "No lyrics available\n\nClick 'Upload Lyrics' to add lyrics",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.Parse("#6A6A6A")),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 60)
            });
            
            if (_headerPanel != null)
            {
                _headerPanel.IsVisible = true;
            }
        }

        private void BuildLyricsUI()
        {
            if (_lyricsPanel == null) return;

            _lyricsPanel.Children.Clear();
            _lyricsPanel.Children.Add(new Border { Height = 200 });

            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                
                if (line.IsInstrumental)
                {
                    var dotsPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 8,
                        Margin = new Thickness(16, 12),
                        Tag = i,
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };

                    for (int d = 0; d < 3; d++)
                    {
                        var dot = new Border
                        {
                            Width = 8,
                            Height = 8,
                            CornerRadius = new CornerRadius(4),
                            Background = new SolidColorBrush(Color.Parse("#6A6A6A")),
                            Tag = $"dot_{d}"
                        };
                        dotsPanel.Children.Add(dot);
                    }

                    dotsPanel.PointerPressed += LyricsLine_Clicked;

                    _lyricsPanel.Children.Add(dotsPanel);
                }
                else
                {
                    var textBlock = new TextBlock
                    {
                        Text = line.Text,
                        FontSize = 24,
                        FontWeight = FontWeight.SemiBold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(16, 12),
                        Tag = i,
                        Foreground = new SolidColorBrush(Color.Parse("#6A6A6A")),
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };

                    textBlock.PointerPressed += LyricsLine_Clicked;
                    
                    // Hover effect
                    textBlock.PointerEntered += (s, e) =>
                    {
                        if (s is TextBlock tb && tb.Tag is int idx && idx != _currentLineIndex)
                        {
                            tb.Opacity = 0.8;
                        }
                    };
                    
                    textBlock.PointerExited += (s, e) =>
                    {
                        if (s is TextBlock tb && tb.Tag is int idx && idx != _currentLineIndex)
                        {
                            if (idx < _currentLineIndex)
                                tb.Opacity = 0.6;
                            else
                                tb.Opacity = 0.5;
                        }
                    };

                    _lyricsPanel.Children.Add(textBlock);
                }
            }

            _lyricsPanel.Children.Add(new Border { Height = 200 });
        }

        private void LyricsLine_Clicked(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.Tag is int lineIndex)
            {
                if (lineIndex >= 0 && lineIndex < _lines.Count)
                {
                    var seekTime = _lines[lineIndex].StartTime;
                    SeekRequested?.Invoke(this, seekTime);
                    e.Handled = true;
                }
            }
        }

        public void UpdatePosition(TimeSpan currentPosition)
        {
            if (_lines.Count == 0) return;

            int newLineIndex = -1;
            
            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                
                if (currentPosition >= line.StartTime && currentPosition < line.EndTime)
                {
                    newLineIndex = i;
                    break; 
                }
            }

            if (newLineIndex != _currentLineIndex)
            {
                _currentLineIndex = newLineIndex;
                UpdateLyricsDisplay();
            }
        }

        private void UpdateLyricsDisplay()
        {
            if (_lyricsPanel == null || _lines.Count == 0) return;

            Control? activeControl = null;

            for (int i = 1; i < _lyricsPanel.Children.Count - 1; i++)
            {
                var child = _lyricsPanel.Children[i];
                
                if (child is TextBlock textBlock && textBlock.Tag is int lineIndex)
                {
                    if (lineIndex < 0 || lineIndex >= _lines.Count)
                        continue;

                    bool isActive = (lineIndex == _currentLineIndex);

                    if (isActive)
                    {
                        textBlock.Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"));
                        textBlock.FontSize = 28;
                        textBlock.FontWeight = FontWeight.Bold;
                        textBlock.Opacity = 1.0;
                        activeControl = textBlock;
                    }
                    else if (lineIndex < _currentLineIndex)
                    {
                        textBlock.Foreground = new SolidColorBrush(Color.Parse("#4A4A4A"));
                        textBlock.FontSize = 24;
                        textBlock.FontWeight = FontWeight.SemiBold;
                        textBlock.Opacity = 0.6;
                    }
                    else
                    {
                        textBlock.Foreground = new SolidColorBrush(Color.Parse("#6A6A6A"));
                        textBlock.FontSize = 24;
                        textBlock.FontWeight = FontWeight.SemiBold;
                        textBlock.Opacity = 0.5;
                    }
                }
                else if (child is StackPanel dotsPanel && dotsPanel.Tag is int dotLineIndex)
                {
                    if (dotLineIndex < 0 || dotLineIndex >= _lines.Count)
                        continue;

                    bool isActive = (dotLineIndex == _currentLineIndex);

                    if (isActive)
                    {
                        activeControl = dotsPanel;
                        
                        var dots = dotsPanel.Children.OfType<Border>().ToList();
                        for (int d = 0; d < dots.Count; d++)
                        {
                            var fillProgress = (_dotAnimationFrame > d) ? 1.0 : 0.5;
                            dots[d].Background = new SolidColorBrush(
                                Color.Parse(fillProgress > 0.9 ? "#FFFFFF" : "#6A6A6A"));
                            dots[d].Opacity = fillProgress;
                        }
                    }
                    else
                    {
                        var dots = dotsPanel.Children.OfType<Border>().ToList();
                        foreach (var dot in dots)
                        {
                            dot.Background = new SolidColorBrush(Color.Parse("#4A4A4A"));
                            dot.Opacity = 0.5;
                        }
                    }
                }
            }

            if (activeControl != null)
            {
                ScrollToControl(activeControl);
            }
        }

        private void ScrollToControl(Control control)
        {
            if (_scrollViewer == null) return;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await Task.Delay(10);
                    
                    _scrollViewer.UpdateLayout();
                    control.UpdateLayout();

                    var bounds = control.Bounds;
                    var scrollBounds = _scrollViewer.Bounds;
                    
                    if (scrollBounds.Height > 0)
                    {
                        var transform = control.TransformToVisual(_lyricsPanel);
                        if (transform != null)
                        {
                            var point = transform.Value.Transform(new Point(0, 0));
                            
                            var targetOffset = point.Y - (scrollBounds.Height / 2) + (bounds.Height / 2);
                            
                            var extent = _scrollViewer.Extent.Height;
                            var viewport = _scrollViewer.Viewport.Height;
                            var maxOffset = extent - viewport;
                            
                            targetOffset = Math.Clamp(targetOffset, 0, Math.Max(0, maxOffset));
                            
                            _scrollViewer.Offset = new Vector(0, targetOffset);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Scroll error: {ex.Message}");
                }
            }, DispatcherPriority.Background);
        }

        public void ClearLyrics()
        {
            _lines.Clear();
            _currentLineIndex = -1;
            _lastScrolledIndex = -1;
            ShowNoLyricsMessage();
        }

        public bool HasLyrics => _lines.Count > 0;
    }
}