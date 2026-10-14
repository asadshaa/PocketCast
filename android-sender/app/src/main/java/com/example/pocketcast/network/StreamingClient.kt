package com.example.pocketcast.network

import android.os.Build
import android.util.Log
import com.example.pocketcast.protocol.ProtocolConstants
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.BufferedOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.nio.ByteBuffer
import java.nio.ByteOrder

class StreamingClient(
    private val host: String,
    private val port: Int,
    private val deviceWidth: Int,
    private val deviceHeight: Int,
    private val densityDpi: Int,
    private val onConnectionStateChanged: (Boolean, String) -> Unit
) {
    private var socket: Socket? = null
    private var outputStream: OutputStream? = null
    private var inputStream: InputStream? = null
    @Volatile
    private var isConnected = false

    companion object {
        private const val TAG = "PocketCast_Client"
        private const val CONNECT_TIMEOUT_MS = 4000
    }

    suspend fun connect(): Boolean = withContext(Dispatchers.IO) {
        try {
            Log.d(TAG, "Connecting to $host:$port...")
            val s = Socket()
            // Configure socket for low latency
            s.tcpNoDelay = true
            s.sendBufferSize = 512 * 1024
            s.connect(InetSocketAddress(host, port), CONNECT_TIMEOUT_MS)

            socket = s
            outputStream = BufferedOutputStream(s.getOutputStream(), 128 * 1024)
            inputStream = s.getInputStream()

            // Perform HELLO handshake
            val handshakeSuccess = performHandshake()
            if (!handshakeSuccess) {
                disconnect("Handshake negotiation failed.")
                return@withContext false
            }

            isConnected = true
            onConnectionStateChanged(true, "Connected to receiver ($host:$port)")
            return@withContext true
        } catch (e: Exception) {
            Log.e(TAG, "Connection failed: ${e.message}")
            disconnect("Connection failed: ${e.localizedMessage}")
            return@withContext false
        }
    }

    private fun performHandshake(): Boolean {
        try {
            val deviceName = "${Build.MANUFACTURER} ${Build.MODEL}".toByteArray(Charsets.UTF_8)
            val payloadLen = 2 + 4 + 4 + 2 + 2 + deviceName.size

            val buffer = ByteBuffer.allocate(payloadLen).order(ByteOrder.BIG_ENDIAN)
            buffer.putShort(ProtocolConstants.CURRENT_VERSION.toShort())
            buffer.putInt(deviceWidth)
            buffer.putInt(deviceHeight)
            buffer.putShort(densityDpi.toShort())
            buffer.putShort(deviceName.size.toShort())
            buffer.put(deviceName)

            val header = ProtocolConstants.createHeader(ProtocolConstants.TYPE_HELLO_INIT, payloadLen)
            outputStream?.write(header)
            outputStream?.write(buffer.array())
            outputStream?.flush()

            // Read HELLO_ACK header
            val ackHeader = ByteArray(ProtocolConstants.HEADER_SIZE)
            var read = 0
            while (read < ProtocolConstants.HEADER_SIZE) {
                val r = inputStream?.read(ackHeader, read, ProtocolConstants.HEADER_SIZE - read) ?: -1
                if (r < 0) return false
                read += r
            }

            val ackPayloadLen = ((ackHeader[6].toInt() and 0xFF) shl 24) or
                    ((ackHeader[7].toInt() and 0xFF) shl 16) or
                    ((ackHeader[8].toInt() and 0xFF) shl 8) or
                    (ackHeader[9].toInt() and 0xFF)

            val ackPayload = ByteArray(ackPayloadLen)
            var pRead = 0
            while (pRead < ackPayloadLen) {
                val r = inputStream?.read(ackPayload, pRead, ackPayloadLen - pRead) ?: -1
                if (r < 0) return false
                pRead += r
            }

            val status = ackPayload.getOrNull(0) ?: -1
            return status == 0.toByte()
        } catch (e: Exception) {
            Log.e(TAG, "Handshake error: ${e.message}")
            return false
        }
    }

    @Synchronized
    fun sendCodecConfig(width: Int, height: Int, configData: ByteArray) {
        if (!isConnected || outputStream == null) return
        try {
            val payloadLen = 4 + 4 + configData.size
            val header = ProtocolConstants.createHeader(ProtocolConstants.TYPE_CODEC_CONFIG, payloadLen)

            val meta = ByteBuffer.allocate(8).order(ByteOrder.BIG_ENDIAN)
            meta.putInt(width)
            meta.putInt(height)

            outputStream?.write(header)
            outputStream?.write(meta.array())
            outputStream?.write(configData)
            outputStream?.flush()
            Log.d(TAG, "Sent CODEC_CONFIG ($width x $height), ${configData.size} bytes SPS/PPS")
        } catch (e: Exception) {
            Log.e(TAG, "Error sending codec config: ${e.message}")
            disconnect("Network error while sending codec configuration")
        }
    }

    @Synchronized
    fun sendVideoFrame(frameData: ByteArray, isKeyFrame: Boolean, ptsUs: Long) {
        if (!isConnected || outputStream == null) return
        try {
            val payloadLen = 1 + 8 + frameData.size
            val frameType = if (isKeyFrame) ProtocolConstants.FRAME_KEYFRAME else ProtocolConstants.FRAME_INTERFRAME
            val header = ProtocolConstants.createHeader(ProtocolConstants.TYPE_VIDEO_FRAME, payloadLen)

            val meta = ByteBuffer.allocate(9).order(ByteOrder.BIG_ENDIAN)
            meta.put(frameType)
            meta.putLong(ptsUs)

            outputStream?.write(header)
            outputStream?.write(meta.array())
            outputStream?.write(frameData)
            outputStream?.flush()
        } catch (e: Exception) {
            Log.e(TAG, "Error sending video frame: ${e.message}")
            disconnect("Network error while transmitting video frame")
        }
    }

    fun disconnect(reason: String = "User requested stop") {
        if (!isConnected) return
        isConnected = false
        Log.d(TAG, "Disconnecting: $reason")
        try {
            // Send DISCONNECT packet if possible
            val header = ProtocolConstants.createHeader(ProtocolConstants.TYPE_DISCONNECT, 0)
            outputStream?.write(header)
            outputStream?.flush()
        } catch (_: Exception) {}

        try { outputStream?.close() } catch (_: Exception) {}
        try { inputStream?.close() } catch (_: Exception) {}
        try { socket?.close() } catch (_: Exception) {}

        outputStream = null
        inputStream = null
        socket = null

        onConnectionStateChanged(false, reason)
    }
}
