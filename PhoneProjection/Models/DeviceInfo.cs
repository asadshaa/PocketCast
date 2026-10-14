using System;
using System.Text.RegularExpressions;

namespace PhoneProjection.Models
{
    public enum TransportType
    {
        Usb,
        WifiTcpIp,
        Unknown
    }

    public class DeviceInfo
    {
        public string Serial { get; set; } = string.Empty;
        public string State { get; set; } = "unknown"; // device, unauthorized, offline
        public string Model { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;

        public bool IsAuthorized => string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);

        public TransportType Transport
        {
            get
            {
                if (Serial.Contains(":") && Regex.IsMatch(Serial, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d+$"))
                {
                    return TransportType.WifiTcpIp;
                }
                return TransportType.Usb;
            }
        }

        public string TransportLabel => Transport switch
        {
            TransportType.WifiTcpIp => "📶 Wi-Fi",
            TransportType.Usb => "🔌 USB",
            _ => "❓ Unknown"
        };

        public string DisplayName
        {
            get
            {
                string info = !string.IsNullOrWhiteSpace(Model) ? Model : Serial;
                if (!IsAuthorized)
                {
                    return $"{TransportLabel} {info} ({State.ToUpperInvariant()}) - Action Required";
                }
                return $"{TransportLabel} {info} [{Serial}]";
            }
        }

        public override string ToString() => DisplayName;

        public static DeviceInfo? Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                return null;

            // Example lines:
            // QV716QHQ1L             unauthorized transport_id:27
            // QV716QHQ1L             device product:SOV39 model:SOV39 device:SOV39 transport_id:27
            // 192.168.1.50:5555      device product:SOV39 model:SOV39 device:SOV39 transport_id:28
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 2)
                return null;

            var dev = new DeviceInfo
            {
                Serial = parts[0],
                State = parts[1]
            };

            for (int i = 2; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                {
                    dev.Model = parts[i].Substring("model:".Length);
                }
                else if (parts[i].StartsWith("product:", StringComparison.OrdinalIgnoreCase))
                {
                    dev.Product = parts[i].Substring("product:".Length);
                }
            }

            return dev;
        }
    }
}
