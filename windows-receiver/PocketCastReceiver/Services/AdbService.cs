using System;
using System.Diagnostics;
using System.IO;

namespace PocketCastReceiver.Services
{
    public class AdbService
    {
        private string _adbPath = "adb.exe";

        public AdbService()
        {
            // Detect adb location
            string localSdkAdb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Android", "Sdk", "platform-tools", "adb.exe"
            );

            if (File.Exists(localSdkAdb))
            {
                _adbPath = localSdkAdb;
            }
        }

        public string GetAdbPath() => _adbPath;

        public (bool Success, string Output) RunAdbCommand(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    return (true, string.IsNullOrWhiteSpace(output) ? "Command succeeded." : output.Trim());
                }
                else
                {
                    return (false, $"Error (code {process.ExitCode}): {error} {output}".Trim());
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to execute ADB: {ex.Message}");
            }
        }

        public (bool HasDevices, string DeviceList) CheckDevices()
        {
            var res = RunAdbCommand("devices -l");
            if (!res.Success)
            {
                return (false, res.Output);
            }

            string[] lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("List of devices") || string.IsNullOrWhiteSpace(line))
                    continue;
                count++;
            }

            return (count > 0, res.Output);
        }

        public (bool Success, string Message) SetupUsbReversePort(int port = 8088)
        {
            // Reverse forwarding allows phone (sender) to connect to localhost:8088 and reach Windows receiver
            var result = RunAdbCommand($"reverse tcp:{port} tcp:{port}");
            if (result.Success)
            {
                return (true, $"USB reverse port forwarding active on tcp:{port}");
            }
            return (false, $"Failed to setup USB reverse: {result.Output}");
        }

        public void RemoveReverse(int port = 8088)
        {
            RunAdbCommand($"reverse --remove tcp:{port}");
        }
    }
}
