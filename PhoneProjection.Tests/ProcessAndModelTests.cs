using System.IO;
using PhoneProjection.Models;
using PhoneProjection.Services;
using Xunit;

namespace PhoneProjection.Tests
{
    public class ProcessAndModelTests
    {
        [Fact]
        public void DeviceInfo_ParseAuthorizedDevice_SetsPropertiesCorrectly()
        {
            string line = "QV716QHQ1L             device product:SOV39 model:SOV39 device:SOV39 transport_id:27";
            var dev = DeviceInfo.Parse(line);

            Assert.NotNull(dev);
            Assert.Equal("QV716QHQ1L", dev.Serial);
            Assert.Equal("device", dev.State);
            Assert.True(dev.IsAuthorized);
            Assert.Equal("SOV39", dev.Model);
            Assert.Equal(TransportType.Usb, dev.Transport);
            Assert.Contains("SOV39 [QV716QHQ1L]", dev.DisplayName);
        }

        [Fact]
        public void DeviceInfo_ParseWifiDevice_SetsTransportTypeWifi()
        {
            string line = "192.168.1.55:5555      device product:SOV39 model:SOV39 device:SOV39 transport_id:28";
            var dev = DeviceInfo.Parse(line);

            Assert.NotNull(dev);
            Assert.Equal("192.168.1.55:5555", dev.Serial);
            Assert.Equal(TransportType.WifiTcpIp, dev.Transport);
            Assert.Contains("Wi-Fi", dev.DisplayName);
        }

        [Fact]
        public void DeviceInfo_ParseUnauthorizedDevice_IndicatesActionRequired()
        {
            string line = "QV716QHQ1L             unauthorized transport_id:27";
            var dev = DeviceInfo.Parse(line);

            Assert.NotNull(dev);
            Assert.Equal("QV716QHQ1L", dev.Serial);
            Assert.False(dev.IsAuthorized);
            Assert.Contains("UNAUTHORIZED", dev.DisplayName);
            Assert.Contains("Action Required", dev.DisplayName);
        }

        [Fact]
        public void BuildArgumentList_BalancedPreset_ConstructsExactFlags()
        {
            var config = new ScrcpyLaunchConfig
            {
                DeviceSerial = "QV716QHQ1L",
                Preset = Presets.Balanced,
                FpsCap = 60,
                VideoBufferMs = 0,
                Fullscreen = false,
                EnableAudio = false
            };

            var args = ScrcpyProcessService.BuildArgumentList(config);

            Assert.Contains("--no-control", args);
            Assert.Contains("--video-buffer=0", args);
            Assert.Contains("--print-fps", args);
            Assert.Contains("--max-size=1600", args);
            Assert.Contains("--video-bit-rate=8M", args);
            Assert.Contains("--max-fps=60", args);
            Assert.Contains("--no-audio", args);
            Assert.Contains("--serial", args);
            Assert.Contains("QV716QHQ1L", args);
            Assert.DoesNotContain("--fullscreen", args);
        }

        [Fact]
        public void BuildArgumentList_CustomPreset_UsesCustomSizeAndBitrate()
        {
            var config = new ScrcpyLaunchConfig
            {
                DeviceSerial = "QV716QHQ1L",
                Preset = Presets.Custom,
                CustomMaxSize = 1440,
                CustomBitrateMbps = 10,
                FpsCap = 30,
                VideoBufferMs = 25,
                Fullscreen = true,
                Borderless = true,
                StayAwake = true
            };

            var args = ScrcpyProcessService.BuildArgumentList(config);

            Assert.Contains("--max-size=1440", args);
            Assert.Contains("--video-bit-rate=10M", args);
            Assert.Contains("--max-fps=30", args);
            Assert.Contains("--video-buffer=25", args);
            Assert.Contains("--fullscreen", args);
            Assert.Contains("--window-borderless", args);
            Assert.Contains("--stay-awake", args);
        }

        [Fact]
        public void BuildArgumentList_AudioExplicitlyControlled()
        {
            var config = new ScrcpyLaunchConfig
            {
                EnableAudio = true,
                AudioSource = "playback"
            };

            var args = ScrcpyProcessService.BuildArgumentList(config);
            Assert.Contains("--audio-source=playback", args);
            Assert.DoesNotContain("--no-audio", args);
        }

        [Fact]
        public void SettingsService_CanSaveAndLoadPreferences()
        {
            var service = new SettingsService();
            var prefs = new UserPreferences
            {
                SelectedPresetName = "Smooth",
                CustomMaxSize = 1280,
                CustomBitrateMbps = 6,
                SelectedFps = 30,
                IsFullscreen = true,
                LastWifiIp = "192.168.1.100",
                LastWifiPort = 5555
            };

            service.Save(prefs);
            var loaded = service.Load();

            Assert.NotNull(loaded);
            Assert.Equal("Smooth", loaded.SelectedPresetName);
            Assert.Equal(1280, loaded.CustomMaxSize);
            Assert.Equal(6, loaded.CustomBitrateMbps);
            Assert.Equal(30, loaded.SelectedFps);
            Assert.True(loaded.IsFullscreen);
            Assert.Equal("192.168.1.100", loaded.LastWifiIp);
        }

        [Fact]
        public void ExecutableDiscovery_FindsAdbAndScrcpyOnSystem()
        {
            var discovery = new ExecutableDiscoveryService();
            string? scrcpy = discovery.FindScrcpyPath();
            string? adb = discovery.FindAdbPath();

            Assert.NotNull(scrcpy);
            Assert.True(File.Exists(scrcpy));

            Assert.NotNull(adb);
            Assert.True(File.Exists(adb));
        }

        [Fact]
        public void BuildArgumentList_AlwaysOnTop_ConstructsCorrectFlag()
        {
            var config = new ScrcpyLaunchConfig
            {
                DeviceSerial = "QV716QHQ1L",
                AlwaysOnTop = true,
                Borderless = true
            };

            var args = ScrcpyProcessService.BuildArgumentList(config);
            Assert.Contains("--always-on-top", args);
            Assert.Contains("--window-borderless", args);
        }

        [Fact]
        public void QualityPreset_AllPresets_IncludeVideoAndLowBandwidth()
        {
            Assert.Contains(Presets.All, p => p.Name == "Video Playback" && p.MaxSize == 1600 && p.BitrateMbps == 10);
            Assert.Contains(Presets.All, p => p.Name == "Low Bandwidth" && p.MaxSize == 1024 && p.BitrateMbps == 3);
            Assert.Contains(Presets.All, p => p.Name == "Balanced" && p.MaxSize == 1600 && p.BitrateMbps == 8);
            Assert.Contains(Presets.All, p => p.Name == "Smooth" && p.MaxSize == 1280 && p.BitrateMbps == 6);
        }

        [Fact]
        public void DiagnosticExportService_GeneratesSanitizedReport()
        {
            var dev = new DeviceInfo
            {
                Serial = "TEST_SERIAL_123",
                Model = "SOV39",
                State = "device"
            };

            string report = DiagnosticExportService.GenerateReport(
                dev,
                Presets.Balanced,
                60,
                58.4,
                true,
                @"C:\tools\scrcpy.exe",
                @"C:\tools\adb.exe",
                "Log line 1\nLog line 2"
            );

            Assert.Contains("PHONEPROJECTION DIAGNOSTIC REPORT", report);
            Assert.Contains("TEST_SERIAL_123", report);
            Assert.Contains("SOV39", report);
            Assert.Contains("Balanced", report);
            Assert.Contains("58.4 FPS", report);
            Assert.Contains("Log line 1", report);
        }
    }
}
