using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PocketCastReceiver.Protocol;

namespace PocketCastReceiver.Networking
{
    public class NetworkReceiverServer : IDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _currentClient;
        private NetworkStream? _networkStream;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;

        public event Action<string>? LogMessage;
        public event Action<string, int, int>? ClientConnected; // deviceName, width, height
        public event Action? ClientDisconnected;
        public event Action<int, int, byte[]>? CodecConfigReceived; // width, height, data
        public event Action<byte[], byte, long>? VideoFrameReceived; // data, frameType, timestampUs

        public bool IsListening => _listener != null;
        public bool IsClientConnected => _currentClient != null && _currentClient.Connected;

        public void Start(int port)
        {
            Stop();

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            LogMessage?.Invoke($"[Server] Listening on 0.0.0.0:{port} (awaiting Android PocketCast sender)...");

            _listenTask = Task.Run(() => AcceptLoop(_cts.Token));
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);

                    // Configure client socket for immediate low-latency delivery
                    client.NoDelay = true; // Disable Nagle's algorithm
                    client.ReceiveBufferSize = 512 * 1024; // 512 KB receive buffer

                    var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    LogMessage?.Invoke($"[Server] Sender connected from {remoteEp}");

                    _currentClient = client;
                    _networkStream = client.GetStream();

                    await ProcessClientSessionAsync(client, _networkStream, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        LogMessage?.Invoke($"[Server] Accept loop error: {ex.Message}");
                    }
                }
            }
        }

        private async Task ProcessClientSessionAsync(TcpClient client, NetworkStream stream, CancellationToken token)
        {
            byte[] headerBuffer = new byte[ProtocolConstants.HeaderSize];

            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    // 1. Read exact 10-byte header
                    bool headerRead = await ReadExactBytesAsync(stream, headerBuffer, 0, ProtocolConstants.HeaderSize, token);
                    if (!headerRead)
                    {
                        LogMessage?.Invoke("[Server] Sender closed connection during header read.");
                        break;
                    }

                    if (!PacketHeader.TryRead(headerBuffer, 0, out var header) || header == null)
                    {
                        LogMessage?.Invoke("[Server] Malformed packet header received! Dropping connection.");
                        break;
                    }

                    // 2. Read full payload according to header.PayloadLength
                    int payloadLength = header.PayloadLength;
                    if (payloadLength < 0 || payloadLength > 20 * 1024 * 1024) // Sanity check 20MB limit
                    {
                        LogMessage?.Invoke($"[Server] Invalid payload length {payloadLength}. Dropping connection.");
                        break;
                    }

                    byte[] payload = new byte[payloadLength];
                    bool payloadRead = await ReadExactBytesAsync(stream, payload, 0, payloadLength, token);
                    if (!payloadRead)
                    {
                        LogMessage?.Invoke("[Server] Connection dropped during payload read.");
                        break;
                    }

                    // 3. Process packet according to type
                    HandlePacket(header.Type, payload, stream);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Server] Session read error: {ex.Message}");
            }
            finally
            {
                CleanupClient();
                ClientDisconnected?.Invoke();
            }
        }

        private void HandlePacket(PacketType type, byte[] payload, NetworkStream stream)
        {
            switch (type)
            {
                case PacketType.HelloInit:
                    HandleHelloInit(payload, stream);
                    break;

                case PacketType.CodecConfig:
                    HandleCodecConfig(payload);
                    break;

                case PacketType.VideoFrame:
                    HandleVideoFrame(payload);
                    break;

                case PacketType.Heartbeat:
                    // Respond to heartbeat if needed
                    break;

                case PacketType.Disconnect:
                    LogMessage?.Invoke("[Server] Sender requested graceful disconnect.");
                    CleanupClient();
                    break;

                default:
                    LogMessage?.Invoke($"[Server] Received unknown packet type: {(byte)type}");
                    break;
            }
        }

        private void HandleHelloInit(byte[] payload, NetworkStream stream)
        {
            if (payload.Length < 12) return;

            ushort protocolVer = (ushort)((payload[0] << 8) | payload[1]);
            int width = (payload[2] << 24) | (payload[3] << 16) | (payload[4] << 8) | payload[5];
            int height = (payload[6] << 24) | (payload[7] << 16) | (payload[8] << 8) | payload[9];
            ushort dpi = (ushort)((payload[10] << 8) | payload[11]);

            string deviceName = "Android Device";
            if (payload.Length > 14)
            {
                ushort nameLen = (ushort)((payload[12] << 8) | payload[13]);
                if (payload.Length >= 14 + nameLen)
                {
                    deviceName = System.Text.Encoding.UTF8.GetString(payload, 14, nameLen);
                }
            }

            LogMessage?.Invoke($"[Server] Handshake HELLO: '{deviceName}' ({width}x{height} @ {dpi}dpi), Protocol v{protocolVer}");

            // Send HELLO_ACK
            byte[] ackPayload = new byte[] {
                0x00, // Status: OK
                0x00, 0x01 // Receiver Version 1
            };
            byte[] ackHeader = PacketHeader.CreateHeader(PacketType.HelloAck, ackPayload.Length);
            byte[] fullPacket = new byte[ackHeader.Length + ackPayload.Length];
            Array.Copy(ackHeader, 0, fullPacket, 0, ackHeader.Length);
            Array.Copy(ackPayload, 0, fullPacket, ackHeader.Length, ackPayload.Length);

            try
            {
                stream.Write(fullPacket, 0, fullPacket.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Server] Failed to send HELLO_ACK: {ex.Message}");
            }

            ClientConnected?.Invoke(deviceName, width, height);
        }

        private void HandleCodecConfig(byte[] payload)
        {
            if (payload.Length < 8) return;

            int width = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
            int height = (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7];

            byte[] configData = new byte[payload.Length - 8];
            Array.Copy(payload, 8, configData, 0, configData.Length);

            LogMessage?.Invoke($"[Server] Received SPS/PPS CodecConfig for {width}x{height} ({configData.Length} bytes)");
            CodecConfigReceived?.Invoke(width, height, configData);
        }

        private void HandleVideoFrame(byte[] payload)
        {
            if (payload.Length < 9) return;

            byte frameType = payload[0];
            long timestampUs = ((long)payload[1] << 56) |
                               ((long)payload[2] << 48) |
                               ((long)payload[3] << 40) |
                               ((long)payload[4] << 32) |
                               ((long)payload[5] << 24) |
                               ((long)payload[6] << 16) |
                               ((long)payload[7] << 8) |
                               (long)payload[8];

            byte[] frameData = new byte[payload.Length - 9];
            Array.Copy(payload, 9, frameData, 0, frameData.Length);

            VideoFrameReceived?.Invoke(frameData, frameType, timestampUs);
        }

        private async Task<bool> ReadExactBytesAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int total = 0;
            while (total < count)
            {
                int read = await stream.ReadAsync(buffer, offset + total, count - total, token);
                if (read <= 0)
                {
                    return false; // Connection closed
                }
                total += read;
            }
            return true;
        }

        private void CleanupClient()
        {
            if (_networkStream != null)
            {
                try { _networkStream.Dispose(); } catch { }
                _networkStream = null;
            }
            if (_currentClient != null)
            {
                try { _currentClient.Close(); } catch { }
                _currentClient = null;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            CleanupClient();

            if (_listener != null)
            {
                try { _listener.Stop(); } catch { }
                _listener = null;
            }

            _listenTask = null;
            LogMessage?.Invoke("[Server] Stopped.");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
