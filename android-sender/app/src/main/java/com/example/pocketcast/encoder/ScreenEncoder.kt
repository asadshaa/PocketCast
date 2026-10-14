package com.example.pocketcast.encoder

import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.media.projection.MediaProjection
import android.os.Handler
import android.os.HandlerThread
import android.util.Log
import android.view.Surface

class ScreenEncoder(
    private val mediaProjection: MediaProjection,
    private val width: Int,
    private val height: Int,
    private val densityDpi: Int,
    private val fps: Int = 60,
    private val bitrateBps: Int = 4_000_000,
    private val onCodecConfigAvailable: (width: Int, height: Int, configData: ByteArray) -> Unit,
    private val onFrameAvailable: (frameData: ByteArray, isKeyFrame: Boolean, ptsUs: Long) -> Unit,
    private val onError: (Exception) -> Unit
) {
    private var mediaCodec: MediaCodec? = null
    private var virtualDisplay: VirtualDisplay? = null
    private var inputSurface: Surface? = null
    private var encoderThread: HandlerThread? = null
    private var encoderHandler: Handler? = null

    @Volatile
    private var isStreaming = false

    companion object {
        private const val TAG = "PocketCast_Encoder"
        private const val MIME_TYPE = "video/avc" // H.264
        private const val I_FRAME_INTERVAL_SEC = 1 // Keyframe every 1 second for rapid sync & low latency
    }

    fun start() {
        try {
            Log.d(TAG, "Initializing MediaCodec for ${width}x${height} @ ${fps}fps, bitrate: $bitrateBps")

            val format = MediaFormat.createVideoFormat(MIME_TYPE, width, height).apply {
                setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
                setInteger(MediaFormat.KEY_BIT_RATE, bitrateBps)
                setInteger(MediaFormat.KEY_FRAME_RATE, fps)
                setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, I_FRAME_INTERVAL_SEC)
                // Low latency encoding optimizations
                setInteger(MediaFormat.KEY_PRIORITY, 0) // Realtime priority
                setInteger(MediaFormat.KEY_LATENCY, 0)
                setInteger(MediaFormat.KEY_REPEAT_PREVIOUS_FRAME_AFTER, 1_000_000 / fps) // Repeat frame if display static
            }

            val codec = MediaCodec.createEncoderByType(MIME_TYPE)
            mediaCodec = codec

            encoderThread = HandlerThread("PocketCastEncoder").apply { start() }
            encoderHandler = Handler(encoderThread!!.looper)

            codec.setCallback(object : MediaCodec.Callback() {
                override fun onInputBufferAvailable(codec: MediaCodec, index: Int) {
                    // Not used for Surface input
                }

                override fun onOutputBufferAvailable(codec: MediaCodec, index: Int, info: MediaCodec.BufferInfo) {
                    if (!isStreaming) return

                    try {
                        val outputBuffer = codec.getOutputBuffer(index) ?: return
                        outputBuffer.position(info.offset)
                        outputBuffer.limit(info.offset + info.size)

                        val chunk = ByteArray(info.size)
                        outputBuffer.get(chunk)

                        // Check if this is Codec Config (SPS / PPS)
                        val isConfig = (info.flags and MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0
                        val isKeyframe = (info.flags and MediaCodec.BUFFER_FLAG_KEY_FRAME) != 0

                        if (isConfig) {
                            Log.d(TAG, "Generated SPS/PPS Codec Config (${info.size} bytes)")
                            onCodecConfigAvailable(width, height, chunk)
                        } else if (info.size > 0) {
                            onFrameAvailable(chunk, isKeyframe, info.presentationTimeUs)
                        }

                        codec.releaseOutputBuffer(index, false)
                    } catch (e: Exception) {
                        Log.e(TAG, "Error handling output buffer: ${e.message}")
                    }
                }

                override fun onError(codec: MediaCodec, e: MediaCodec.CodecException) {
                    Log.e(TAG, "MediaCodec Error: ${e.message}")
                    onError(e)
                }

                override fun onOutputFormatChanged(codec: MediaCodec, format: MediaFormat) {
                    Log.d(TAG, "MediaCodec output format changed: $format")
                    val sps = format.getByteBuffer("csd-0")
                    val pps = format.getByteBuffer("csd-1")
                    if (sps != null && pps != null) {
                        val spsBytes = ByteArray(sps.remaining())
                        sps.get(spsBytes)
                        val ppsBytes = ByteArray(pps.remaining())
                        pps.get(ppsBytes)

                        val combined = ByteArray(spsBytes.size + ppsBytes.size)
                        System.arraycopy(spsBytes, 0, combined, 0, spsBytes.size)
                        System.arraycopy(ppsBytes, 0, combined, spsBytes.size, ppsBytes.size)

                        onCodecConfigAvailable(width, height, combined)
                    }
                }
            }, encoderHandler)

            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            inputSurface = codec.createInputSurface()
            codec.start()

            // Create VirtualDisplay capturing from MediaProjection directly into the codec Surface
            virtualDisplay = mediaProjection.createVirtualDisplay(
                "PocketCastScreen",
                width,
                height,
                densityDpi,
                DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                inputSurface,
                null,
                null
            )

            isStreaming = true
            Log.d(TAG, "Screen capture & encoder started successfully")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start screen encoder: ${e.message}", e)
            onError(e)
        }
    }

    fun stop() {
        if (!isStreaming) return
        isStreaming = false
        Log.d(TAG, "Stopping screen encoder and releasing resources...")

        try {
            virtualDisplay?.release()
            virtualDisplay = null
        } catch (e: Exception) {
            Log.w(TAG, "Error releasing virtual display: ${e.message}")
        }

        try {
            mediaCodec?.stop()
            mediaCodec?.release()
            mediaCodec = null
        } catch (e: Exception) {
            Log.w(TAG, "Error releasing MediaCodec: ${e.message}")
        }

        try {
            inputSurface?.release()
            inputSurface = null
        } catch (_: Exception) {}

        encoderThread?.quitSafely()
        encoderThread = null
        encoderHandler = null
    }
}
