using System;

namespace PocketCastReceiver.Protocol
{
    public enum PacketType : byte
    {
        HelloInit = 0x01,
        HelloAck = 0x02,
        CodecConfig = 0x03,
        VideoFrame = 0x04,
        Heartbeat = 0x05,
        Disconnect = 0x06
    }

    public enum FrameType : byte
    {
        Keyframe = 0x01,
        Interframe = 0x02
    }

    public static class ProtocolConstants
    {
        public static readonly byte[] MagicBytes = new byte[] { 0x50, 0x43, 0x53, 0x54 }; // "PCST"
        public const byte CurrentVersion = 1;
        public const int HeaderSize = 10; // 4 magic + 1 version + 1 type + 4 payload length
        public const int DefaultPort = 8088;
    }
}
