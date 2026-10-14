# PocketCast Troubleshooting Guide

This guide covers common issues and resolutions when running PocketCast over USB or Wi-Fi.

---

## 1. ADB / USB Connection Issues

### Issue: "No authorized ADB devices detected!"
- **Cause 1**: USB Debugging is not enabled on the Android device.
  - *Fix*: Go to Settings -> Developer Options -> Turn on **USB Debugging**.
- **Cause 2**: The phone is prompting for authorization.
  - *Fix*: Unlock your phone screen, look for the popup *"Allow USB debugging?"*, check *"Always allow"*, and tap **OK**.
- **Cause 3**: USB mode is set to "Charge Only".
  - *Fix*: Pull down the Android notification shade, tap the USB options, and select **File Transfer (MTP)** or **MIDI** to trigger ADB re-enumeration.
- **Cause 4**: Missing ADB driver on Windows.
  - *Fix*: Install official OEM USB drivers for your phone (Samsung, Google, Xiaomi, etc.).

### Issue: "Failed to setup USB reverse"
- **Cause**: ADB daemon was interrupted or another scrcpy/adb session is holding the port.
  - *Fix*: Run `adb kill-server` and retry clicking **Setup ADB Forward**.

---

## 2. Wi-Fi Connection Issues

### Issue: "Connection timed out" or "Failed to connect to host"
- **Cause 1**: Laptop and Phone are on different subnets or Wi-Fi AP isolation is active.
  - *Fix*: Check the Wi-Fi network on both devices; ensure phone is not on cellular data or guest Wi-Fi.
- **Cause 2**: Windows Defender Firewall blocked port 8088.
  - *Fix*: Open PowerShell as Administrator and run:
    ```powershell
    New-NetFirewallRule -DisplayName "PocketCast Receiver" -Direction Inbound -LocalPort 8088 -Protocol TCP -Action Allow
    ```
- **Cause 3**: Wrong IP entered.
  - *Fix*: Use the IP address displayed under "LOCAL NETWORK IP" in the Windows PocketCast sidebar.

---

## 3. Video Display & Performance

### Issue: Black screen or "Waiting for PocketCast Stream" persists
- **Cause**: Screen capture permission prompt was dismissed or cancelled on phone.
  - *Fix*: Disconnect and tap Start Mirroring again, ensuring you tap **Start now** on Android.

### Issue: FFmpeg decoder error or missing FFmpeg
- **Cause**: `ffmpeg.exe` is not found in Windows system PATH.
  - *Fix*: Install FFmpeg using `winget install Gyan.FFmpeg` or download from `ffmpeg.org` and add its `bin` folder to system PATH.

### Issue: High latency or frame stuttering over Wi-Fi
- **Fix**: Switch from 2.4 GHz Wi-Fi to a 5 GHz Wi-Fi band, or use USB Mode for zero wireless interference.

---

## 4. Android Limitations & DRM Content

### DRM Protected Content (Netflix, Disney+, Prime Video, Banking Apps)
- Under Android security policies (`FLAG_SECURE`), protected content displays as a black box during screen projection. This is an enforced operating system security standard across all non-rooted Android devices.

### Audio Streaming
- Android 10 (API 29) introduced `AudioPlaybackCaptureConfiguration`, which allows capturing app audio only if the app allows recording (`USAGE_MEDIA`, `USAGE_GAME`) and is non-DRM. Microphone audio capture can be added for legacy Android 9 and older devices.
