using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DentFonaViewer.Helpers
{
    internal static class ImageConversion
n    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource BitmapToBitmapSource(Bitmap bmp)
        {
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        public static BitmapSource BytesBmpToBitmapSource(byte[] bmpBytes)
        {
            using var ms = new MemoryStream(bmpBytes);
            using var bmp = new Bitmap(ms);
            return BitmapToBitmapSource(bmp);
        }

        public static Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            if (source == null) return null!;
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return new Bitmap(ms);
        }
    }
}
