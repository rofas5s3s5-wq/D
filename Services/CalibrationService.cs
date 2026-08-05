using System.IO;
using System.Text.Json;

namespace DentFona.Services
{
    public class CalibrationModel
    {
        public double PixelsPerMm { get; set; } = 3.0;
    }

    public class CalibrationService
    {
        private const string FileName = "calibration.json";
        public CalibrationModel Model { get; private set; }

        public CalibrationService()
        {
            if (File.Exists(FileName))
            {
                var json = File.ReadAllText(FileName);
                Model = JsonSerializer.Deserialize<CalibrationModel>(json) ?? new CalibrationModel();
            }
            else
            {
                Model = new CalibrationModel();
                Save();
            }
        }

        public double PixelsPerMm
        {
            get => Model.PixelsPerMm;
            set { Model.PixelsPerMm = value; Save(); }
        }

        public void Save()
        {
            File.WriteAllText(FileName, JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
