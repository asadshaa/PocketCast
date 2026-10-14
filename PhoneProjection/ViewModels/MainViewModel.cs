using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using PhoneProjection.Models;
using PhoneProjection.Services;

namespace PhoneProjection.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ExecutableDiscoveryService _discovery;
        private readonly AdbDeviceService _adbService;
        private readonly ScrcpyProcessService _scrcpyService;
        private readonly SettingsService _settingsService;
        private UserPreferences _prefs;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Collections
        public ObservableCollection<DeviceInfo> Devices { get; } = new();
        public ObservableCollection<QualityPreset> PresetsList { get; } = new(Presets.All);
        public ObservableCollection<int> FpsOptions { get; } = new(new[] { 30, 60 });
        public ObservableCollection<string> AudioSources { get; } = new(new[] { "playback", "mic", "output" });

        private DeviceInfo? _selectedDevice;
        public DeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged();
                    UpdateStatusFromDevice();
                }
            }
        }

        private QualityPreset _selectedPreset = Presets.Balanced;
        public QualityPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value && value != null)
                {
                    _selectedPreset = value;
                    _prefs.SelectedPresetName = value.Name;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomPresetSelected));
                    SavePreferences();
                }
            }
        }

        public bool IsCustomPresetSelected => SelectedPreset.IsCustom;

        private int _customMaxSize = 1600;
        public int CustomMaxSize
        {
            get => _customMaxSize;
            set
            {
                if (_customMaxSize != value)
                {
                    _customMaxSize = Math.Clamp(value, 480, 3840);
                    _prefs.CustomMaxSize = _customMaxSize;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private int _customBitrateMbps = 8;
        public int CustomBitrateMbps
        {
            get => _customBitrateMbps;
            set
            {
                if (_customBitrateMbps != value)
                {
                    _customBitrateMbps = Math.Clamp(value, 1, 60);
                    _prefs.CustomBitrateMbps = _customBitrateMbps;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private int _selectedFps = 60;
        public int SelectedFps
        {
            get => _selectedFps;
            set
            {
                if (_selectedFps != value)
                {
                    _selectedFps = value;
                    _prefs.SelectedFps = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private int _videoBufferMs = 0;
        public int VideoBufferMs
        {
            get => _videoBufferMs;
            set
            {
                if (_videoBufferMs != value)
                {
                    _videoBufferMs = Math.Max(0, value);
                    _prefs.VideoBufferMs = _videoBufferMs;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private bool _isFullscreen;
        public bool IsFullscreen
        {
            get => _isFullscreen;
            set
            {
                if (_isFullscreen != value)
                {
                    _isFullscreen = value;
                    _prefs.IsFullscreen = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private bool _isBorderless;
        public bool IsBorderless
        {
            get => _isBorderless;
            set
            {
                if (_isBorderless != value)
                {
                    _isBorderless = value;
                    _prefs.IsBorderless = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private bool _stayAwake;
        public bool StayAwake
        {
            get => _stayAwake;
            set
            {
                if (_stayAwake != value)
                {
                    _stayAwake = value;
                    _prefs.StayAwake = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private bool _enableAudio;
        public bool EnableAudio
        {
            get => _enableAudio;
            set
            {
                if (_enableAudio != value)
                {
                    _enableAudio = value;
                    _prefs.EnableAudio = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private string _selectedAudioSource = "playback";
        public string SelectedAudioSource
        {
            get => _selectedAudioSource;
            set
            {
                if (_selectedAudioSource != value)
                {
                    _selectedAudioSource = value;
                    _prefs.AudioSource = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        // Wi-Fi Pairing/Connect Inputs
        private string _wifiIp = string.Empty;
        public string WifiIp
        {
            get => _wifiIp;
            set
            {
                if (_wifiIp != value)
                {
                    _wifiIp = value;
                    _prefs.LastWifiIp = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private int _wifiPort = 5555;
        public int WifiPort
        {
            get => _wifiPort;
            set
            {
                if (_wifiPort != value)
                {
                    _wifiPort = value;
                    _prefs.LastWifiPort = value;
                    OnPropertyChanged();
                    SavePreferences();
                }
            }
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _statusColor = "#10B981";
        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _reportedFpsText = "--";
        public string ReportedFpsText
        {
            get => _reportedFpsText;
            set
            {
                if (_reportedFpsText != value)
                {
                    _reportedFpsText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _logOutput = string.Empty;
        public string LogOutput
        {
            get => _logOutput;
            set
            {
                if (_logOutput != value)
                {
                    _logOutput = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMirroringActive => _scrcpyService.IsRunning;

        // Custom paths
        private string _customScrcpyPath = string.Empty;
        public string CustomScrcpyPath
        {
            get => _customScrcpyPath;
            set
            {
                if (_customScrcpyPath != value)
                {
                    _customScrcpyPath = value;
                    _prefs.CustomScrcpyPath = value;
                    OnPropertyChanged();
                    RefreshDiscoveryPaths();
                    SavePreferences();
                }
            }
        }

        private string _customAdbPath = string.Empty;
        public string CustomAdbPath
        {
            get => _customAdbPath;
            set
            {
                if (_customAdbPath != value)
                {
                    _customAdbPath = value;
                    _prefs.CustomAdbPath = value;
                    OnPropertyChanged();
                    RefreshDiscoveryPaths();
                    SavePreferences();
                }
            }
        }

        private string _resolvedScrcpyPath = "Detecting...";
        public string ResolvedScrcpyPath
        {
            get => _resolvedScrcpyPath;
            set
            {
                if (_resolvedScrcpyPath != value)
                {
                    _resolvedScrcpyPath = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _resolvedAdbPath = "Detecting...";
        public string ResolvedAdbPath
        {
            get => _resolvedAdbPath;
            set
            {
                if (_resolvedAdbPath != value)
                {
                    _resolvedAdbPath = value;
                    OnPropertyChanged();
                }
            }
        }

        // Commands
        public ICommand RefreshDevicesCommand { get; }
        public ICommand StartMirroringCommand { get; }
        public ICommand StopMirroringCommand { get; }
        public ICommand RestartMirroringCommand { get; }
        public ICommand ConnectWifiCommand { get; }
        public ICommand DisconnectWifiCommand { get; }
        public ICommand EnableUsbTcpIpCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public MainViewModel()
        {
            _discovery = new ExecutableDiscoveryService();
            _adbService = new AdbDeviceService(_discovery);
            _scrcpyService = new ScrcpyProcessService(_discovery);
            _settingsService = new SettingsService();

            _prefs = _settingsService.Load();
            ApplyLoadedPreferences();

            _scrcpyService.LogReceived += OnLog;
            _scrcpyService.FpsReported += OnFpsReported;
            _scrcpyService.ProcessExited += OnProcessExited;

            RefreshDevicesCommand = new RelayCommand(async () => await RefreshDevicesAsync(), () => !IsBusy);
            StartMirroringCommand = new RelayCommand(StartMirroring, () => !IsMirroringActive && !IsBusy);
            StopMirroringCommand = new RelayCommand(StopMirroring, () => IsMirroringActive);
            RestartMirroringCommand = new RelayCommand(RestartMirroring, () => !IsBusy);
            ConnectWifiCommand = new RelayCommand(async () => await ConnectWifiAsync(), () => !IsBusy);
            DisconnectWifiCommand = new RelayCommand(async () => await DisconnectWifiAsync(), () => !IsBusy);
            EnableUsbTcpIpCommand = new RelayCommand(async () => await EnableUsbTcpIpAsync(), () => !IsBusy);
            ClearLogsCommand = new RelayCommand(() => LogOutput = string.Empty);

            RefreshDiscoveryPaths();
            _ = RefreshDevicesAsync();
        }

        private void ApplyLoadedPreferences()
        {
            _customMaxSize = _prefs.CustomMaxSize;
            _customBitrateMbps = _prefs.CustomBitrateMbps;
            _selectedFps = _prefs.SelectedFps;
            _videoBufferMs = _prefs.VideoBufferMs;
            _isFullscreen = _prefs.IsFullscreen;
            _isBorderless = _prefs.IsBorderless;
            _stayAwake = _prefs.StayAwake;
            _enableAudio = _prefs.EnableAudio;
            _selectedAudioSource = _prefs.AudioSource;
            _customScrcpyPath = _prefs.CustomScrcpyPath;
            _customAdbPath = _prefs.CustomAdbPath;
            _wifiIp = _prefs.LastWifiIp;
            _wifiPort = _prefs.LastWifiPort > 0 ? _prefs.LastWifiPort : 5555;

            foreach (var p in PresetsList)
            {
                if (string.Equals(p.Name, _prefs.SelectedPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedPreset = p;
                    break;
                }
            }
        }

        private void SavePreferences()
        {
            _settingsService.Save(_prefs);
        }

        public void RefreshDiscoveryPaths()
        {
            ResolvedScrcpyPath = _discovery.FindScrcpyPath(CustomScrcpyPath) ?? "Not found (Install via WinGet or configure path)";
            ResolvedAdbPath = _discovery.FindAdbPath(CustomAdbPath) ?? "Not found (Install Android SDK or scrcpy)";
        }

        public async Task RefreshDevicesAsync()
        {
            IsBusy = true;
            StatusText = "Scanning ADB devices...";
            StatusColor = "#F59E0B";

            var (success, list, _, error) = await _adbService.GetConnectedDevicesAsync(CustomAdbPath);
            Devices.Clear();

            if (!success)
            {
                StatusText = error;
                StatusColor = "#EF4444";
                AppendLog($"[ADB Error] {error}");
            }
            else
            {
                foreach (var d in list)
                {
                    Devices.Add(d);
                }

                if (Devices.Count == 0)
                {
                    StatusText = "No devices detected. Check USB connection & debugging.";
                    StatusColor = "#64748B";
                    SelectedDevice = null;
                }
                else
                {
                    // Preserve selection if possible
                    SelectedDevice = Devices[0];
                    UpdateStatusFromDevice();
                }
            }

            IsBusy = false;
        }

        private void UpdateStatusFromDevice()
        {
            if (IsMirroringActive) return;

            if (SelectedDevice == null)
            {
                StatusText = "No device selected";
                StatusColor = "#64748B";
                return;
            }

            if (!SelectedDevice.IsAuthorized)
            {
                StatusText = $"Device [{SelectedDevice.Serial}] UNAUTHORIZED! Check phone screen and tap 'Allow'.";
                StatusColor = "#EF4444";
            }
            else
            {
                StatusText = $"Ready: {SelectedDevice.DisplayName}";
                StatusColor = "#10B981";
            }
        }

        private void StartMirroring()
        {
            if (SelectedDevice != null && !SelectedDevice.IsAuthorized)
            {
                StatusText = "Cannot start: Phone is unauthorized. Unlock and allow USB debugging on phone.";
                StatusColor = "#EF4444";
                AppendLog("[Security] Aborted launch: Android authorization required on device screen.");
                return;
            }

            var config = new ScrcpyLaunchConfig
            {
                DeviceSerial = SelectedDevice?.Serial,
                Preset = SelectedPreset,
                CustomMaxSize = CustomMaxSize,
                CustomBitrateMbps = CustomBitrateMbps,
                FpsCap = SelectedFps,
                VideoBufferMs = VideoBufferMs,
                Fullscreen = IsFullscreen,
                Borderless = IsBorderless,
                StayAwake = StayAwake,
                EnableAudio = EnableAudio,
                AudioSource = SelectedAudioSource,
                CustomScrcpyPath = CustomScrcpyPath,
                CustomAdbPath = CustomAdbPath
            };

            var (success, msg) = _scrcpyService.Start(config);
            if (success)
            {
                StatusText = $"Mirroring active: {SelectedDevice?.DisplayName ?? "Default"}";
                StatusColor = "#10B981";
            }
            else
            {
                StatusText = msg;
                StatusColor = "#EF4444";
                AppendLog($"[Error] {msg}");
            }

            OnPropertyChanged(nameof(IsMirroringActive));
        }

        private void StopMirroring()
        {
            var (success, msg) = _scrcpyService.Stop();
            StatusText = msg;
            StatusColor = "#F59E0B";
            ReportedFpsText = "--";
            OnPropertyChanged(nameof(IsMirroringActive));
        }

        private void RestartMirroring()
        {
            if (IsMirroringActive)
            {
                StopMirroring();
            }
            StartMirroring();
        }

        public async Task ConnectWifiAsync()
        {
            if (string.IsNullOrWhiteSpace(WifiIp))
            {
                StatusText = "Please enter phone Wi-Fi IP address.";
                StatusColor = "#EF4444";
                return;
            }

            IsBusy = true;
            StatusText = $"Connecting to {WifiIp}:{WifiPort} over Wi-Fi...";
            StatusColor = "#F59E0B";

            var (success, msg) = await _adbService.ConnectTcpIpAsync(WifiIp, WifiPort, CustomAdbPath);
            AppendLog($"[Wi-Fi] {msg}");

            if (success)
            {
                StatusText = msg;
                StatusColor = "#10B981";
                await RefreshDevicesAsync();
            }
            else
            {
                StatusText = msg;
                StatusColor = "#EF4444";
            }

            IsBusy = false;
        }

        public async Task DisconnectWifiAsync()
        {
            string endpoint = $"{WifiIp.Trim()}:{WifiPort}";
            if (SelectedDevice != null && SelectedDevice.Transport == TransportType.WifiTcpIp)
            {
                endpoint = SelectedDevice.Serial;
            }

            IsBusy = true;
            StatusText = $"Disconnecting {endpoint}...";
            var (success, msg) = await _adbService.DisconnectTcpIpAsync(endpoint, CustomAdbPath);
            AppendLog($"[Wi-Fi] {msg}");
            await RefreshDevicesAsync();
            IsBusy = false;
        }

        public async Task EnableUsbTcpIpAsync()
        {
            if (SelectedDevice == null || SelectedDevice.Transport != TransportType.Usb)
            {
                StatusText = "Select a USB-connected phone first to enable TCP/IP mode.";
                StatusColor = "#EF4444";
                return;
            }

            IsBusy = true;
            StatusText = $"Enabling TCP mode on port {WifiPort}...";
            var (success, msg) = await _adbService.SwitchToTcpIpModeAsync(SelectedDevice.Serial, WifiPort, CustomAdbPath);
            AppendLog($"[Wi-Fi Setup] {msg}");
            StatusText = msg;
            StatusColor = success ? "#10B981" : "#EF4444";
            IsBusy = false;
        }

        private void OnProcessExited(int exitCode)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsMirroringActive));
                ReportedFpsText = "--";

                if (exitCode == 0)
                {
                    StatusText = "Mirroring session ended cleanly.";
                    StatusColor = "#10B981";
                }
                else if (exitCode == 2)
                {
                    StatusText = "Phone disconnected or USB/Wi-Fi link interrupted.";
                    StatusColor = "#EF4444";
                }
                else
                {
                    StatusText = $"Mirroring stopped (Exit code {exitCode}).";
                    StatusColor = "#F59E0B";
                }
                _ = RefreshDevicesAsync();
            });
        }

        private void OnFpsReported(double fps)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                ReportedFpsText = $"{fps:F1} FPS (Reported)";
            });
        }

        private void OnLog(string line)
        {
            App.Current?.Dispatcher.Invoke(() => AppendLog(line));
        }

        private void AppendLog(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogOutput += $"[{time}] {message}\n";
        }

        public void Cleanup()
        {
            _scrcpyService.Dispose();
        }
    }
}
