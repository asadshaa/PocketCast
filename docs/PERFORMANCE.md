# PocketCast Performance & Latency Optimization Guide

## 1. Latency Breakdown & Target Budget

PocketCast targets **30 ms – 100 ms** end-to-end glass-to-glass latency under typical operational conditions.

| Pipeline Stage | Typical Latency (USB Mode) | Typical Latency (Wi-Fi 5GHz) | Optimization Strategy Applied |
| :--- | :--- | :--- | :--- |
| **Android Display Capture** | 8 – 16 ms | 8 – 16 ms | Direct `VirtualDisplay` binding into MediaCodec `Surface` (zero intermediate frame copy / zero CPU buffer copy). |
| **H.264 Hardware Encoding** | 5 – 12 ms | 5 – 12 ms | MediaCodec `KEY_PRIORITY = 0` (real-time priority), `KEY_LATENCY = 0`, CBR rate control, 1s I-frame interval. |
| **Network Transport** | < 1 ms | 4 – 15 ms | `TCP_NODELAY = true` (disables Nagle's algorithm), 512KB socket buffers, packet framing header of only 10 bytes. |
| **Windows Ingest & Framing** | < 1 ms | < 1 ms | Pre-allocated single-pass framing buffer with direct stream offset calculation. |
| **FFmpeg Decoding** | 4 – 10 ms | 4 – 10 ms | `-probesize 32 -analyzeduration 0 -fflags nobuffer -flags low_delay` (prevents buffering frames before decoding). |
| **WPF Rendering** | 8 – 16 ms | 8 – 16 ms | Frozen `BitmapSource` delivered via Dispatcher thread straight to D3D compositor. |
| **Total Estimated Latency** | **~26 – 45 ms** | **~35 – 65 ms** | Sub-frame latency suitable for real-time video playback and interactive screen monitoring. |

---

## 2. Low-Latency Framing & Queue Management

### Zero Buffer Bloat
Conventional streaming systems (e.g., standard RTSP or RTMP) buffer multiple seconds of video to ensure seamless playback over fluctuating connections. For screen mirroring, accumulated buffer lag is fatal.

PocketCast eliminates buffer bloat via:
1. **Immediate Push**: Encoded NAL units are directly written to the TCP network socket as soon as `MediaCodec.Callback.onOutputBufferAvailable` triggers.
2. **TCP NoDelay**: Ensures Windows receiver receives packets immediately rather than waiting for TCP packet filling.
3. **Low-Delay FFmpeg Pipe**: FFmpeg is fed the raw Annex-B H.264 stream and instructed with `-fflags nobuffer -flags low_delay` so that every received NAL slice is parsed and decoded without awaiting future B-frames or lookaheads.

---

## 3. Presets & Bitrate Tuning

| Preset | Target Resolution | Framerate | Bitrate | Best Used For |
| :--- | :--- | :--- | :--- | :--- |
| **Compatibility** | 720p (720x1280) | 30 FPS | 2.5 Mbps | Older devices (Android 8/9/10), low-power laptops, weak 2.4GHz Wi-Fi |
| **Balanced** | 1080p (1080x1920) | 30 FPS | 4.0 Mbps | Default recommended preset: crisp video, low CPU usage on both ends |
| **Smooth** | 1080p (1080x1920) | 60 FPS | 6.0 Mbps | High-motion video playback, 60fps smooth scrolling, USB mode |
