package com.example.pocketcast.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.example.pocketcast.encoder.ScreenEncoder
import com.example.pocketcast.network.StreamingClient
import com.example.pocketcast.ui.MainActivity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

class ScreenCastService : Service() {

    private val binder = LocalBinder()
    private val serviceScope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    private var mediaProjection: MediaProjection? = null
    private var encoder: ScreenEncoder? = null
    private var networkClient: StreamingClient? = null

    @Volatile
    private var isStreaming = false

    var onStatusChanged: ((Boolean, String) -> Unit)? = null

    companion object {
        private const val TAG = "PocketCast_Service"
        private const val NOTIFICATION_CHANNEL_ID = "pocketcast_screen_stream"
        private const val NOTIFICATION_ID = 1001

        const val EXTRA_RESULT_CODE = "extra_result_code"
        const val EXTRA_RESULT_DATA = "extra_result_data"
        const val EXTRA_HOST = "extra_host"
        const val EXTRA_PORT = "extra_port"
        const val EXTRA_WIDTH = "extra_width"
        const val EXTRA_HEIGHT = "extra_height"
        const val EXTRA_DPI = "extra_dpi"
        const val EXTRA_FPS = "extra_fps"
        const val EXTRA_BITRATE = "extra_bitrate"
    }

    inner class LocalBinder : Binder() {
        fun getService(): ScreenCastService = this@ScreenCastService
    }

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent == null) return START_NOT_STICKY

        val resultCode = intent.getIntExtra(EXTRA_RESULT_CODE, 0)
        val resultData = intent.getParcelableExtra<Intent>(EXTRA_RESULT_DATA)
        val host = intent.getStringExtra(EXTRA_HOST) ?: "127.0.0.1"
        val port = intent.getIntExtra(EXTRA_PORT, 8088)
        val width = intent.getIntExtra(EXTRA_WIDTH, 1080)
        val height = intent.getIntExtra(EXTRA_HEIGHT, 1920)
        val dpi = intent.getIntExtra(EXTRA_DPI, 400)
        val fps = intent.getIntExtra(EXTRA_FPS, 60)
        val bitrate = intent.getIntExtra(EXTRA_BITRATE, 4_000_000)

        // Start Foreground Service with appropriate notification
        startForegroundServiceWithNotification()

        if (resultData != null && resultCode != 0) {
            val mpManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
            mediaProjection = mpManager.getMediaProjection(resultCode, resultData)

            mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                override fun onStop() {
                    Log.d(TAG, "MediaProjection stopped by system")
                    stopStreaming()
                }
            }, null)

            startStreamingSession(host, port, width, height, dpi, fps, bitrate)
        }

        return START_NOT_STICKY
    }

    private fun startForegroundServiceWithNotification() {
        val notification = createNotification("PocketCast Streaming Active")
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun createNotification(statusText: String): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        return NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
            .setContentTitle("PocketCast Screen Mirror")
            .setContentText(statusText)
            .setSmallIcon(android.R.drawable.ic_menu_camera)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                NOTIFICATION_CHANNEL_ID,
                "PocketCast Mirroring Service",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Foreground screen mirroring channel for PocketCast"
            }
            val nm = getSystemService(NotificationManager::class.java)
            nm?.createNotificationChannel(channel)
        }
    }

    private fun startStreamingSession(
        host: String,
        port: Int,
        width: Int,
        height: Int,
        dpi: Int,
        fps: Int,
        bitrate: Int
    ) {
        serviceScope.launch {
            onStatusChanged?.invoke(false, "Connecting to $host:$port...")

            val client = StreamingClient(host, port, width, height, dpi) { connected, message ->
                serviceScope.launch {
                    onStatusChanged?.invoke(connected, message)
                    if (!connected && isStreaming) {
                        stopStreaming()
                    }
                }
            }
            networkClient = client

            val connected = client.connect()
            if (!connected) {
                stopStreaming()
                return@launch
            }

            val projection = mediaProjection
            if (projection == null) {
                client.disconnect("MediaProjection unavailable")
                stopStreaming()
                return@launch
            }

            encoder = ScreenEncoder(
                mediaProjection = projection,
                width = width,
                height = height,
                densityDpi = dpi,
                fps = fps,
                bitrateBps = bitrate,
                onCodecConfigAvailable = { w, h, config ->
                    client.sendCodecConfig(w, h, config)
                },
                onFrameAvailable = { frame, isKey, pts ->
                    client.sendVideoFrame(frame, isKey, pts)
                },
                onError = { e ->
                    Log.e(TAG, "Encoder error: ${e.message}")
                    stopStreaming()
                }
            )

            encoder?.start()
            isStreaming = true
            onStatusChanged?.invoke(true, "Mirroring active to $host:$port")
        }
    }

    fun stopStreaming() {
        if (!isStreaming) return
        isStreaming = false
        Log.d(TAG, "Stopping streaming service...")

        try {
            encoder?.stop()
            encoder = null
        } catch (e: Exception) {
            Log.e(TAG, "Error stopping encoder: ${e.message}")
        }

        try {
            networkClient?.disconnect()
            networkClient = null
        } catch (_: Exception) {}

        try {
            mediaProjection?.stop()
            mediaProjection = null
        } catch (_: Exception) {}

        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
        onStatusChanged?.invoke(false, "Disconnected")
    }

    override fun onDestroy() {
        stopStreaming()
        serviceScope.cancel()
        super.onDestroy()
    }
}
