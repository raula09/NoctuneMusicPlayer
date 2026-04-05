using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;


namespace MusicPlayerApp.Helpers
{
    public static class AlbumColorExtractor
    {
        public static Color Extract(Bitmap bmp)
        {
            var size = new PixelSize(24, 24);
            var rtb = new RenderTargetBitmap(size);

            using (var dc = rtb.CreateDrawingContext())
                dc.DrawImage(bmp, new Rect(bmp.Size), new Rect(0, 0, size.Width, size.Height));

            int pixels = size.Width * size.Height;
            int stride = size.Width * 4;
            byte[] buf = new byte[pixels * 4];

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                rtb.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height),
                    handle.AddrOfPinnedObject(),
                    buf.Length,
                    stride);
            }
            finally
            {
                handle.Free();
            }

            long rs = 0, gs = 0, bs = 0;

            for (int i = 0; i < buf.Length; i += 4)
            {
                bs += buf[i + 0];
                gs += buf[i + 1];
                rs += buf[i + 2];
            }

            byte r = (byte)(rs / pixels);
            byte g = (byte)(gs / pixels);
            byte b = (byte)(bs / pixels);

            return Color.FromRgb(r, g, b);
        }
    }
}
