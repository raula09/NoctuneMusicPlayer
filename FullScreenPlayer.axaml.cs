using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using System;

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
    TextBlock? YearBlock;




    DispatcherTimer? bgTimer;
    Random rand = new Random();

    double blob1X = 0, blob1Y = 0, blob1TargetX = 0, blob1TargetY = 0;
    double blob1Rotation = 0, blob1TargetRotation = 0;

    double blob2X = 0, blob2Y = 0, blob2TargetX = 0, blob2TargetY = 0;
    double blob2Rotation = 0, blob2TargetRotation = 0;

    double blob3X = 0, blob3Y = 0, blob3TargetX = 0, blob3TargetY = 0;
    double blob3Rotation = 0, blob3TargetRotation = 0;

    bool _fsSeeking = false;
    double _fsSeekMax = 1;

    public FullscreenPlayer(Bitmap? art, string title, string artist, string album, string year)
    {
        InitializeComponent();

        AlbumArt = this.FindControl<Image>("FS_AlbumArt");
        AlbumNameBlock = this.FindControl<TextBlock>("FS_AlbumName");
        TitleBlock = this.FindControl<TextBlock>("FS_Title");
        ArtistBlock = this.FindControl<TextBlock>("FS_Artist");
        YearBlock = this.FindControl<TextBlock>("FS_Year");
        FS_Current = this.FindControl<TextBlock>("FS_Current");
        FS_Total = this.FindControl<TextBlock>("FS_Total");

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
        if (YearBlock != null) YearBlock.Text = year;

        InitBackgroundAnimation();
        UpdateBackgroundFromAlbum(art);

        KeyDown += FullscreenPlayer_KeyDown;
        Focusable = true;
        Focus();
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
    }

    public void UpdateTrack(Bitmap? art, string title, string artist, string album, string year)
    {
        if (AlbumArt != null) AlbumArt.Source = art;
        if (AlbumNameBlock != null) AlbumNameBlock.Text = album;
        if (TitleBlock != null) TitleBlock.Text = title;
        if (ArtistBlock != null) ArtistBlock.Text = artist;
        if (YearBlock != null) YearBlock.Text = year;

        UpdateBackgroundFromAlbum(art);
    }

    void UpdateBackgroundFromAlbum(Bitmap? art)
    {
        if (art == null) return;

        var c = AlbumColorExtractor.Extract(art);
        Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        var bg1 = this.FindControl<Ellipse>("BG1");
        var bg2 = this.FindControl<Ellipse>("BG2");
        var bg3 = this.FindControl<Ellipse>("BG3");

        if (bg1 != null) bg1.Fill = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        var (r2, g2, b2) = ShiftColor(c.R, c.G, c.B, 40);
        if (bg2 != null) bg2.Fill = new SolidColorBrush(Color.FromRgb(r2, g2, b2));

        var (r3, g3, b3) = ShiftColor(c.R, c.G, c.B, -40);
        if (bg3 != null) bg3.Fill = new SolidColorBrush(Color.FromRgb(r3, g3, b3));
    }

    (byte r, byte g, byte b) ShiftColor(byte r, byte g, byte b, int shift)
    {
        int nr = r + shift;
        int ng = g + shift / 2;
        int nb = b - shift / 2;

        nr = ((nr % 256) + 256) % 256;
        ng = ((ng % 256) + 256) % 256;
        nb = ((nb % 256) + 256) % 256;

        if (nr < 30) nr += 60;
        if (ng < 30) ng += 60;
        if (nb < 30) nb += 60;

        return ((byte)Math.Min(255, nr), (byte)Math.Min(255, ng), (byte)Math.Min(255, nb));
    }

    TranslateTransform? Tx(Ellipse? e)
        => e?.RenderTransform is TransformGroup tg ? tg.Children[0] as TranslateTransform : null;

    RotateTransform? Rx(Ellipse? e)
        => e?.RenderTransform is TransformGroup tg ? tg.Children[2] as RotateTransform : null;

    void InitBackgroundAnimation()
    {
        blob1X = blob1TargetX = rand.Next(-400, 400);
        blob1Y = blob1TargetY = rand.Next(-400, 400);
        blob1Rotation = blob1TargetRotation = rand.Next(0, 360);

        blob2X = blob2TargetX = rand.Next(-500, 500);
        blob2Y = blob2TargetY = rand.Next(-500, 500);
        blob2Rotation = blob2TargetRotation = rand.Next(0, 360);

        blob3X = blob3TargetX = rand.Next(-450, 450);
        blob3Y = blob3TargetY = rand.Next(-450, 450);
        blob3Rotation = blob3TargetRotation = rand.Next(0, 360);

        bgTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        bgTimer.Tick += (_, __) => AnimateBackground();
        bgTimer.Start();
    }

    void AnimateBackground()
    {
        double t = DateTime.Now.Ticks * 0.0000001;

        var bg1 = this.FindControl<Ellipse>("BG1");
        var bg2 = this.FindControl<Ellipse>("BG2");
        var bg3 = this.FindControl<Ellipse>("BG3");

        var tx1 = Tx(bg1);
        var rx1 = Rx(bg1);
        var tx2 = Tx(bg2);
        var rx2 = Rx(bg2);
        var tx3 = Tx(bg3);
        var rx3 = Rx(bg3);

        blob1X += (blob1TargetX - blob1X) * 0.015;
        blob1Y += (blob1TargetY - blob1Y) * 0.015;
        blob1Rotation += (blob1TargetRotation - blob1Rotation) * 0.01;

        blob2X += (blob2TargetX - blob2X) * 0.012;
        blob2Y += (blob2TargetY - blob2Y) * 0.012;
        blob2Rotation += (blob2TargetRotation - blob2Rotation) * 0.008;

        blob3X += (blob3TargetX - blob3X) * 0.018;
        blob3Y += (blob3TargetY - blob3Y) * 0.018;
        blob3Rotation += (blob3TargetRotation - blob3Rotation) * 0.012;

        double w1x = Math.Sin(t * 0.4) * 80;
        double w1y = Math.Cos(t * 0.3) * 80;
        double w2x = Math.Sin(t * 0.6) * 110;
        double w2y = Math.Cos(t * 0.4) * 110;
        double w3x = Math.Sin(t * 0.5) * 90;
        double w3y = Math.Cos(t * 0.5) * 90;

        if (tx1 != null) { tx1.X = blob1X + w1x; tx1.Y = blob1Y + w1y; }
        if (rx1 != null) rx1.Angle = blob1Rotation;

        if (tx2 != null) { tx2.X = blob2X + w2x; tx2.Y = blob2Y + w2y; }
        if (rx2 != null) rx2.Angle = blob2Rotation;

        if (tx3 != null) { tx3.X = blob3X + w3x; tx3.Y = blob3Y + w3y; }
        if (rx3 != null) rx3.Angle = blob3Rotation;
    }
}
