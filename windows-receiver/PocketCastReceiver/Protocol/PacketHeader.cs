using System;
using System.IO;

namespace PocketCastReceiver.Protocol
{
    public class PacketHeader
    {
        public byte Version { get; set; }
        public PacketType Type { get; set; }
        public int PayloadLength { get; set; }

        public static bool TryRead(byte[] buffer, int offset, out PacketHeader? header)
        {
            header = null;
            if (buffer.Length - offset < ProtocolConstants.HeaderSize)
                return false;

            if (buffer[offset] != ProtocolConstants.MagicBytes[0] ||
                buffer[offset + 1] != ProtocolConstants.MagicBytes[1] ||
                buffer[offset + 2] != ProtocolConstants.MagicBytes[2] ||
                buffer[offset + 3] != ProtocolConstants.MagicBytes[3])
            {
                return false;
            }

            byte version = buffer[offset + 4];
            PacketType type = (PacketType)buffer[offset + 5];

            // Big-endian 32-bit integer
            int payloadLength = (buffer[offset + 6] << 24) |
                               (buffer[offset + 7] << 16) |
                               (buffer[offset + 8] << 8) |
                               (buffer[offset + 9]);

            header = new PacketHeader
            {
                Version = version,
                Type = type,
                PayloadLength = payloadLength
            };

            return true;
        }

        public static byte[] CreateHeader(PacketType type, int payloadLength)
        {
            byte[] header = new byte[ProtocolConstants.HeaderSize];
            Array.Copy(ProtocolConstants.MagicBytes, 0, header, 0, 4);
            header[4] = ProtocolConstants.CurrentVersion;
            header[5] = (byte)type;

            header[6] = (byte)((payloadLength >> 24) & 0xFF);
            header[7] = (byte)((payloadLength >> 16) & 0xFF);
            header[8] = (byte)((payloadLength >> 8) & 0xFF);
            header[9] = (byte)(payloadLength & 0xFF);

            return header;
        }
    }
}
