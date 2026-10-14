using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PhoneProjection.Models;

namespace PhoneProjection.Services
{
    public class AdbDeviceService
    {
        private readonly ExecutableDiscoveryService _discovery;

        public AdbDeviceService(ExecutableDiscoveryService discovery)
        {
            _discovery = discovery;
        }

        public async Task<(bool Success, List<DeviceInfo> Devices, string RawOutput, string ErrorMessage)> GetConnectedDevicesAsync(string? customAdbPath = null)
        {
            string? adbPath = _discovery.FindAdbPath(customAdbPath);
            if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
            {
                return (false, new List<DeviceInfo>(), string.Empty, "ADB executable not found. Please verify Android SDK platform-tools or configure the custom ADB path.");
            }

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "devices -l",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string output = await outputTask;
                string error = await errorTask;

                var devices = new List<DeviceInfo>();
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var dev = DeviceInfo.Parse(line);
                    if (dev != null)
                    {
                        devices.Add(dev);
                    }
                }

                return (true, devices, output, error);
            }
            catch (Exception ex)
            {
                return (false, new List<DeviceInfo>(), string.Empty, $"Failed to query ADB devices: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ConnectTcpIpAsync(string ip, int port, string? customAdbPath = null)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return (false, "IP address cannot be empty.");

            if (port <= 0 || port > 65535)
                return (false, "Port must be between 1 and 65535.");

            string? adbPath = _discovery.FindAdbPath(customAdbPath);
            if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
                return (false, "ADB executable not found.");

            string endpoint = $"{ip.Trim()}:{port}";

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"connect {endpoint}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                string combined = $"{output} {error}".Trim();
                if (combined.Contains("connected to", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, $"Successfully connected to {endpoint}");
                }
                else
                {
                    return (false, $"Connection attempt to {endpoint} returned: {combined}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"ADB connect error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DisconnectTcpIpAsync(string endpoint, string? customAdbPath = null)
        {
            string? adbPath = _discovery.FindAdbPath(customAdbPath);
            if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
                return (false, "ADB executable not found.");

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"disconnect {endpoint.Trim()}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return (true, output.Trim());
            }
            catch (Exception ex)
            {
                return (false, $"ADB disconnect error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SwitchToTcpIpModeAsync(string usbSerial, int port = 5555, string? customAdbPath = null)
        {
            string? adbPath = _discovery.FindAdbPath(customAdbPath);
            if (string.IsNullOrEmpty(adbPath) || !File.Exists(adbPath))
                return (false, "ADB executable not found.");

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"-s {usbSerial} tcpip {port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                string combined = $"{output} {error}".Trim();
                if (combined.Contains("restarting in TCP mode", StringComparison.OrdinalIgnoreCase) ||
                    combined.Contains("listening on port", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, $"Device switched to TCP mode on port {port}. You can now disconnect the USB cable.");
                }
                return (false, $"ADB tcpip returned: {combined}");
            }
            catch (Exception ex)
            {
                return (false, $"Error setting device to tcpip mode: {ex.Message}");
            }
        }
    }
}
