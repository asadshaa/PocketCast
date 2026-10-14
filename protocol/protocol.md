# PocketCast Streaming Protocol Specification (v1.0)

## 1. Overview
PocketCast is a low-latency, real-time screen mirroring system designed for Android (Sender) to Windows (Receiver). 
The streaming protocol operates over a reliable bi-directional or uni-directional connection:
- **USB Mode**: Direct TCP over ADB port reverse/forward (`adb reverse tcp:8088 tcp:8088` or `adb forward`).
- **Wi-Fi Mode**: Standard TCP over LAN (or optional UDP streaming).

TCP is selected as the default baseline transport due to:
1. Low kernel overhead over local loopback (`adb reverse` socket) with sub-millisecond network transit time.
2. Complete absence of packet loss on USB cables and strong Wi-Fi networks.
3. Simpler integration without complex NAT traversal or multi-packet reassembly buffers.
4. Setting `TCP_NODELAY = true` disables Nagle's algorithm, yielding direct packet delivery with 10–30ms wire latency.

---

## 2. Framing Strategy & Byte Ordering
All binary integer fields are encoded in **Big-Endian (Network Byte Order)**.

Streaming frames must never be treated as boundary-delimited by TCP reads. Receiver buffers reads into a stream accumulation buffer and processes packets based on header length fields.

### 2.1 Magic Bytes & Header
Every packet starts with a 4-byte magic signature:
- `0x50 0x43 0x53 0x54` ("PCST" - PocketCast Stream)

Packet Header Structure:
```
+-----------------------------------+
| Magic (4 bytes) "PCST"            |
+-------------------+---------------+
| Version (1 byte)  | Type (1 byte) |
+-------------------+---------------+
| Payload Length (4 bytes, BigEndian|
+-----------------------------------+
| Payload (Payload Length bytes)    |
+-----------------------------------+
Total Header Size: 10 bytes
```

### 2.2 Packet Types (`Type`)
- `0x01` (`HELLO_INIT`): Handshake from Sender announcing device name, protocol version, initial display metrics.
- `0x02` (`HELLO_ACK`): Receiver handshake response acknowledging capability, desired audio/video parameters.
- `0x03` (`CODEC_CONFIG`): H.264 Parameter Sets (SPS / PPS NAL units). Sent before any video frame and upon rotation/reconnect.
- `0x04` (`VIDEO_FRAME`): Encoded H.264 video slice / Access Unit (IDR keyframe or P-frame).
- `0x05` (`HEARTBEAT`): Keepalive ping/pong packet.
- `0x06` (`DISCONNECT`): Graceful shutdown notification.

---

## 3. Packet Definitions

### 3.1 HELLO_INIT (`0x01`)
Payload:
- `ProtocolVersion` (2 bytes, uint16): Currently `1`
- `DeviceWidth` (4 bytes, uint32): Screen width in pixels
- `DeviceHeight` (4 bytes, uint32): Screen height in pixels
- `DeviceDensityDpi` (2 bytes, uint16): Display DPI
- `DeviceNameLength` (2 bytes, uint16): Length $N$ of UTF-8 device name
- `DeviceName` ($N$ bytes): Device model / manufacturer string

### 3.2 HELLO_ACK (`0x02`)
Payload:
- `Status` (1 byte): `0 = OK`, `1 = Version Incompatible`, `2 = Error`
- `ReceiverVersion` (2 bytes, uint16)

### 3.3 CODEC_CONFIG (`0x03`)
Payload:
- `Width` (4 bytes, uint32): Video encoded width
- `Height` (4 bytes, uint32): Video encoded height
- `ConfigData` (Remaining payload bytes): Contains SPS (Sequence Parameter Set) and PPS (Picture Parameter Set) NAL units formatted with Annex-B start codes (`00 00 00 01`).

### 3.4 VIDEO_FRAME (`0x04`)
Payload:
- `FrameType` (1 byte):
  - `0x01`: Keyframe (IDR frame)
  - `0x02`: Interframe (P-frame)
- `TimestampUs` (8 bytes, int64): Presentation timestamp in microseconds (PTS from `MediaCodec.BufferInfo.presentationTimeUs`)
- `FrameData` (Remaining payload bytes): Raw Annex-B NAL units (`00 00 00 01 ...`)

---

## 4. Lifecycle & Flow Control

1. **Connection**: Receiver starts listening on port (default `8088`). Sender connects.
2. **Handshake**: Sender sends `HELLO_INIT`. Receiver validates and responds with `HELLO_ACK`.
3. **Codec Setup**: Sender initializes `MediaCodec` and `VirtualDisplay`. When the first output buffer flagged `BUFFER_FLAG_CODEC_CONFIG` is produced, Sender transmits `CODEC_CONFIG`.
4. **Streaming**: MediaCodec generates H.264 NALUs. Keyframes (IDR) and P-frames are wrapped in `VIDEO_FRAME` packets and sent over TCP with `TCP_NODELAY`.
5. **Frame Dropping & Low Latency**:
   - Sender drops outdated capture frames if the network send buffer backpressures.
   - Receiver decodes immediately on arrival using FFmpeg with low-latency flags (`flags=low_delay`, `threads=1`, zero-frame reordering).
6. **Screen Rotation**: VirtualDisplay is resized or re-created with new width/height, triggering a new IDR and `CODEC_CONFIG` packet.
