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
    TextBlock? CurrentText;
    TextBlock? TotalText;
    Slider? PositionSlider;

    DispatcherTimer? bgTimer;
    Random rand = new Random();
    
    double blob1X = 0, blob1Y = 0, blob1TargetX = 0, blob1TargetY = 0;
    double blob1Rotation = 0, blob1TargetRotation = 0;
    
    double blob2X = 0, blob2Y = 0, blob2TargetX = 0, blob2TargetY = 0;
    double blob2Rotation = 0, blob2TargetRotation = 0;
    
    double blob3X = 0, blob3Y = 0, blob3TargetX = 0, blob3TargetY = 0;
    double blob3Rotation = 0, blob3TargetRotation = 0;
    
    int moveCounter = 0;
    bool _userIsSeeking = false;

public FullscreenPlayer(Bitmap? art, string title, string artist, string album, string year)
{
    InitializeComponent();

    AlbumArt = this.FindControl<Image>("FS_AlbumArt");
    AlbumNameBlock = this.FindControl<TextBlock>("FS_AlbumName");
    TitleBlock = this.FindControl<TextBlock>("FS_Title");
    ArtistBlock = this.FindControl<TextBlock>("FS_Artist");
    YearBlock = this.FindControl<TextBlock>("FS_Year");
    CurrentText = this.FindControl<TextBlock>("FS_Current");
    TotalText = this.FindControl<TextBlock>("FS_Total");
    PositionSlider = this.FindControl<Slider>("FS_Slider");

    if (AlbumArt != null) AlbumArt.Source = art;
    if (AlbumNameBlock != null) AlbumNameBlock.Text = album;  // ← Changed from 'artist' to 'album'
    if (TitleBlock != null) TitleBlock.Text = title;
    if (ArtistBlock != null) ArtistBlock.Text = artist;
    if (YearBlock != null) YearBlock.Text = year;

    InitBackgroundAnimation();
    UpdateBackgroundFromAlbum(art);

        if (PositionSlider != null)
        {
            PositionSlider.PointerPressed += FS_Slider_PointerPressed;
            PositionSlider.PointerReleased += FS_Slider_PointerReleased;
            PositionSlider.PointerMoved += FS_Slider_PointerMoved;
            PositionSlider.PointerCaptureLost += FS_Slider_PointerCaptureLost;
        }

        this.KeyDown += FullscreenPlayer_KeyDown;
        this.Focusable = true;
        this.Focus();
        
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.Focus();
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

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    void FS_Slider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (PositionSlider == null) return;
        _userIsSeeking = true;

        var p = e.GetPosition(PositionSlider);
        var pct = Math.Clamp(p.X / PositionSlider.Bounds.Width, 0, 1);
        var newPos = (long)(pct * PositionSlider.Maximum);

        PositionSlider.Value = newPos;
        if (CurrentText != null)
            CurrentText.Text = TimeSpan.FromMilliseconds(newPos).ToString(@"m\:ss");
    }

    void FS_Slider_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_userIsSeeking || PositionSlider == null) return;
        if (!e.GetCurrentPoint(PositionSlider).Properties.IsLeftButtonPressed) return;

        var p = e.GetPosition(PositionSlider);
        var pct = Math.Clamp(p.X / PositionSlider.Bounds.Width, 0, 1);
        var newPos = (long)(pct * PositionSlider.Maximum);

        PositionSlider.Value = newPos;
        if (CurrentText != null)
            CurrentText.Text = TimeSpan.FromMilliseconds(newPos).ToString(@"m\:ss");
    }

    void FS_Slider_PointerCaptureLost(object? s, PointerCaptureLostEventArgs e) => HandleSeek();
    void FS_Slider_PointerReleased(object? s, PointerReleasedEventArgs e) => HandleSeek();

    void HandleSeek()
    {
        if (_userIsSeeking && PositionSlider != null)
            SeekRequested?.Invoke(this, (long)PositionSlider.Value);

        _userIsSeeking = false;
    }

    public void UpdatePlayback(double pos, double max, string cur, string tot, bool playing)
    {
        if (!_userIsSeeking && PositionSlider != null)
        {
            PositionSlider.Maximum = max;
            PositionSlider.Value = pos;
        }

        if (!_userIsSeeking && CurrentText != null) CurrentText.Text = cur;
        if (TotalText != null) TotalText.Text = tot;
    }

    public void UpdateTrack(Bitmap? art, string title, string artist, string album, string year)
{
    if (AlbumArt != null) AlbumArt.Source = art;
    if (AlbumNameBlock != null) AlbumNameBlock.Text = album;  // ← Changed from 'year' to 'album'
    if (TitleBlock != null) TitleBlock.Text = title;
    if (ArtistBlock != null) ArtistBlock.Text = artist;
    if (YearBlock != null) YearBlock.Text = year;
    UpdateBackgroundFromAlbum(art);
}
    void UpdateBackgroundFromAlbum(Bitmap? art)
    {
        if (art == null) return;
        var c = AlbumColorExtractor.Extract(art);

        this.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

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
        int nr = r + shift, ng = g + shift / 2, nb = b - shift / 2;
        nr = ((nr % 256) + 256) % 256;
        ng = ((ng % 256) + 256) % 256;
        nb = ((nb % 256) + 256) % 256;

        if (nr < 30) nr += 60;
        if (ng < 30) ng += 60;
        if (nb < 30) nb += 60;

        return ((byte)Math.Min(255, nr), (byte)Math.Min(255, ng), (byte)Math.Min(255, nb));
    }

    TranslateTransform? Tx(Ellipse? e) => e?.RenderTransform is TransformGroup tg ? tg.Children[0] as TranslateTransform : null;
    RotateTransform? Rx(Ellipse? e) => e?.RenderTransform is TransformGroup tg ? tg.Children[2] as RotateTransform : null;

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
        moveCounter++;
        if (moveCounter >= 180)
        {
            blob1TargetX = rand.Next(-500, 500);
            blob1TargetY = rand.Next(-500, 500);
            blob1TargetRotation = rand.Next(0, 360);

            blob2TargetX = rand.Next(-600, 600);
            blob2TargetY = rand.Next(-600, 600);
            blob2TargetRotation = rand.Next(0, 360);

            blob3TargetX = rand.Next(-550, 550);
            blob3TargetY = rand.Next(-550, 550);
            blob3TargetRotation = rand.Next(0, 360);

            moveCounter = 0;
        }

        blob1X += (blob1TargetX - blob1X) * 0.015;
        blob1Y += (blob1TargetY - blob1Y) * 0.015;
        blob1Rotation += (blob1TargetRotation - blob1Rotation) * 0.01;

        blob2X += (blob2TargetX - blob2X) * 0.012;
        blob2Y += (blob2TargetY - blob2Y) * 0.012;
        blob2Rotation += (blob2TargetRotation - blob2Rotation) * 0.008;

        blob3X += (blob3TargetX - blob3X) * 0.018;
        blob3Y += (blob3TargetY - blob3Y) * 0.018;
        blob3Rotation += (blob3TargetRotation - blob3Rotation) * 0.012;

        double t = DateTime.Now.Ticks * 0.0000001;
        double w1x = Math.Sin(t * 0.4) * 80, w1y = Math.Cos(t * 0.3) * 80;
        double w2x = Math.Sin(t * 0.6) * 110, w2y = Math.Cos(t * 0.4) * 110;
        double w3x = Math.Sin(t * 0.5) * 90, w3y = Math.Cos(t * 0.5) * 90;

        var bg1 = this.FindControl<Ellipse>("BG1");
        var tx1 = Tx(bg1); var rx1 = Rx(bg1);
        if (tx1 != null) { tx1.X = blob1X + w1x; tx1.Y = blob1Y + w1y; }
        if (rx1 != null) rx1.Angle = blob1Rotation;

        var bg2 = this.FindControl<Ellipse>("BG2");
        var tx2 = Tx(bg2); var rx2 = Rx(bg2);
        if (tx2 != null) { tx2.X = blob2X + w2x; tx2.Y = blob2Y + w2y; }
        if (rx2 != null) rx2.Angle = blob2Rotation;

        var bg3 = this.FindControl<Ellipse>("BG3");
        var tx3 = Tx(bg3); var rx3 = Rx(bg3);
        if (tx3 != null) { tx3.X = blob3X + w3x; tx3.Y = blob3Y + w3y; }
        if (rx3 != null) rx3.Angle = blob3Rotation;
    }
}
