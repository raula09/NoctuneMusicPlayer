using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using MusicPlayerApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using MusicPlayerApp.Models;


namespace MusicPlayerApp;

public partial class FullscreenPlayer : Window
{
    public event EventHandler? PrevRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler<long>? SeekRequested;
    public event EventHandler<int>? VolumeRequested;

    Image? AlbumArt;
    TextBlock? AlbumNameBlock;
    TextBlock? TitleBlock;
    TextBlock? ArtistBlock;

    private List<LyricsLine> _fsLyrics = new List<LyricsLine>();
    private int _fsCurrentLineIndex = -1;
    private int _fsDotAnimationFrame = 0;
    private DispatcherTimer? _fsDotTimer;

    DispatcherTimer? bgTimer;
    Random rand = new Random();
 private class BlobState
    {
        public double X, Y, TargetX, TargetY;
        public double Rotation, TargetRotation;
        public double Scale, TargetScale;
        public double ColorPhase;
        public double NoiseOffsetX, NoiseOffsetY;
        public double Speed;
        public double FrequencyMultiplier;
    }

    private List<BlobState> blobs = new List<BlobState>();
    private double globalTime = 0;
    private double[] permutation;
     
    private byte baseR, baseG, baseB;
    private double colorShiftPhase = 0;

    bool _fsSeeking = false;
    double _fsSeekMax = 1;

    public FullscreenPlayer(Bitmap? art, string title, string artist, string album, string year)
    {
        InitializeComponent();

        AlbumArt = this.FindControl<Image>("FS_AlbumArt");
        AlbumNameBlock = this.FindControl<TextBlock>("FS_AlbumName");
        TitleBlock = this.FindControl<TextBlock>("FS_Title");
        ArtistBlock = this.FindControl<TextBlock>("FS_Artist");
        FS_Current = this.FindControl<TextBlock>("FS_Current");
        FS_Total = this.FindControl<TextBlock>("FS_Total");
        FS_LyricsScrollViewer = this.FindControl<ScrollViewer>("FS_LyricsScrollViewer");
        FS_LyricsPanel = this.FindControl<StackPanel>("FS_LyricsPanel");
        FS_LyricsContainer = this.FindControl<Border>("FS_LyricsContainer");
        FS_MainContent = this.FindControl<Grid>("FS_MainContent");

        SeekBarContainer = this.FindControl<Border>("SeekBarContainer");
        SeekBarFill = this.FindControl<Border>("SeekBarFill");

        if (SeekBarContainer != null)
        {
            SeekBarContainer.PointerPressed += SeekBarPressed;
            SeekBarContainer.PointerMoved += SeekBarMoved;
            SeekBarContainer.PointerReleased += SeekBarReleased;
            SeekBarContainer.PointerCaptureLost += SeekBarCaptureLost;
        }

        if (AlbumArt != null) AlbumArt.Source = art;
        if (AlbumNameBlock != null) AlbumNameBlock.Text = album;
        if (TitleBlock != null) TitleBlock.Text = title;
        if (ArtistBlock != null) ArtistBlock.Text = artist;

        InitializeNoisePermutation();
        InitBackgroundAnimation();
        InitDotAnimation();
        UpdateBackgroundFromAlbum(art);

        KeyDown += FullscreenPlayer_KeyDown;
        Focusable = true;
        Focus();
    }

    
    private void InitializeNoisePermutation()
    { 
        permutation = new double[512];
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
         
        for (int i = 255; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            int temp = p[i];
            p[i] = p[j];
            p[j] = temp;
        }
        
        for (int i = 0; i < 512; i++)
            permutation[i] = p[i % 256];
    }

    private double Noise(double x, double y)
    { 
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);
        
        double u = Fade(xf);
        double v = Fade(yf);
        
