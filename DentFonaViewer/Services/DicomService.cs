using System;
using System.Drawing;
using System.IO;
using Dicom;
using Dicom.Imaging.Render.Serialization;

namespace DentFonaViewer.Services
{
    public class DicomService
    {
        public void SaveBitmapSourceAsDicom(System.Windows.Media.Imaging.BitmapSource bmpSource, string outFilePath, double pixelsPerMm)
        {
            // Convert BitmapSource to System.Drawing.Bitmap
            using var sysBmp = Helpers.ImageConversion.BitmapSourceToBitmap(bmpSource);

            // Create a DICOM secondary capture from bitmap
            var dicom = DicomImageConverter.ToDicom(sysBmp);

            // Add/Update PixelSpacing if calibration present
            if (pixelsPerMm > 0)
            {
                double mmPerPixel = 1.0 / pixelsPerMm;
                dicom.Dataset.AddOrUpdate(DicomTag.PixelSpacing, new[] { mmPerPixel.ToString(System.Globalization.CultureInfo.InvariantCulture), mmPerPixel.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }

            // Ensure some required tags exist (minimal)
            if (!dicom.Dataset.Contains(DicomTag.PatientName)) dicom.Dataset.AddOrUpdate(DicomTag.PatientName, "UNKNOWN");
            if (!dicom.Dataset.Contains(DicomTag.StudyInstanceUID)) dicom.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, DicomUID.Generate().UID);

            Directory.CreateDirectory(Path.GetDirectoryName(outFilePath) ?? ".");
            dicom.Save(outFilePath);
        }
    }
}
