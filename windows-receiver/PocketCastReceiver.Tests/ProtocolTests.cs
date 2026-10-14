using PocketCastReceiver.Protocol;
using Xunit;

namespace PocketCastReceiver.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void CreateHeader_GeneratesValidHeaderBytes()
        {
            int payloadLength = 1024;
            var header = PacketHeader.CreateHeader(PacketType.VideoFrame, payloadLength);

            Assert.Equal(ProtocolConstants.HeaderSize, header.Length);
            Assert.Equal(ProtocolConstants.MagicBytes[0], header[0]);
            Assert.Equal(ProtocolConstants.MagicBytes[1], header[1]);
            Assert.Equal(ProtocolConstants.MagicBytes[2], header[2]);
            Assert.Equal(ProtocolConstants.MagicBytes[3], header[3]);
            Assert.Equal(ProtocolConstants.CurrentVersion, header[4]);
            Assert.Equal((byte)PacketType.VideoFrame, header[5]);

            // Big-endian check
            int decodedLen = (header[6] << 24) | (header[7] << 16) | (header[8] << 8) | header[9];
            Assert.Equal(payloadLength, decodedLen);
        }

        [Fact]
        public void TryRead_ParsesValidHeaderSuccessfully()
        {
            byte[] validHeaderBytes = new byte[] {
                0x50, 0x43, 0x53, 0x54, // Magic
                0x01,                   // Version 1
                0x03,                   // CodecConfig
                0x00, 0x00, 0x01, 0x2C  // Length: 300
            };

            bool success = PacketHeader.TryRead(validHeaderBytes, 0, out var parsed);

            Assert.True(success);
            Assert.NotNull(parsed);
            Assert.Equal(1, parsed.Version);
            Assert.Equal(PacketType.CodecConfig, parsed.Type);
            Assert.Equal(300, parsed.PayloadLength);
        }

        [Fact]
        public void TryRead_RejectsCorruptMagicBytes()
        {
            byte[] invalidMagic = new byte[] {
                0x00, 0x11, 0x22, 0x33,
                0x01, 0x01,
                0x00, 0x00, 0x00, 0x10
            };

            bool success = PacketHeader.TryRead(invalidMagic, 0, out var parsed);

            Assert.False(success);
            Assert.Null(parsed);
        }
    }
}
