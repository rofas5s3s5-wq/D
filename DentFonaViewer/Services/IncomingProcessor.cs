using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Dicom;
using DentFonaViewer.Helpers;

namespace DentFonaViewer.Services
{
    public class IncomingProcessor
    {
        private readonly DicomService _dicomService;
        private readonly CalibrationService _calib;

        public event Action<BitmapSource?, string?>? OnImageReady;

        public IncomingProcessor(CalibrationService calib, DicomService dicomService)
        {
            _calib = calib;
            _dicomService = dicomService;
        }

        public async Task ProcessIncomingFileAsync(string path)
        {
            if (!File.Exists(path)) return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            BitmapSource? bmp = null;

            try
            {
                if (ext == ".dcm")
                {
                    // Use fo-dicom to open and render
                    var dcm = await Task.Run(() => DicomFile.Open(path));
                    var dicomImage = new Dicom.Imaging.DicomImage(dcm.Dataset);
                    using var sysBmp = dicomImage.RenderImage().As<System.Drawing.Bitmap>();
                    bmp = ImageConversion.BitmapToBitmapSource(sysBmp);
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".tif" || ext == ".tiff")
                {
                    bmp = await LoadBitmapSourceAsync(path);
                }
                else
                {
                    // unsupported file type
                    return;
                }

                // Raise event to UI for approval/display
                OnImageReady?.Invoke(bmp, path);

                // Note: actual saving should be triggered by UI (RequireApproval) or auto-save flow
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IncomingProcessor failed to process {path}: {ex}");
            }
        }

        private static Task<BitmapSource> LoadBitmapSourceAsync(string path)
        {
            return Task.Run(() =>
            {
                var img = new BitmapImage();
                using var fs = File.OpenRead(path);
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = fs;
                img.EndInit();
                img.Freeze();
                return (BitmapSource)img;
            });
        }

        public void SaveAsDicom(BitmapSource bmp, string patientId = "UNKNOWN")
        {
            // output folder
            var outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DentFona\SavedDICOM");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, $"SC_{DateTime.Now:yyyyMMdd_HHmmss}.dcm");
            _dicomService.SaveBitmapSourceAsDicom(bmp, outPath, _calib.PixelsPerMm);
        }
    }
}
