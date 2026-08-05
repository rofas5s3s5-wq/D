using System;
using System.Windows;
using System.Windows.Media.Imaging;
using DentFonaViewer.Services;
using DentFonaViewer.Helpers;

namespace DentFonaViewer.Views
{
    public partial class MainWindow : Window
    {
        private readonly CalibrationService _calib;
        private readonly DicomService _dicomService;
        private IncomingProcessor _incomingProcessor;
        private ExternalCaptureWatcher? _watcher;
        private readonly TwainService _twain;
        private readonly ImageProcessingService _imgProc;
        private BitmapSource? _current;

        public MainWindow()
        {
            InitializeComponent();

            _calib = new CalibrationService();
            _dicomService = new DicomService();
            _incomingProcessor = new IncomingProcessor(_calib, _dicomService);
            _incomingProcessor.OnImageReady += IncomingProcessor_OnImageReady;

            _twain = new TwainService(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            _imgProc = new ImageProcessingService();

            UpdateCalibrationText();
        }

        private void IncomingProcessor_OnImageReady(System.Windows.Media.Imaging.BitmapSource? bmp, string? path)
        {
            Dispatcher.Invoke(() =>
            {
                if (bmp != null)
                {
                    _current = bmp;
                    MainImage.Source = bmp;
                    LstRecent.Items.Insert(0, path ?? "(incoming)");
                    TxtStatus.Text = "وصَل ملف: " + (path ?? "(غير معروف)");
                }
                else
                {
                    TxtStatus.Text = "فشل في قراءة الصورة الواردة.";
                }
            });
        }

        private void UpdateCalibrationText()
        {
            TxtCalibration.Text = $"المعايرة: {Math.Round(_calib.PixelsPerMm, 4)} بكسل/مم (مصدر: {_calib.Model.SourceFile})";
        }

        private void BtnStartWatcher_Click(object sender, RoutedEventArgs e)
        {
            if (_watcher != null)
            {
                TxtStatus.Text = "المراقبة تعمل بالفعل.";
                return;
            }

            try
            {
                _watcher = new ExternalCaptureWatcher(@"C:\DentFona\Incoming", _incomingProcessor);
                TxtStatus.Text = "تم بدء المراقبة على C:\\DentFona\\Incoming";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "خطأ عند بدء المراقبة: " + ex.Message;
            }
        }

        private void BtnStopWatcher_Click(object sender, RoutedEventArgs e)
        {
            if (_watcher == null)
            {
                TxtStatus.Text = "لا توجد مراقبة لتوقيفها.";
                return;
            }
            _watcher.Dispose();
            _watcher = null;
            TxtStatus.Text = "تم إيقاف المراقبة.";
        }

        private async void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "جاري التقاط صورة عبر TWAIN...";
            try
            {
                var bytes = await _twain.CaptureAsync();
                if (bytes != null && bytes.Length > 0)
                {
                    var bmp = ImageConversion.BytesBmpToBitmapSource(bytes);
                    _current = bmp;
                    MainImage.Source = bmp;
                    TxtStatus.Text = "تم الالتقاط بنجاح.";
                }
                else
                {
                    TxtStatus.Text = "لم يتم التقاط صورة.";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "خطأ أثناء التقاط الصورة: " + ex.Message;
            }
        }

        private void BtnSaveDicom_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                TxtStatus.Text = "لا توجد صورة لحفظها.";
                return;
            }

            try
            {
                _incomingProcessor.SaveAsDicom(_current);
                TxtStatus.Text = "تم حفظ DICOM في المستندات (DentFona\\SavedDICOM).";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "خطأ عند حفظ DICOM: " + ex.Message;
            }
        }

        private void BtnNegative_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                TxtStatus.Text = "لا توجد صورة لتطبيق الفلتر.";
                return;
            }
            try
            {
                var res = _imgProc.ApplyNegative(_current);
                _current = res;
                MainImage.Source = res;
                TxtStatus.Text = "تم تطبيق السالب.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "خطأ عند تطبيق السالب: " + ex.Message;
            }
        }

        private void BtnCLAHE_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                TxtStatus.Text = "لا توجد صورة لتطبيق الفلتر.";
                return;
            }
            try
            {
                var res = _imgProc.ApplyCLAHE(_current);
                _current = res;
                MainImage.Source = res;
                TxtStatus.Text = "تم تطبيق CLAHE.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "خطأ عند تطبيق CLAHE: " + ex.Message;
            }
        }
    }
}
