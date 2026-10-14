# PhoneProjection — Complete Desktop Application Guide

**PhoneProjection** is a Windows 10/11 desktop control panel and launcher engineered for low-latency Android screen mirroring. It wraps `scrcpy 4.1` with a dark WPF graphical interface, designed and optimized for the **Sony Xperia XZ3 (model SOV39, Android 10)**.

---

## 🎨 Midnight Cast Design System & Animated Background

PhoneProjection features the **Midnight Cast** design language with subtle hardware-accelerated animated signal waves representing real-time screen transmission from phone to desktop:

| UI Element | Palette Name | Hex Color | Purpose |
| :--- | :--- | :--- | :--- |
| **Main Window Background** | Midnight Navy | `#0B1020` | Base deep atmospheric canvas |
| **Secondary Background** | Deep Navy | `#0E1527` | Header and footer sections |
| **Cards & Panels** | Dark Slate | `#121A2B` | Content card surfaces |
| **Elevated Surfaces** | Slate Blue | `#19243A` | Inputs, dropdowns, and button containers |
| **Borders & Dividers** | Muted Blue-Gray | `#26334B` | Subtle card framing |
| **Primary Buttons** | Electric Blue | `#2563EB` | Start Casting and main actions |
| **Primary Hover** | Brighter Blue | `#3B82F6` | Interactive hover elevation |
| **Casting Accent** | Cyan | `#22D3EE` | Active signal flow & wireless indicators |
| **Main Text** | Off White | `#F1F5F9` | High-contrast readable typography |
| **Secondary Text** | Cool Gray | `#94A3B8` | Subtitles, labels, and descriptions |
| **Connected / Success** | Emerald | `#22C55E` | Authorized devices & active streams |
| **Warning** | Amber | `#F59E0B` | Connecting status & Android 10 audio notice |
| **Error / Disconnected** | Soft Red | `#EF4444` | Unauthorized device alerts & link drops |

### Responsive Background Wave States
The `SignalWaveBackground` control adjusts dynamically according to actual device and process states:
1. **Idle**: Slow, subtle blue waves with minimal undulation.
2. **Device Detected**: Signal waves pulse cyan with increased particle flow.
3. **Connecting**: Amber pulse with accelerated particle motion.
4. **Casting Active**: Vivid cyan transmission waves flowing smoothly with glowing signal particles.
5. **Connection Lost / Unauthorized**: Red accentuation alerting the user.
6. **Efficiency Optimization**: When minimized, animations automatically pause to consume 0% background GPU/CPU cycles.

---

## 🌟 Highlights

- **Hardware Decoding in Standalone Window**: Leaves hardware-accelerated video rendering to scrcpy's SDL window, preventing WPF compositor overhead and dropped frames.
- **Dynamic Tool Resolution**: Auto-discovers `scrcpy.exe` and `adb.exe` across WinGet packages, Android SDKs, and system PATH without hardcoded username paths.
- **Safe Process Architecture**:
  - Uses `ProcessStartInfo.ArgumentList` to completely eliminate command-injection risks.
  - Spawns and kills only its own child process PID without touching unrelated ADB or scrcpy instances.
  - Asynchronous background output parsing prevents UI deadlocks.
- **Device & Transport Intelligence**:
  - Handles 0, 1, or multiple USB and Wi-Fi devices.
  - Detects `unauthorized` device states (e.g. `QV716QHQ1L`) and guides the user.
  - Labels devices clearly with icons (`🔌 USB` vs. `📶 Wi-Fi`).
- **Guided Wi-Fi (Wireless ADB) Workflow**:
  - Step 1: One-click setup `adb -s <serial> tcpip 5555`.
  - Step 2: Connect directly to the phone's LAN IP address.
  - Dedicated wireless disconnect without interrupting USB sessions.
- **Quality Presets & Granular Controls**:
  - **Balanced**: Max size 1600, Bitrate 8 Mbps (baseline configuration).
  - **Smooth**: Max size 1280, Bitrate 6 Mbps (low encoding burden).
  - **High Quality**: Max size 1920, Bitrate 12 Mbps (sharp 1080p).
  - **Custom**: User-defined resolution (480–3840px) and bitrate (1–60 Mbps).
- **Persistent Preferences**: Local JSON storage in `%APPDATA%\PhoneProjection\preferences.json`.
- **Android 10 Audio Policy**: Audio forwarding is disabled by default (`--no-audio`) as Android 10 requires specific app capture opt-ins. An experimental toggle is available with clear disclaimers.

---

## 📋 Prerequisites

1. **Operating System**: Windows 10 or Windows 11 (64-bit).
2. **Runtime / SDK**: .NET 8.0 SDK or Desktop Runtime.
3. **Engine**: `scrcpy v4.1+` (installed via `winget install Genymobile.scrcpy`).
4. **Android SDK**: `platform-tools` with `adb.exe`.
5. **Phone**: Sony Xperia XZ3 (SOV39) with **USB Debugging** enabled in Developer Options.

---

## 🚀 Building & Running

### 1. Build in Release Mode
```powershell
dotnet build PhoneProjection/PhoneProjection.sln -c Release
```

### 2. Run Automated Unit Tests
```powershell
dotnet test PhoneProjection.Tests/PhoneProjection.Tests.csproj
```

### 3. Launch Application
```powershell
# Run from source
dotnet run --project PhoneProjection/PhoneProjection.csproj -c Release

# Or run the compiled binary directly:
.\PhoneProjection\bin\Release\net8.0-windows\PhoneProjection.exe
```

---

## ⌨️ Shortcuts

- `F11` or `Alt + Enter`: Toggle Fullscreen mirroring preference.
- `Alt + F`: Switch fullscreen in active scrcpy window.
- `Alt + Q`: Exit scrcpy video window cleanly.
