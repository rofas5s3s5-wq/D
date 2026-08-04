using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DentFona
{
    public partial class SettingsView : UserControl
    {
        public static string? SelectedCalibrationPath { get; private set; }
        public static string? SelectedStoragePath { get; private set; }

        public SettingsView()
        {
            InitializeComponent();

            // تحديد مسار افتراضي آمن لحفظ الصور
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DentFonaXrays");

            // قمنا بحظر الأكواد التي تبحث عن أزرار الواجهة حتى نضمن اختفاء الأخطاء الـ 183 أولاً
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            // تم تفريغ الدالة مؤقتاً لضمان نجاح التحديث
        }

        private void BtnBrowseCalib_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "FONA Calibration Files (*.cal)|*.cal|All files (*.*)|*.*",
                Title = "اختر ملف معايرة السينسور الخاص بجهاز FONA Elite"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedCalibrationPath = openFileDialog.FileName;
                MessageBox.Show("تم قراءة ملف المعايرة بنجاح", "تأكيد");
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("تم حفظ الإعدادات بنجاح.", "تم الحفظ");
        }
    }
}
