using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using DentFonaViewer.Helpers;

namespace DentFonaViewer.Services
{
    // خدمات معالجة الصورة البسيطة: سالب (Negative) و CLAHE تقريبي (باستخدام معادلة تباين/معادلة التوزيع)
    public class ImageProcessingService
    {
        public BitmapSource ApplyNegative(BitmapSource source)
        {
            using var bmp = ImageConversion.BitmapSourceToBitmap(source);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);
            try
            {
                int bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                int byteCount = data.Stride * bmp.Height;
                byte[] pixels = new byte[byteCount];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, byteCount);

                for (int i = 0; i < byteCount; i += bytesPerPixel)
                {
                    // For common formats (24bpp/32bpp) invert RGB channels
                    pixels[i + 0] = (byte)(255 - pixels[i + 0]); // B
                    pixels[i + 1] = (byte)(255 - pixels[i + 1]); // G
                    pixels[i + 2] = (byte)(255 - pixels[i + 2]); // R
                    // alpha left as-is if present
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, byteCount);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return ImageConversion.BitmapToBitmapSource(bmp);
        }

        // Simple histogram equalization on the luminance channel as an approximation of CLAHE
        public BitmapSource ApplyCLAHE(BitmapSource source)
        {
            using var bmp = ImageConversion.BitmapSourceToBitmap(source);

            // Convert to 24bpp if necessary
            using var dst = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(dst))
                g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);

            var rect = new Rectangle(0, 0, dst.Width, dst.Height);
            var data = dst.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, dst.PixelFormat);
            try
            {
                int bytesPerPixel = Image.GetPixelFormatSize(dst.PixelFormat) / 8;
                int byteCount = data.Stride * dst.Height;
                byte[] pixels = new byte[byteCount];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, byteCount);

                // Compute luminance histogram
                int[] hist = new int[256];
                for (int i = 0; i < byteCount; i += bytesPerPixel)
                {
                    byte b = pixels[i + 0];
                    byte g2 = pixels[i + 1];
                    byte r = pixels[i + 2];
                    int lum = (int)(0.2126 * r + 0.7152 * g2 + 0.0722 * b);
                    hist[lum]++;
                }

                // Compute CDF
                int total = dst.Width * dst.Height;
                int[] cdf = new int[256];
                int cum = 0;
                for (int i = 0; i < 256; i++) { cum += hist[i]; cdf[i] = cum; }

                // Build LUT
                byte[] lut = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    lut[i] = (byte)Math.Round((double)(cdf[i] - cdf[0]) / (total - cdf[0]) * 255.0);
                }

                // Apply equalization to each pixel by mapping luminance preserving color ratios
                for (int i = 0; i < byteCount; i += bytesPerPixel)
                {
                    byte b = pixels[i + 0];
                    byte g2 = pixels[i + 1];
                    byte r = pixels[i + 2];

                    int lum = (int)(0.2126 * r + 0.7152 * g2 + 0.0722 * b);
                    byte newLum = lut[lum];

                    if (lum == 0)
                    {
                        pixels[i + 0] = pixels[i + 1] = pixels[i + 2] = newLum;
                    }
                    else
                    {
                        double scale = (double)newLum / lum;
                        int nr = Math.Min(255, (int)(r * scale));
                        int ng = Math.Min(255, (int)(g2 * scale));
                        int nb = Math.Min(255, (int)(b * scale));
                        pixels[i + 0] = (byte)nb;
                        pixels[i + 1] = (byte)ng;
                        pixels[i + 2] = (byte)nr;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, byteCount);
            }
            finally
            {
                dst.UnlockBits(data);
            }

            return ImageConversion.BitmapToBitmapSource(dst);
        }
    }
}
