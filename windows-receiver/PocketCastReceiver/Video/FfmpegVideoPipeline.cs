using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PocketCastReceiver.Video
{
    public class PerformanceMetrics
    {
        public double Fps { get; set; }
        public double BitrateMbps { get; set; }
        public long TotalFramesReceived { get; set; }
        public long DroppedFrames { get; set; }
        public double LatencyMs { get; set; }
    }

    public class FfmpegVideoPipeline : IDisposable
    {
        private Process? _ffmpegProcess;
        private Stream? _ffmpegInputStream;
        private Stream? _ffmpegOutputStream;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        private int _width;
        private int _height;
        private byte[]? _codecConfigHeader;

        // Statistics
        private long _framesReceived = 0;
        private long _bytesReceived = 0;
        private Stopwatch _fpsStopwatch = new Stopwatch();
        private int _fpsCounter = 0;
        private double _currentFps = 0.0;
        private double _currentBitrateMbps = 0.0;
        private double _lastLatencyMs = 0.0;

        public event Action<BitmapSource>? FrameReady;
        public event Action<PerformanceMetrics>? MetricsUpdated;
        public event Action<string>? LogMessage;

        public int CurrentWidth => _width;
        public int CurrentHeight => _height;
        public bool IsRunning => _ffmpegProcess != null && !_ffmpegProcess.HasExited;

        public FfmpegVideoPipeline()
        {
        }

        public void SetCodecConfig(int width, int height, byte[] configData)
        {
            _width = width;
            _height = height;
            _codecConfigHeader = configData;

            LogMessage?.Invoke($"[Pipeline] Codec configuration updated: {width}x{height}, config size: {configData.Length} bytes.");

            // If ffmpeg is already running with different dimensions, restart it cleanly
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                Stop();
            }

            StartFfmpeg(width, height);

            // Write codec config SPS/PPS NAL units to FFmpeg stdin immediately
            if (_ffmpegInputStream != null && _codecConfigHeader != null && _codecConfigHeader.Length > 0)
            {
                try
                {
                    _ffmpegInputStream.Write(_codecConfigHeader, 0, _codecConfigHeader.Length);
                    _ffmpegInputStream.Flush();
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"[Pipeline] Error feeding SPS/PPS to FFmpeg: {ex.Message}");
                }
            }
        }

        private void StartFfmpeg(int width, int height)
        {
            try
            {
                _cts = new CancellationTokenSource();

                // Ultra low-latency FFmpeg arguments:
                // -probesize 32 -analyzeduration 0: instantly recognize H.264 Annex-B stream without buffer delay
                // -fflags nobuffer: discard internal input buffering
                // -flags low_delay: turn off multi-frame lookahead
                // -f h264 -i pipe:0: read raw H.264 stream from standard input
                // -f rawvideo -pix_fmt bgr24 pipe:1: output uncompressed raw BGR24 frames to standard output for direct WPF WriteableBitmap display
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-probesize 32 -analyzeduration 0 -fflags nobuffer -flags low_delay -f h264 -i pipe:0 -f rawvideo -pix_fmt bgr24 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _ffmpegProcess = new Process { StartInfo = startInfo };
                _ffmpegProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Filter FFmpeg logs
                        if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            e.Data.Contains("warning", StringComparison.OrdinalIgnoreCase))
                        {
                            LogMessage?.Invoke($"[FFmpeg] {e.Data}");
                        }
                    }
                };

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();

                _ffmpegInputStream = _ffmpegProcess.StandardInput.BaseStream;
                _ffmpegOutputStream = _ffmpegProcess.StandardOutput.BaseStream;

                _fpsStopwatch.Restart();
                _framesReceived = 0;
                _bytesReceived = 0;

                _readLoopTask = Task.Run(() => OutputReaderLoop(width, height, _cts.Token));
                LogMessage?.Invoke($"[Pipeline] FFmpeg low-latency decoder process started (PID {_ffmpegProcess.Id})");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Pipeline] Failed to launch FFmpeg: {ex.Message}. Ensure ffmpeg.exe is installed and in PATH.");
            }
        }

        public void PushFrame(byte[] frameData, byte frameType, long timestampUs)
        {
            if (_ffmpegInputStream == null || _ffmpegProcess == null || _ffmpegProcess.HasExited)
                return;

            try
            {
                // Calculate latency if timestamp is provided from sender
                long nowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
                if (timestampUs > 0 && nowUs >= timestampUs)
                {
                    double diffMs = (nowUs - timestampUs) / 1000.0;
                    if (diffMs >= 0 && diffMs < 5000)
                    {
                        _lastLatencyMs = _lastLatencyMs == 0 ? diffMs : (_lastLatencyMs * 0.9 + diffMs * 0.1);
                    }
                }

                _ffmpegInputStream.Write(frameData, 0, frameData.Length);
                _ffmpegInputStream.Flush();

                _framesReceived++;
                _fpsCounter++;
                _bytesReceived += frameData.Length;

                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    double elapsedSec = _fpsStopwatch.ElapsedMilliseconds / 1000.0;
                    _currentFps = _fpsCounter / elapsedSec;
                    _currentBitrateMbps = (_bytesReceived * 8.0) / (elapsedSec * 1_000_000.0);

                    _fpsCounter = 0;
                    _bytesReceived = 0;
                    _fpsStopwatch.Restart();

                    MetricsUpdated?.Invoke(new PerformanceMetrics
                    {
                        Fps = _currentFps,
                        BitrateMbps = _currentBitrateMbps,
                        TotalFramesReceived = _framesReceived,
                        LatencyMs = _lastLatencyMs
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Pipeline] Failed writing frame to FFmpeg: {ex.Message}");
            }
        }

        private void OutputReaderLoop(int width, int height, CancellationToken token)
        {
            int frameBytes = width * height * 3; // BGR24: 3 bytes per pixel
            byte[] rawBuffer = new byte[frameBytes];

            while (!token.IsCancellationRequested && _ffmpegOutputStream != null)
            {
                try
                {
                    int totalRead = 0;
                    while (totalRead < frameBytes)
                    {
                        int read = _ffmpegOutputStream.Read(rawBuffer, totalRead, frameBytes - totalRead);
                        if (read <= 0)
                        {
                            return; // End of stream or process closed
                        }
                        totalRead += read;
                    }

                    // Raw frame completely read
                    byte[] frameCopy = new byte[frameBytes];
                    Buffer.BlockCopy(rawBuffer, 0, frameCopy, 0, frameBytes);

                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var bitmap = BitmapSource.Create(
                                width,
                                height,
                                96,
                                96,
                                System.Windows.Media.PixelFormats.Bgr24,
                                null,
                                frameCopy,
                                width * 3
                            );
                            bitmap.Freeze();
                            FrameReady?.Invoke(bitmap);
                        }
                        catch
                        {
                            // Ignore rendering errors on shutdown
                        }
                    }));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        LogMessage?.Invoke($"[Pipeline] Frame read loop error: {ex.Message}");
                    }
                    break;
                }
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                if (_ffmpegInputStream != null)
                {
                    try { _ffmpegInputStream.Close(); } catch { }
                    _ffmpegInputStream = null;
                }

                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    try
                    {
                        _ffmpegProcess.Kill(entireProcessTree: true);
                        _ffmpegProcess.WaitForExit(1000);
                    }
                    catch { }
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
                _readLoopTask = null;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Pipeline] Error stopping FFmpeg: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
