package com.example.pocketcast.protocol

object ProtocolConstants {
    val MAGIC_BYTES = byteArrayOf(0x50.toByte(), 0x43.toByte(), 0x53.toByte(), 0x54.toByte()) // "PCST"
    const val CURRENT_VERSION: Byte = 1
    const val HEADER_SIZE = 10

    const val TYPE_HELLO_INIT: Byte = 0x01
    const val TYPE_HELLO_ACK: Byte = 0x02
    const val TYPE_CODEC_CONFIG: Byte = 0x03
    const val TYPE_VIDEO_FRAME: Byte = 0x04
    const val TYPE_HEARTBEAT: Byte = 0x05
    const val TYPE_DISCONNECT: Byte = 0x06

    const val FRAME_KEYFRAME: Byte = 0x01
    const val FRAME_INTERFRAME: Byte = 0x02

    fun createHeader(type: Byte, payloadLength: Int): ByteArray {
        val header = ByteArray(HEADER_SIZE)
        System.arraycopy(MAGIC_BYTES, 0, header, 0, 4)
        header[4] = CURRENT_VERSION
        header[5] = type
        header[6] = ((payloadLength shr 24) and 0xFF).toByte()
        header[7] = ((payloadLength shr 16) and 0xFF).toByte()
        header[8] = ((payloadLength shr 8) and 0xFF).toByte()
        header[9] = (payloadLength and 0xFF).toByte()
        return header
    }
}
