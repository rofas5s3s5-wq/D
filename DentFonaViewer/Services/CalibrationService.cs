using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DentFonaViewer.Services
{
    public class CalibrationModel
    {
        public double PixelsPerMm { get; set; } = 3.0;
        public string SourceFile { get; set; } = string.Empty;
    }

    public class CalibrationService
    {
        private static readonly string DefaultJson = "DentFonaViewer/Calibration/calibration.json";
        private static readonly string CorPath = "DentFonaViewer/Calibration/STSYN111015225.cor";

        public CalibrationModel Model { get; private set; }

        public CalibrationService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultJson) ?? ".");

            if (File.Exists(DefaultJson))
            {
                if (TryLoadJson(DefaultJson)) return;
            }

            // If not, try to detect the .cor file in the expected location
            if (File.Exists(CorPath))
            {
                if (TryLoadCorSmart(CorPath))
                {
                    // Save a JSON copy for quick subsequent loads
                    Save(DefaultJson);
                    return;
                }
            }

            // Fallback: create default model and save
            Model = new CalibrationModel();
            Save(DefaultJson);
        }

        public double PixelsPerMm
        {
            get => Model.PixelsPerMm;
            set { Model.PixelsPerMm = value; Save(DefaultJson); }
        }

        public void Save(string path)
        {
            Model.SourceFile = path;
            File.WriteAllText(path, JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true }));
        }

        private bool TryLoadJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                Model = JsonSerializer.Deserialize<CalibrationModel>(json) ?? new CalibrationModel();
                Model.SourceFile = path;
                return true;
            }
            catch
            {
                Model = new CalibrationModel();
                return false;
            }
        }

        private bool TryLoadCorSmart(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);

                // Try UTF8 text heuristics
                string text = string.Empty;
                try { text = Encoding.UTF8.GetString(bytes); }
                catch { text = string.Empty; }

                // 1) JSON inside
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("pixelsPerMm", out var prop))
                        {
                            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var v))
                            {
                                Model = new CalibrationModel { PixelsPerMm = v, SourceFile = path };
                                return true;
                            }
                        }
                    }
                    catch { }
                }

                // 2) XML-like text
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("<"))
                {
                    var m = Regex.Match(text, @"(?i)(pixelsPerMm|pixelspacing|pixel_size|pixelpitch)[^>]*>([0-9.,]+)<");
                    if (m.Success && double.TryParse(m.Groups[2].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v2))
                    {
                        Model = new CalibrationModel { PixelsPerMm = v2, SourceFile = path };
                        return true;
                    }
                }

                // 3) key=value or similar inside ASCII
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var regexKey = new Regex(@"(?i)(pixelspermm|pixelspacing|pixels_per_mm|pixel_size|pixelpitch|px_per_mm|mm_per_pixel)[\s:=]*([0-9]+(?:[.,][0-9]+)?)");
                    var m = regexKey.Match(text);
                    if (m.Success && double.TryParse(m.Groups[2].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var found))
                    {
                        // If key says mm_per_pixel, convert
                        var key = m.Groups[1].Value.ToLowerInvariant();
                        if (key.Contains("mm") && !key.Contains("px") && found > 0)
                        {
                            // value is mm per pixel -> pixelsPerMm = 1/val
                            Model = new CalibrationModel { PixelsPerMm = 1.0 / found, SourceFile = path };
                        }
                        else
                        {
                            Model = new CalibrationModel { PixelsPerMm = found, SourceFile = path };
                        }
                        return true;
                    }
                }

                // 4) Binary scan for float/double candidates near printable ASCII labels
                var floatCandidates = FindFloatCandidates(bytes).ToList();
                if (floatCandidates.Count > 0)
                {
                    // choose the most reasonable candidate (closest to typical pixelsPerMm range)
                    // typical pixelsPerMm maybe between 0.5 and 50
                    var chosen = floatCandidates.OrderBy(c => Math.Abs(c - 10.0)).First();
                    Model = new CalibrationModel { PixelsPerMm = chosen, SourceFile = path };
                    return true;
                }

                var doubleCandidates = FindDoubleCandidates(bytes).ToList();
                if (doubleCandidates.Count > 0)
                {
                    var chosen = doubleCandidates.OrderBy(c => Math.Abs(c - 10.0)).First();
                    Model = new CalibrationModel { PixelsPerMm = chosen, SourceFile = path };
                    return true;
                }

                // 5) As last resort try to extract any decimal and interpret heuristically
                var regexNum = new Regex(@"([0-9]+(?:[.,][0-9]+))");
                var m2 = regexNum.Match(text + " " + AsciiFromBytes(bytes, 1024));
                if (m2.Success && double.TryParse(m2.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    // heuristics: if num < 1 -> maybe mm/pixel, else maybe pixelsPerMm
                    if (num > 0 && num < 1)
                    {
                        Model = new CalibrationModel { PixelsPerMm = 1.0 / num, SourceFile = path };
                    }
                    else
                    {
                        Model = new CalibrationModel { PixelsPerMm = num, SourceFile = path };
                    }
                    return true;
                }

            }
            catch
            {
                // ignore
            }

            // failed to parse
            Model = new CalibrationModel();
            return false;
        }

        private static string AsciiFromBytes(byte[] bytes, int maxLen = 2048)
        {
            int len = Math.Min(bytes.Length, maxLen);
            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                var b = bytes[i];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }
            return sb.ToString();
        }

        private static IEnumerable<double> FindFloatCandidates(byte[] data)
        {
            var list = new List<double>();
            for (int i = 0; i + 4 <= data.Length; i++)
            {
                float val = BitConverter.ToSingle(data, i);
                if (float.IsNaN(val) || float.IsInfinity(val)) continue;
                if (val > 0.1f && val < 200.0f)
                {
                    // interpret both as px/mm or mm/pixel
                    if (val >= 0.5f && val <= 200.0f)
                    {
                        // likely pixels per mm
                        list.Add(val);
                    }
                    else if (val > 0.01f && val < 1.0f)
                    {
                        // mm per pixel -> convert
                        list.Add(1.0 / val);
                    }
                }
            }
            return list.Distinct().ToList();
        }

        private static IEnumerable<double> FindDoubleCandidates(byte[] data)
        {
            var list = new List<double>();
            for (int i = 0; i + 8 <= data.Length; i++)
            {
                double val = BitConverter.ToDouble(data, i);
                if (double.IsNaN(val) || double.IsInfinity(val)) continue;
                if (val > 0.1 && val < 200.0)
                {
                    if (val >= 0.5 && val <= 200.0) list.Add(val);
                    else if (val > 0.01 && val < 1.0) list.Add(1.0 / val);
                }
            }
            return list.Distinct().ToList();
        }
    }
}
