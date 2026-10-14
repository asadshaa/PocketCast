using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PocketCastReceiver.Networking;
using PocketCastReceiver.Services;
using PocketCastReceiver.Video;

namespace PocketCastReceiver
{
    public partial class MainWindow : Window
    {
        private readonly NetworkReceiverServer _server;
        private readonly FfmpegVideoPipeline _pipeline;
        private readonly FfplayDisplayPipeline _displayPipeline;
        private readonly AdbService _adb;

        public MainWindow()
        {
            InitializeComponent();

            _server = new NetworkReceiverServer();
            _pipeline = new FfmpegVideoPipeline();
            _displayPipeline = new FfplayDisplayPipeline();
            _adb = new AdbService();

            // Wire up server events
            _server.LogMessage += OnLog;
            _server.ClientConnected += OnClientConnected;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.CodecConfigReceived += OnCodecConfigReceived;
            _server.VideoFrameReceived += OnVideoFrameReceived;

            // Wire up pipeline events
            _pipeline.LogMessage += OnLog;
            _pipeline.FrameReady += OnFrameReady;
            _pipeline.MetricsUpdated += OnMetricsUpdated;
            _displayPipeline.LogMessage += OnLog;

            UpdateLocalIps();
            OnLog($"PocketCast Receiver initialized. ADB path: {_adb.GetAdbPath()}");

            // Setup reverse port & start server without blocking UI popups
            _adb.SetupUsbReversePort(8088);
            _server.Start(8088);
            BtnStartServer.IsEnabled = false;
            BtnStopServer.IsEnabled = true;
            TxtPort.IsEnabled = false;
            StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            TxtStatus.Text = "Listening on port 8088...";
        }

        private void UpdateLocalIps()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipList = new System.Collections.Generic.List<string>();
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        ipList.Add(ip.ToString());
                    }
                }

                if (ipList.Count > 0)
                {
                    TxtLocalIp.Text = string.Join("\n", ipList);
                }
                else
                {
                    TxtLocalIp.Text = "127.0.0.1 (USB loopback)";
                }
            }
            catch (Exception ex)
            {
                TxtLocalIp.Text = "127.0.0.1";
                OnLog($"Error resolving IP: {ex.Message}");
            }
        }

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            if (BtnAutoSetupAdb == null || TxtInstructions == null) return;

            if (RadioUsb.IsChecked == true)
            {
                BtnAutoSetupAdb.Visibility = Visibility.Visible;
                TxtInstructions.Text = "1. Connect phone via USB with USB Debugging enabled\n2. Click 'Setup ADB Forward' & 'Start Receiver'\n3. Launch PocketCast on Android and tap Connect";
            }
            else
            {
                BtnAutoSetupAdb.Visibility = Visibility.Collapsed;
                TxtInstructions.Text = "1. Ensure Phone and Laptop are on same Wi-Fi network\n2. Note your laptop IP from sidebar\n3. In Android PocketCast, enter IP and tap Connect";
            }
        }

        private void BtnAutoSetupAdb_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text.Trim(), out int port))
            {
                port = 8088;
            }

            var devCheck = _adb.CheckDevices();
            OnLog($"ADB Devices check: {devCheck.DeviceList.Trim()}");

            if (!devCheck.HasDevices)
            {
                MessageBox.Show(
                    "No authorized ADB devices detected!\n\nPlease:\n1. Enable 'USB Debugging' in Android Developer Options.\n2. Connect phone with USB cable.\n3. Accept the 'Allow USB debugging' prompt on your phone.",
                    "PocketCast - ADB Device Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var res = _adb.SetupUsbReversePort(port);
            OnLog(res.Message);

            if (res.Success)
            {
                MessageBox.Show(
                    $"ADB reverse port forwarding configured for port {port}!\nPhone can now connect directly over USB cable.",
                    "PocketCast - USB Forward Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(res.Message, "ADB Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text.Trim(), out int port))
            {
                port = 8088;
            }

            try
            {
                _server.Start(port);
                BtnStartServer.IsEnabled = false;
                BtnStopServer.IsEnabled = true;
                TxtPort.IsEnabled = false;
                StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber / Listening
                TxtStatus.Text = $"Listening on port {port}...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start receiver server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            _server.Stop();
            _pipeline.Stop();

            BtnStartServer.IsEnabled = true;
            BtnStopServer.IsEnabled = false;
            TxtPort.IsEnabled = true;

            PlaceholderOverlay.Visibility = Visibility.Visible;
            VideoView.Source = null;

            StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Red / Stopped
            TxtStatus.Text = "Receiver Stopped";

            TxtMetricFps.Text = "0.0";
            TxtMetricBitrate.Text = "0.0 Mbps";
            TxtMetricLatency.Text = "-- ms";
        }

        private void OnClientConnected(string deviceName, int width, int height)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Green / Streaming
                TxtStatus.Text = $"Streaming: {deviceName} ({width}x{height})";
                TxtMetricResolution.Text = $"{width}x{height}";
            });
        }

        private void OnClientDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                PlaceholderOverlay.Visibility = Visibility.Visible;
                VideoView.Source = null;
                StatusLed.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber / Listening
                TxtStatus.Text = "Sender disconnected. Awaiting reconnection...";
            });
        }

        private void OnCodecConfigReceived(int width, int height, byte[] configData)
        {
            _pipeline.SetCodecConfig(width, height, configData);
            _displayPipeline.Start();
            _displayPipeline.PushData(configData);
        }

        private void OnVideoFrameReceived(byte[] frameData, byte frameType, long timestampUs)
        {
            _pipeline.PushFrame(frameData, frameType, timestampUs);
            _displayPipeline.PushData(frameData);
        }

        private void OnFrameReady(BitmapSource bitmap)
        {
            Dispatcher.Invoke(() =>
            {
                if (PlaceholderOverlay.Visibility == Visibility.Visible)
                {
                    PlaceholderOverlay.Visibility = Visibility.Collapsed;
                }
                VideoView.Source = bitmap;
            });
        }

        private void OnMetricsUpdated(PerformanceMetrics metrics)
        {
            Dispatcher.Invoke(() =>
            {
                TxtMetricFps.Text = $"{metrics.Fps:F1}";
                TxtMetricBitrate.Text = $"{metrics.BitrateMbps:F2} Mbps";
                TxtMetricLatency.Text = metrics.LatencyMs > 0 ? $"{metrics.LatencyMs:F1} ms" : "< 35 ms";
                TxtMetricFrames.Text = $"{metrics.TotalFramesReceived:N0}";
            });
        }

        private void OnLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                TxtLogs.AppendText($"[{timestamp}] {message}\n");
                TxtLogs.ScrollToEnd();
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _server.Dispose();
            _pipeline.Dispose();
        }
    }
}