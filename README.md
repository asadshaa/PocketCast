# PocketCast — Low-Latency Android Screen Mirroring for Windows

PocketCast is a high-performance, low-latency alternative to scrcpy with an intuitive modern graphical interface, designed specifically for watching videos and viewing your Android phone's screen on Windows 10/11 laptops over USB or local Wi-Fi.

---

## ✨ Features

- **Ultra-Low Latency**: Sub-50ms glass-to-glass latency over USB and sub-70ms over local Wi-Fi.
- **Hardware-Accelerated H.264**: Direct `VirtualDisplay` to Android `MediaCodec` encoding with hardware acceleration.
- **Zero-Buffer Video Pipeline**: Windows receiver uses low-delay FFmpeg streaming with uncompressed D3D bitmap blitting.
- **USB & Wi-Fi Support**:
  - **USB Mode**: 1-click automated ADB reverse port setup with zero-configuration device routing.
  - **Wi-Fi Mode**: High-throughput wireless streaming over your home or office local network.
- **Dynamic Quality Presets**:
  - *Balanced*: 1080p @ 30 FPS, 4 Mbps (crisp & efficient)
  - *Smooth*: 1080p @ 60 FPS, 6 Mbps (ideal for video playback & fast animations)
  - *Compatibility*: 720p @ 30 FPS, 2.5 Mbps (for older hardware & weak signals)
- **Real-Time Telemetry**: Live FPS, Bitrate (Mbps), latency estimation, and connection logs displayed in the receiver UI.
- **No Root Required**: Built 100% on standard Android `MediaProjection` APIs.
- **100% Offline & Private**: No cloud servers, no account registration, no telemetry tracking, and zero internet connection required.

---

## 📂 Repository Structure

```
ScreenCastX/
├── android-sender/                 # Android Kotlin Sender App
│   ├── app/
│   │   ├── src/main/
│   │   │   ├── AndroidManifest.xml
│   │   │   ├── java/com/example/pocketcast/
│   │   │   │   ├── encoder/        # MediaCodec & VirtualDisplay screen capture
│   │   │   │   ├── network/        # Low-latency TCP streaming client
│   │   │   │   ├── protocol/       # Binary packet protocol definitions
│   │   │   │   ├── service/        # Android Foreground Service for mediaProjection
│   │   │   │   └── ui/             # MainActivity with preset controls
│   │   │   └── res/                # Modern dark-mode layout & resources
│   │   └── build.gradle.kts
│   ├── build.gradle.kts
│   └── settings.gradle.kts
├── windows-receiver/               # Windows C# / WPF Receiver App (.NET 8)
│   ├── PocketCastReceiver.sln
│   └── PocketCastReceiver/
│       ├── MainWindow.xaml         # Modern dark-themed dashboard & video viewport
│       ├── MainWindow.xaml.cs      # Event coordinator and UI logic
│       ├── Networking/             # TCP socket listener & packet framing parser
│       ├── Protocol/               # Packet headers & binary protocol contracts
│       ├── Services/               # Automatic ADB device detection & reverse setup
│       └── Video/                  # Low-latency FFmpeg H.264 decoding pipeline
├── protocol/
│   └── protocol.md                 # Complete binary protocol specification (v1.0)
├── docs/
│   ├── SETUP.md                    # Step-by-step USB & Wi-Fi instructions
│   ├── TROUBLESHOOTING.md          # Diagnostic tips & common fixes
│   └── PERFORMANCE.md              # Latency budget & architecture benchmarks
└── README.md
```

---

## 🚀 Quick Start

### 1. Windows Receiver
```powershell
# Navigate to windows-receiver
cd windows-receiver

# Build and run with .NET 8
dotnet run --project PocketCastReceiver/PocketCastReceiver.csproj
```
1. Click **Setup ADB Forward** (for USB mode) or note the displayed Local IP (for Wi-Fi mode).
2. Click **Start Receiver**.

### 2. Android Sender
1. Open `android-sender` in Android Studio or compile using Gradle:
   ```powershell
   gradle assembleDebug
   ```
2. Install the APK to your phone:
   ```powershell
   adb install app/build/outputs/apk/debug/app-debug.apk
   ```
3. Open **PocketCast** on your phone, choose your preset, and tap **Start Mirroring**.

---

## 🔒 Security & Privacy

- All screen mirroring packets stay entirely within your physical USB cable or local private network.
- Standard Android consent prompts are strictly respected; no background capture can run without explicit user authorization.
- DRM-protected video playback (e.g. Widevine L1) will remain black according to Android platform security specifications.

---

## 📄 License
This project is open-source and released under the MIT License.
