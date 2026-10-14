using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using PhoneProjection.Models;

namespace PhoneProjection.Services
{
    public class ScrcpyLaunchConfig
    {
        public string? DeviceSerial { get; set; }
        public QualityPreset Preset { get; set; } = Presets.Balanced;
        public int CustomMaxSize { get; set; } = 1600;
        public int CustomBitrateMbps { get; set; } = 8;
        public int FpsCap { get; set; } = 60;
        public int VideoBufferMs { get; set; } = 0;
        public bool Fullscreen { get; set; } = false;
        public bool Borderless { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = false;
        public bool StayAwake { get; set; } = false;
        public bool EnableAudio { get; set; } = false;
        public string AudioSource { get; set; } = "playback";
        public string? CustomScrcpyPath { get; set; }
        public string? CustomAdbPath { get; set; }
    }

    public class ScrcpyProcessService : IDisposable
    {
        private readonly ExecutableDiscoveryService _discovery;
        private Process? _activeProcess;
        private readonly object _lock = new();

        public event Action<string>? LogReceived;
        public event Action<double>? FpsReported;
        public event Action<int>? ProcessExited;

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _activeProcess != null && !_activeProcess.HasExited;
                }
            }
        }

        public ScrcpyProcessService(ExecutableDiscoveryService discovery)
        {
            _discovery = discovery;
        }

        public static List<string> BuildArgumentList(ScrcpyLaunchConfig config)
        {
            var args = new List<string>
            {
                "--no-control",
                "--print-fps"
            };

            // Video buffer delay
            args.Add($"--video-buffer={Math.Max(0, config.VideoBufferMs)}");

            // Resolution & Bitrate from preset or custom values
            int maxSize = config.Preset.IsCustom ? config.CustomMaxSize : config.Preset.MaxSize;
            int bitrate = config.Preset.IsCustom ? config.CustomBitrateMbps : config.Preset.BitrateMbps;

            if (maxSize > 0)
            {
                args.Add($"--max-size={maxSize}");
            }

            if (bitrate > 0)
            {
                args.Add($"--video-bit-rate={bitrate}M");
            }

            // FPS Cap
            if (config.FpsCap > 0)
            {
                args.Add($"--max-fps={config.FpsCap}");
            }

            // Window / Display Modes
            if (config.Fullscreen)
            {
                args.Add("--fullscreen");
            }

            if (config.Borderless)
            {
                args.Add("--window-borderless");
            }

            if (config.AlwaysOnTop)
            {
                args.Add("--always-on-top");
            }

            if (config.StayAwake)
            {
                args.Add("--stay-awake");
            }

            // Audio configuration
            if (!config.EnableAudio)
            {
                args.Add("--no-audio");
            }
            else
            {
                args.Add($"--audio-source={config.AudioSource}");
            }

            // Explicit device serial
            if (!string.IsNullOrWhiteSpace(config.DeviceSerial))
            {
                args.Add("--serial");
                args.Add(config.DeviceSerial.Trim());
            }

            return args;
        }

        public (bool Success, string Message) Start(ScrcpyLaunchConfig config)
        {
            lock (_lock)
            {
                if (IsRunning)
                {
                    return (false, "Mirroring session is already running. Please stop it first.");
                }

                string? scrcpyPath = _discovery.FindScrcpyPath(config.CustomScrcpyPath);
                if (string.IsNullOrEmpty(scrcpyPath) || !File.Exists(scrcpyPath))
                {
                    return (false, "scrcpy.exe not found! Please verify installation or configure its path.");
                }

                string? adbPath = _discovery.FindAdbPath(config.CustomAdbPath);

                var argList = BuildArgumentList(config);

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = scrcpyPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    foreach (var arg in argList)
                    {
                        psi.ArgumentList.Add(arg);
                    }

                    if (!string.IsNullOrEmpty(adbPath) && File.Exists(adbPath))
                    {
                        psi.EnvironmentVariables["ADB"] = adbPath;
                    }

                    var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            ParseAndDispatchLog(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            ParseAndDispatchLog($"[WARN/ERR] {e.Data}");
                        }
                    };

                    process.Exited += (s, e) =>
                    {
                        int exitCode = -1;
                        try { exitCode = process.ExitCode; } catch { }

                        lock (_lock)
                        {
                            if (_activeProcess == process)
                            {
                                _activeProcess = null;
                            }
                        }

                        LogReceived?.Invoke($"[Process] scrcpy exited with code {exitCode}.");
                        ProcessExited?.Invoke(exitCode);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    _activeProcess = process;

                    LogReceived?.Invoke($"[Process] Started scrcpy PID {process.Id}: {scrcpyPath} {string.Join(" ", argList)}");
                    return (true, $"scrcpy started (PID {process.Id})");
                }
                catch (Exception ex)
                {
                    return (false, $"Failed to launch scrcpy: {ex.Message}");
                }
            }
        }

        private void ParseAndDispatchLog(string line)
        {
            LogReceived?.Invoke(line);

            // Match e.g. "INFO: 52 fps" or "1920x1080: 24.5 fps"
            var match = Regex.Match(line, @"(\d+(\.\d+)?)\s*fps", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double fps))
            {
                FpsReported?.Invoke(fps);
            }
        }

        public (bool Success, string Message) Stop()
        {
            lock (_lock)
            {
                if (_activeProcess == null || _activeProcess.HasExited)
                {
                    _activeProcess = null;
                    return (true, "Process already stopped.");
                }

                try
                {
                    int pid = _activeProcess.Id;
                    _activeProcess.Kill(entireProcessTree: true);
                    _activeProcess.WaitForExit(1500);
                    _activeProcess.Dispose();
                    _activeProcess = null;
                    LogReceived?.Invoke($"[Process] Cleanly stopped scrcpy PID {pid}.");
                    return (true, $"scrcpy session PID {pid} stopped.");
                }
                catch (Exception ex)
                {
                    return (false, $"Error stopping scrcpy: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
