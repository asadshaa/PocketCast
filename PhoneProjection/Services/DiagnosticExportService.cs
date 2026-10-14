using System;
using System.IO;
using System.Text;
using PhoneProjection.Models;

namespace PhoneProjection.Services
{
    public class DiagnosticExportService
    {
        public static string GenerateReport(
            DeviceInfo? selectedDevice,
            QualityPreset selectedPreset,
            int fpsCap,
            double currentFps,
            bool isMirroring,
            string scrcpyPath,
            string adbPath,
            string rawLogs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================================");
            sb.AppendLine("            PHONEPROJECTION DIAGNOSTIC REPORT           ");
            sb.AppendLine("========================================================");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Host OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($".NET Runtime: {Environment.Version}");
            sb.AppendLine();

            sb.AppendLine("--- EXECUTABLES & ENVIRONMENT ---");
            sb.AppendLine($"scrcpy Path: {(string.IsNullOrEmpty(scrcpyPath) ? "NOT FOUND" : scrcpyPath)}");
            sb.AppendLine($"adb Path:    {(string.IsNullOrEmpty(adbPath) ? "NOT FOUND" : adbPath)}");
            sb.AppendLine();

            sb.AppendLine("--- TARGET DEVICE ---");
            if (selectedDevice != null)
            {
                sb.AppendLine($"Model:        {selectedDevice.Model}");
                sb.AppendLine($"Serial/ID:    {selectedDevice.Serial}");
                sb.AppendLine($"Transport:    {selectedDevice.Transport}");
                sb.AppendLine($"State:        {selectedDevice.State}");
                sb.AppendLine($"Authorized:   {selectedDevice.IsAuthorized}");
            }
            else
            {
                sb.AppendLine("No device currently selected.");
            }
            sb.AppendLine();

            sb.AppendLine("--- ACTIVE STREAMING CONFIGURATION ---");
            sb.AppendLine($"Session Active:    {isMirroring}");
            sb.AppendLine($"Selected Preset:   {selectedPreset.Name} ({selectedPreset.UseCaseCategory})");
            sb.AppendLine($"Target Max Size:   {(selectedPreset.IsCustom ? "Custom" : selectedPreset.MaxSize.ToString())} px");
            sb.AppendLine($"Bitrate:           {selectedPreset.BitrateMbps} Mbps");
            sb.AppendLine($"FPS Cap:           {fpsCap} FPS");
            sb.AppendLine($"Reported FPS:      {(currentFps > 0 ? $"{currentFps:F1} FPS" : "No active stream samples")}");
            sb.AppendLine();

            sb.AppendLine("--- RECENT SESSION LOGS ---");
            if (string.IsNullOrWhiteSpace(rawLogs))
            {
                sb.AppendLine("No recent session logs recorded.");
            }
            else
            {
                // Sanitize any sensitive tokens/credentials just in case
                sb.AppendLine(rawLogs.Trim());
            }
            sb.AppendLine();
            sb.AppendLine("======================= END REPORT =====================");

            return sb.ToString();
        }

        public static (bool Success, string FilePath, string Message) SaveReportToFile(string reportContent)
        {
            try
            {
                string exportDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PhoneProjection"
                );

                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                string fileName = $"Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string fullPath = Path.Combine(exportDir, fileName);

                File.WriteAllText(fullPath, reportContent);
                return (true, fullPath, $"Diagnostic report saved to:\n{fullPath}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Failed to export diagnostic report: {ex.Message}");
            }
        }
    }
}
