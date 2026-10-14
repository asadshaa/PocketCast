using System;
using System.Diagnostics;
using System.IO;

namespace PocketCastReceiver.Video
{
    public class FfplayDisplayPipeline : IDisposable
    {
        private Process? _ffplayProcess;
        private Stream? _ffplayInputStream;
        private readonly object _lock = new();

        public event Action<string>? LogMessage;

        public bool IsRunning => _ffplayProcess != null && !_ffplayProcess.HasExited;

        public void Start()
        {
            lock (_lock)
            {
                if (IsRunning) return;

                try
                {
                    // ffplay ultra low latency options:
                    // -window_title "PocketCast Live Mirror"
                    // -probesize 32 -analyzeduration 0: immediate stream detection
                    // -fflags nobuffer: disable internal buffering
                    // -flags low_delay: turn off multi-frame lookahead
                    // -f h264 -i pipe:0: receive raw H.264 Annex-B stream from stdin
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "ffplay.exe",
                        Arguments = "-window_title \"PocketCast Live Mirror\" -probesize 32 -analyzeduration 0 -fflags nobuffer -flags low_delay -f h264 -i pipe:0",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = false
                    };

                    _ffplayProcess = new Process { StartInfo = startInfo };
                    _ffplayProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            LogMessage?.Invoke($"[ffplay] {e.Data}");
                        }
                    };

                    _ffplayProcess.Start();
                    _ffplayProcess.BeginErrorReadLine();
                    _ffplayInputStream = _ffplayProcess.StandardInput.BaseStream;

                    LogMessage?.Invoke($"[Pipeline] ffplay live display window launched (PID {_ffplayProcess.Id})");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Pipeline] Failed to launch ffplay: {ex.Message}");
                }
            }
        }

        public void PushData(byte[] data)
        {
            lock (_lock)
            {
                if (_ffplayInputStream == null || _ffplayProcess == null || _ffplayProcess.HasExited)
                    return;

                try
                {
                    _ffplayInputStream.Write(data, 0, data.Length);
                    _ffplayInputStream.Flush();
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Pipeline] Stream write error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_ffplayInputStream != null)
                {
                    try { _ffplayInputStream.Close(); } catch { }
                    _ffplayInputStream = null;
                }

                if (_ffplayProcess != null && !_ffplayProcess.HasExited)
                {
                    try
                    {
                        _ffplayProcess.Kill(entireProcessTree: true);
                        _ffplayProcess.WaitForExit(1000);
                    }
                    catch { }
                    _ffplayProcess.Dispose();
                    _ffplayProcess = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