        int aa = (int)permutation[(int)permutation[xi] + yi];
        int ab = (int)permutation[(int)permutation[xi] + yi + 1];
        int ba = (int)permutation[(int)permutation[xi + 1] + yi];
        int bb = (int)permutation[(int)permutation[xi + 1] + yi + 1];
        
        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        
        return Lerp(x1, x2, v);
    }

    private double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    
    private double Lerp(double a, double b, double t) => a + t * (b - a);
    
    private double Grad(int hash, double x, double y)
    {
        int h = hash & 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
 
    
    private double EaseOutElastic(double t)
    {
        const double c4 = (2 * Math.PI) / 3;
        return t == 0 ? 0 : t == 1 ? 1 : 
            Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
    }

    private double EaseInOutCubic(double t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }
 
    void InitBackgroundAnimation()
    { 
        for (int i = 0; i < 5; i++)
        {
            var blob = new BlobState
            {
                X = rand.Next(-400, 400),
                Y = rand.Next(-400, 400),
                TargetX = rand.Next(-400, 400),
                TargetY = rand.Next(-400, 400),
                Rotation = rand.Next(0, 360),
                TargetRotation = rand.Next(0, 360),
                Scale = 0.8 + rand.NextDouble() * 0.4,
                TargetScale = 0.8 + rand.NextDouble() * 0.4,
                ColorPhase = rand.NextDouble() * Math.PI * 2,
                NoiseOffsetX = rand.NextDouble() * 1000,
                NoiseOffsetY = rand.NextDouble() * 1000,
                Speed = 0.004 + rand.NextDouble() * 0.006,
                FrequencyMultiplier = 0.8 + rand.NextDouble() * 0.4
            };
            blob.TargetX = blob.X;
            blob.TargetY = blob.Y;
            blob.TargetRotation = blob.Rotation;
            blob.TargetScale = blob.Scale;
            blobs.Add(blob);
        }

        bgTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };  
        bgTimer.Tick += (_, __) => AnimateBackground();
        bgTimer.Start();
    }

    void AnimateBackground()
    {
        globalTime += 0.016;  
        colorShiftPhase += 0.001;

        var blobElements = new[]
        {
            this.FindControl<Ellipse>("BG1"),
            this.FindControl<Ellipse>("BG2"),
            this.FindControl<Ellipse>("BG3"),
            this.FindControl<Ellipse>("BG4"),
            this.FindControl<Ellipse>("BG5")
        };

        for (int i = 0; i < Math.Min(blobs.Count, blobElements.Length); i++)
        {
            var blob = blobs[i];
            var element = blobElements[i];
            if (element == null) continue;
 
            double noiseX = Noise(blob.NoiseOffsetX + globalTime * 0.3 * blob.FrequencyMultiplier, globalTime * 0.2);
            double noiseY = Noise(blob.NoiseOffsetY + globalTime * 0.3 * blob.FrequencyMultiplier, globalTime * 0.2);
            double noiseX2 = Noise(blob.NoiseOffsetX + globalTime * 0.15, globalTime * 0.1);
            double noiseY2 = Noise(blob.NoiseOffsetY + globalTime * 0.15, globalTime * 0.1);
 
            blob.X += (blob.TargetX - blob.X) * blob.Speed;
            blob.Y += (blob.TargetY - blob.Y) * blob.Speed;
            blob.Rotation += (blob.TargetRotation - blob.Rotation) * (blob.Speed * 0.7);
            blob.Scale += (blob.TargetScale - blob.Scale) * (blob.Speed * 1.2);
 
            if (rand.NextDouble() < 0.003 * (i + 1) * 0.5)
            {
                blob.TargetX = rand.Next(-450, 450);
                blob.TargetY = rand.Next(-450, 450);
                blob.TargetRotation = rand.Next(0, 360);
                blob.TargetScale = 0.7 + rand.NextDouble() * 0.6;
            }
  double wave1 = Math.Sin(globalTime * 0.5 * blob.FrequencyMultiplier) * 150;
            double wave2 = Math.Cos(globalTime * 0.3 * blob.FrequencyMultiplier) * 100;
            double wave3 = Math.Sin(globalTime * 0.8 * blob.FrequencyMultiplier) * 50;
            
            double finalX = blob.X + noiseX * 200 + noiseX2 * 80 + wave1 + wave3;
            double finalY = blob.Y + noiseY * 200 + noiseY2 * 80 + wave2 + wave3;

           
            var tx = GetTranslateTransform(element);
            var rx = GetRotateTransform(element);
            var sx = GetScaleTransform(element);

            if (tx != null)
            {
                tx.X = finalX;
                tx.Y = finalY;
            }

            if (rx != null)
            {
                double rotationNoise = Noise(globalTime * 0.2 + i, globalTime * 0.15) * 30;
                rx.Angle = blob.Rotation + rotationNoise;
            }

            if (sx != null)
            {
                
                double scalePulse = Math.Sin(globalTime * 1.5 + i) * 0.15;
                double finalScale = blob.Scale + scalePulse;
                sx.ScaleX = finalScale;
                sx.ScaleY = finalScale;
            }
 
            UpdateBlobColor(element, i, blob);
        }
 
        UpdateBackgroundColor();
    }

    private void UpdateBlobColor(Ellipse element, int index, BlobState blob)
    {
        if (element == null) return;
 
        double colorNoise = Noise(globalTime * 0.1 + index * 10, globalTime * 0.08);
        double hueShift = (Math.Sin(globalTime * 0.3 + blob.ColorPhase) * 40) + (colorNoise * 20);
        double satShift = 1.4 + Math.Sin(globalTime * 0.4 + index) * 0.3;
        
        var color = EnhanceColor(baseR, baseG, baseB, satShift, (int)hueShift);
 
        double gradientPhase = globalTime * 0.5 + index * 0.3;
        double originX = 0.5 + Math.Sin(gradientPhase) * 0.3;
        double originY = 0.5 + Math.Cos(gradientPhase * 0.8) * 0.3;
 
        byte alpha1 = (byte)(180 + Math.Sin(globalTime * 0.6 + index) * 60);
        byte alpha2 = (byte)(100 + Math.Sin(globalTime * 0.4 + index) * 50);

        var gradient = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(originX, originY, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Radius = 1.2 + Math.Sin(globalTime * 0.3 + index) * 0.15,
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(alpha1, color.r, color.g, color.b), 0.0),
                new GradientStop(Color.FromArgb(alpha2, color.r, color.g, color.b), 0.5),
                new GradientStop(Color.FromArgb(0, color.r, color.g, color.b), 1.0)
            }
        };
        element.Fill = gradient;
    }

    private void UpdateBackgroundColor()
    { 
        double pulse = Math.Sin(colorShiftPhase * 2) * 0.05 + 0.65;
        var baseDark = DarkenColor(baseR, baseG, baseB, pulse);
        Background = new SolidColorBrush(Color.FromRgb(baseDark.r, baseDark.g, baseDark.b));
    }
 
    private TranslateTransform? GetTranslateTransform(Ellipse? e)
        => e?.RenderTransform is TransformGroup tg && tg.Children.Count > 0 
            ? tg.Children[0] as TranslateTransform : null;

    private RotateTransform? GetRotateTransform(Ellipse? e)
        => e?.RenderTransform is TransformGroup tg && tg.Children.Count > 2 
            ? tg.Children[2] as RotateTransform : null;

    private ScaleTransform? GetScaleTransform(Ellipse? e)
        => e?.RenderTransform is TransformGroup tg && tg.Children.Count > 1 
            ? tg.Children[1] as ScaleTransform : null;
 
    private void UpdateBackgroundFromAlbum(Bitmap? art)
    {
        if (art == null) return;

        var c = AlbumColorExtractor.Extract(art);
        baseR = c.R;
        baseG = c.G;
        baseB = c.B;

        UpdateBackgroundColor();
 
        var blobElements = new[]
        {
            this.FindControl<Ellipse>("BG1"),
            this.FindControl<Ellipse>("BG2"),
            this.FindControl<Ellipse>("BG3"),
            this.FindControl<Ellipse>("BG4"),
            this.FindControl<Ellipse>("BG5")
        };

        for (int i = 0; i < Math.Min(blobs.Count, blobElements.Length); i++)
        {
            if (blobElements[i] != null)
            {
                UpdateBlobColor(blobElements[i], i, blobs[i]);
            }
        }
    }

    (byte r, byte g, byte b) DarkenColor(byte r, byte g, byte b, double factor)
    {
        return (
            (byte)Math.Min(255, r * factor),
            (byte)Math.Min(255, g * factor),
            (byte)Math.Min(255, b * factor)
        );
    }

    (byte r, byte g, byte b) EnhanceColor(byte r, byte g, byte b, double saturation, int hueShift)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double h = 0, s = 0, l = (max + min) / 2.0;

        if (max != min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == rd) h = (gd - bd) / d + (gd < bd ? 6 : 0);
            else if (max == gd) h = (bd - rd) / d + 2;
            else h = (rd - gd) / d + 4;
            h /= 6.0;
        }

        s = Math.Min(1.0, s * saturation);
        h = (h + hueShift / 360.0) % 1.0;
        if (h < 0) h += 1.0;
        
        l = Math.Min(0.85, l * 1.15);

        double r1, g1, b1;
        if (s == 0)
        {
            r1 = g1 = b1 = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r1 = HueToRgb(p, q, h + 1.0 / 3.0);
            g1 = HueToRgb(p, q, h);
            b1 = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return (
            (byte)Math.Min(255, Math.Max(0, r1 * 255)),
            (byte)Math.Min(255, Math.Max(0, g1 * 255)),
            (byte)Math.Min(255, Math.Max(0, b1 * 255))
        );
    }

    double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
 
    private void InitDotAnimation()
    {
        _fsDotTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(500) 
        };
        _fsDotTimer.Tick += (_, _) =>
        {
            _fsDotAnimationFrame = (_fsDotAnimationFrame + 1) % 4;
            UpdateFSLyricsDisplay();
        };
        _fsDotTimer.Start();
    }

    public void LoadLyrics(List<LyricsLine> lyrics)
    {
        _fsLyrics = lyrics;
        _fsCurrentLineIndex = -1;
    
        if (_fsLyrics.Count > 0)
        {
            if (FS_LyricsContainer != null)
            {
                FS_LyricsContainer.IsVisible = true;
            }
        
            BuildFSLyricsUI();
            CenterMainContent(false);
        }
        else
        {
            if (FS_LyricsContainer != null)
            {
                FS_LyricsContainer.IsVisible = false;
            }
            CenterMainContent(true);
        }
    }

    private void CenterMainContent(bool center)
    {
        if (FS_MainContent == null) return;

        if (center)
        {
            FS_MainContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            FS_MainContent.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }
        else
        {
            FS_MainContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            FS_MainContent.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }
    }

    private void BuildFSLyricsUI()
    {
        if (FS_LyricsPanel == null) return;

        FS_LyricsPanel.Children.Clear();

        if (_fsLyrics.Count == 0)
        {
            FS_LyricsPanel.Children.Add(new TextBlock
            {
                Text = "♪",
                FontSize = 40,
                Foreground = new SolidColorBrush(Color.Parse("#6A6A6A")),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 40)
            });
            return;
        }

        for (int i = 0; i < _fsLyrics.Count; i++)
        {
            var line = _fsLyrics[i];
            
            if (line.IsInstrumental)
            {
                var dotsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(16, 12),
                    Tag = i,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                for (int d = 0; d < 3; d++)
                {
                    var dot = new Border
                    {
                        Width = 10,
                        Height = 10,
                        CornerRadius = new Avalonia.CornerRadius(5),
                        Background = new SolidColorBrush(Color.Parse("#6A6A6A")),
                        Tag = $"dot_{d}"
                    };
                    dotsPanel.Children.Add(dot);
                }

                dotsPanel.PointerPressed += FSLyricsLine_Clicked;

                FS_LyricsPanel.Children.Add(dotsPanel);
            }
            else
            {
                var textBlock = new TextBlock
                {
                    Text = line.Text,
                    FontSize = 34,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(16, 12),
                    Tag = i,
                    Foreground = new SolidColorBrush(Color.Parse("#6A6A6A")),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                textBlock.PointerPressed += FSLyricsLine_Clicked;
                
                textBlock.PointerEntered += (s, e) =>
                {
                    if (s is TextBlock tb && tb.Tag is int idx && idx != _fsCurrentLineIndex)
                    {
                        tb.Opacity = 0.8;
                    }
                };
                
                textBlock.PointerExited += (s, e) =>
                {
                    if (s is TextBlock tb && tb.Tag is int idx && idx != _fsCurrentLineIndex)
                    {
                        if (idx < _fsCurrentLineIndex)
                            tb.Opacity = 0.6;
                        else
                            tb.Opacity = 0.5;
                    }
                };

                FS_LyricsPanel.Children.Add(textBlock);
            }
        }
    }

    private void FSLyricsLine_Clicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is int lineIndex)
        {
            if (lineIndex >= 0 && lineIndex < _fsLyrics.Count)
            {
                var seekTime = _fsLyrics[lineIndex].StartTime;
                long milliseconds = (long)seekTime.TotalMilliseconds;
                SeekRequested?.Invoke(this, milliseconds);
                e.Handled = true;
            }
        }
    }

    public void UpdateLyricsPosition(TimeSpan currentPosition)
    {
        if (_fsLyrics.Count == 0) return;

        int newLineIndex = -1;
        
        for (int i = 0; i < _fsLyrics.Count; i++)
        {
            var line = _fsLyrics[i];
            
            if (currentPosition >= line.StartTime && currentPosition < line.EndTime)
            {
                newLineIndex = i;
                break;
            }
        }

        if (newLineIndex != _fsCurrentLineIndex)
        {
            _fsCurrentLineIndex = newLineIndex;
            UpdateFSLyricsDisplay();
        }
    }

    private void UpdateFSLyricsDisplay()
    {
        if (FS_LyricsPanel == null || _fsLyrics.Count == 0) return;

        Control? activeControl = null;

        foreach (var child in FS_LyricsPanel.Children)
        {
            if (child is TextBlock textBlock && textBlock.Tag is int lineIndex)
            {
                if (lineIndex < 0 || lineIndex >= _fsLyrics.Count)
                    continue;

                bool isActive = (lineIndex == _fsCurrentLineIndex);

                if (isActive)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"));
                    textBlock.FontSize = 40;
                    textBlock.FontWeight = FontWeight.Bold;
                    textBlock.Opacity = 1.0;
                    activeControl = textBlock;
                }
                else if (lineIndex < _fsCurrentLineIndex)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.Parse("#4A4A4A"));
                    textBlock.FontSize = 34;
                    textBlock.FontWeight = FontWeight.SemiBold;
                    textBlock.Opacity = 0.6;
                }
                else
                {
                    textBlock.Foreground = new SolidColorBrush(Color.Parse("#6A6A6A"));
                    textBlock.FontSize = 34;
                    textBlock.FontWeight = FontWeight.SemiBold;
                    textBlock.Opacity = 0.5;
                }
            }
            else if (child is StackPanel dotsPanel && dotsPanel.Tag is int dotLineIndex)
            {
                if (dotLineIndex < 0 || dotLineIndex >= _fsLyrics.Count)
                    continue;

                bool isActive = (dotLineIndex == _fsCurrentLineIndex);

                if (isActive)
                {
                    activeControl = dotsPanel;
                    
                    var dots = dotsPanel.Children.OfType<Border>().ToList();
                    for (int d = 0; d < dots.Count; d++)
                    {
                        var fillProgress = (_fsDotAnimationFrame > d) ? 1.0 : 0.5;
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
            ScrollFSLyricsToControl(activeControl);
        }
    }

    private void ScrollFSLyricsToControl(Control control)
    {
        if (FS_LyricsScrollViewer == null || FS_LyricsPanel == null) return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Task.Delay(50);
            
                FS_LyricsScrollViewer.UpdateLayout();
                FS_LyricsPanel.UpdateLayout();
                control.UpdateLayout();

                var controlPosition = control.TranslatePoint(new Avalonia.Point(0, 0), FS_LyricsPanel);
                
                if (controlPosition != null)
                {
                    var scrollBounds = FS_LyricsScrollViewer.Bounds;
                    var controlBounds = control.Bounds;
                    
                    var targetOffset = controlPosition.Value.Y - (scrollBounds.Height / 2) + (controlBounds.Height / 2);
                    
                    var extent = FS_LyricsScrollViewer.Extent.Height;
                    var viewport = FS_LyricsScrollViewer.Viewport.Height;
                    var maxOffset = Math.Max(0, extent - viewport);
                    
                    targetOffset = Math.Clamp(targetOffset, 0, maxOffset);
                    
                    var currentOffset = FS_LyricsScrollViewer.Offset.Y;
                    var distance = Math.Abs(targetOffset - currentOffset);
                    
                    if (distance < 5)
                    {
                        FS_LyricsScrollViewer.Offset = new Avalonia.Vector(0, targetOffset);
                    }
                    else
                    {
                        var steps = Math.Min(20, (int)(distance / 10));
                        for (int i = 1; i <= steps; i++)
                        {
                            await Task.Delay(15);
                            var progress = (double)i / steps;
                            var easedProgress = 1 - Math.Pow(1 - progress, 3);
                            var newOffset = currentOffset + (targetOffset - currentOffset) * easedProgress;
                            FS_LyricsScrollViewer.Offset = new Avalonia.Vector(0, newOffset);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FS Scroll error: {ex.Message}");
            }
        });
    }
 
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Focus();
    }

    void FullscreenPlayer_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            PlayPauseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right)
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            PrevRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            VolumeRequested?.Invoke(this, +5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            VolumeRequested?.Invoke(this, -5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    void SeekBarPressed(object? sender, PointerPressedEventArgs e)
    {
        _fsSeeking = true;
        UpdateSeekbar(e);
    }

    void SeekBarMoved(object? sender, PointerEventArgs e)
    {
        if (_fsSeeking)
            UpdateSeekbar(e);
    }

    void SeekBarReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_fsSeeking && SeekBarContainer != null && SeekBarFill != null && _fsSeekMax > 0)
        {
            double pct = SeekBarFill.Width / SeekBarContainer.Bounds.Width;
            long ms = (long)(pct * _fsSeekMax);
            SeekRequested?.Invoke(this, ms);
        }

        _fsSeeking = false;
    }

    void SeekBarCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _fsSeeking = false;
    }

    void UpdateSeekbar(PointerEventArgs e)
    {
        if (SeekBarContainer == null || SeekBarFill == null || FS_Current == null)
            return;

        double pos = e.GetPosition(SeekBarContainer).X;
        pos = Math.Clamp(pos, 0, SeekBarContainer.Bounds.Width);

        SeekBarFill.Width = pos;

        if (_fsSeekMax > 0)
        {
            double pct = pos / SeekBarContainer.Bounds.Width;
            long ms = (long)(pct * _fsSeekMax);
            FS_Current.Text = TimeSpan.FromMilliseconds(ms).ToString("m\\:ss");
        }
    }

    public void UpdatePlayback(double pos, double max, string cur, string tot, bool playing)
    {
        _fsSeekMax = max;

        if (!_fsSeeking && SeekBarContainer != null && SeekBarFill != null)
        {
            if (max > 0)
            {
                double pct = pos / max;
                SeekBarFill.Width = pct * SeekBarContainer.Bounds.Width;
            }
        }

        if (!_fsSeeking && FS_Current != null) FS_Current.Text = cur;
        if (FS_Total != null) FS_Total.Text = tot;
        
        UpdateLyricsPosition(TimeSpan.FromMilliseconds(pos));
    }

    public void UpdateTrack(Bitmap? art, string title, string artist, string album, string year)
    {
        if (AlbumArt != null) AlbumArt.Source = art;
        if (AlbumNameBlock != null) AlbumNameBlock.Text = album;
        if (TitleBlock != null) TitleBlock.Text = title;
        if (ArtistBlock != null) ArtistBlock.Text = artist;

        UpdateBackgroundFromAlbum(art);
    }
}