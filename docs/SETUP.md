# PocketCast Setup Guide

PocketCast enables ultra-low latency screen mirroring from an Android phone (Android 8.0 through Android 14+) to a Windows 10/11 laptop over USB cable or local Wi-Fi.

---

## 1. Prerequisites

### Windows Laptop
- Windows 10 or 11 (64-bit).
- `.NET 8 Runtime` (or SDK).
- `FFmpeg` (installed and in system PATH, e.g., via `winget install Gyan.FFmpeg`).
- `ADB` (Android Debug Bridge, included in Android SDK platform-tools or downloaded standalone).

### Android Phone
- Android 8.0 (API 26) or newer (Android 10, 11, 12, 13, 14 tested).
- USB cable (for USB mode).
- Phone and laptop connected to the same Wi-Fi network (for Wi-Fi mode).

---

## 2. USB Mode Setup (Fastest & Lowest Latency: 30–50ms)

USB mode uses ADB reverse port forwarding. This channels all network packets directly over the physical USB bus with 0 network jitter.

### Step 1: Enable USB Debugging on Android
1. Open phone **Settings** -> **About Phone**.
2. Tap **Build Number** 7 times until you see *"You are now a developer!"*.
3. Go back to **Settings** -> **System** -> **Developer Options**.
4. Enable **USB Debugging**.

### Step 2: Connect to Laptop & Authorize ADB
1. Connect your phone to your Windows laptop with a USB cable.
2. An Android dialog will appear: *"Allow USB debugging?"*. Check *"Always allow from this computer"* and tap **Allow**.

### Step 3: Run Windows Receiver
1. Run `PocketCastReceiver.exe` (or launch via `dotnet run --project windows-receiver/PocketCastReceiver`).
2. Keep **USB Mode (ADB)** selected.
3. Click **Setup ADB Forward**. PocketCast verifies the device and executes `adb reverse tcp:8088 tcp:8088`.
4. Click **Start Receiver**. The status will show `Listening on port 8088...`.

### Step 4: Run Android PocketCast
1. Open PocketCast on your phone.
2. Leave Host as `127.0.0.1` and Port as `8088`.
3. Choose your quality preset (e.g. *Balanced: 1080p @ 30 FPS* or *Smooth: 1080p @ 60 FPS*).
4. Tap **Start Mirroring**.
5. When Android prompts *"Start recording or casting with PocketCast?"*, tap **Start now**.
6. The phone screen immediately mirrors on your laptop with ultra-low latency!

---

## 3. Wi-Fi Mode Setup (Wireless Mirroring)

### Step 1: Same Local Network
- Ensure your Android phone and Windows laptop are connected to the same Wi-Fi router.

### Step 2: Windows Receiver
1. Launch `PocketCastReceiver.exe`.
2. Select **Wi-Fi (LAN)**.
3. Note your laptop's Local IP displayed in the right sidebar (e.g., `192.168.1.45`).
4. Click **Start Receiver**.

### Step 3: Windows Firewall (First Run)
- If Windows Firewall prompts you when starting the receiver, check **Private networks** and click **Allow access**.

### Step 4: Android Sender
1. Launch PocketCast on Android.
2. Select **Wi-Fi (LAN)**.
3. Enter your laptop's IP address (e.g. `192.168.1.45`) and Port `8088`.
4. Tap **Start Mirroring** and approve the screen-capture prompt.
