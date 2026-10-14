package com.example.pocketcast.ui

import android.app.Activity
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.util.DisplayMetrics
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.example.pocketcast.databinding.ActivityMainBinding
import com.example.pocketcast.service.ScreenCastService

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private var screenCastService: ScreenCastService? = null
    private var isBound = false

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, binder: IBinder?) {
            val localBinder = binder as? ScreenCastService.LocalBinder
            screenCastService = localBinder?.getService()
            isBound = true

            screenCastService?.onStatusChanged = { connected, message ->
                runOnUiThread {
                    binding.tvStatus.text = message
                    if (connected) {
                        binding.tvStatus.setTextColor(ContextCompat.getColor(this@MainActivity, android.R.color.holo_green_light))
                        binding.btnStart.visibility = View.GONE
                        binding.btnStop.visibility = View.VISIBLE
                        setInputsEnabled(false)
                    } else {
                        binding.tvStatus.setTextColor(ContextCompat.getColor(this@MainActivity, android.R.color.holo_red_light))
                        binding.btnStart.visibility = View.VISIBLE
                        binding.btnStop.visibility = View.GONE
                        setInputsEnabled(true)
                    }
                }
            }
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            screenCastService = null
            isBound = false
        }
    }

    private val capturePermissionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            launchScreenCastService(result.resultCode, result.data!!)
        } else {
            Toast.makeText(this, "Screen capture permission denied by user.", Toast.LENGTH_LONG).show()
            binding.tvStatus.text = "Permission denied. Cannot capture screen."
        }
    }

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted) {
            requestScreenCapture()
        } else {
            Toast.makeText(this, "Notification permission is recommended for streaming", Toast.LENGTH_SHORT).show()
            requestScreenCapture()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        setupListeners()
    }

    private fun setupListeners() {
        binding.rgTransport.setOnCheckedChangeListener { _, checkedId ->
            if (checkedId == binding.rbUsb.id) {
                binding.etHost.setText("127.0.0.1")
                binding.tvHint.text = "• For USB mode, ensure phone is connected via cable and ADB reverse is setup."
            } else {
                binding.etHost.setText("")
                binding.etHost.hint = "192.168.1.xxx"
                binding.tvHint.text = "• For Wi-Fi mode, enter laptop's LAN IP address shown in Windows PocketCast."
            }
        }

        binding.btnStart.setOnClickListener {
            checkPermissionsAndStart()
        }

        binding.btnStop.setOnClickListener {
            stopMirroring()
        }
    }

    private fun checkPermissionsAndStart() {
        val host = binding.etHost.text.toString().trim()
        if (host.isEmpty()) {
            Toast.makeText(this, "Please enter receiver host IP address", Toast.LENGTH_SHORT).show()
            return
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, android.Manifest.permission.POST_NOTIFICATIONS)
                != PackageManager.PERMISSION_GRANTED) {
                notificationPermissionLauncher.launch(android.Manifest.permission.POST_NOTIFICATIONS)
                return
            }
        }

        requestScreenCapture()
    }

    private fun requestScreenCapture() {
        val mpManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        val intent = mpManager.createScreenCaptureIntent()
        capturePermissionLauncher.launch(intent)
    }

    private fun launchScreenCastService(resultCode: Int, data: Intent) {
        val host = binding.etHost.text.toString().trim()
        val port = binding.etPort.text.toString().toIntOrNull() ?: 8088

        // Calculate resolution & bitrate based on preset
        val (width, height, fps, bitrate) = when (binding.rgPreset.checkedRadioButtonId) {
            binding.rbSmooth.id -> Quad(1080, 1920, 60, 6_000_000)
            binding.rbCompatibility.id -> Quad(720, 1280, 30, 2_500_000)
            else -> Quad(1080, 1920, 30, 4_000_000) // Balanced default
        }

        val metrics = DisplayMetrics()
        windowManager.defaultDisplay.getRealMetrics(metrics)
        val dpi = metrics.densityDpi

        val serviceIntent = Intent(this, ScreenCastService::class.java).apply {
            putExtra(ScreenCastService.EXTRA_RESULT_CODE, resultCode)
            putExtra(ScreenCastService.EXTRA_RESULT_DATA, data)
            putExtra(ScreenCastService.EXTRA_HOST, host)
            putExtra(ScreenCastService.EXTRA_PORT, port)
            putExtra(ScreenCastService.EXTRA_WIDTH, width)
            putExtra(ScreenCastService.EXTRA_HEIGHT, height)
            putExtra(ScreenCastService.EXTRA_DPI, dpi)
            putExtra(ScreenCastService.EXTRA_FPS, fps)
            putExtra(ScreenCastService.EXTRA_BITRATE, bitrate)
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(serviceIntent)
        } else {
            startService(serviceIntent)
        }

        bindService(serviceIntent, serviceConnection, Context.BIND_AUTO_CREATE)
    }

    private fun stopMirroring() {
        screenCastService?.stopStreaming()
        if (isBound) {
            unbindService(serviceConnection)
            isBound = false
        }
    }

    private fun setInputsEnabled(enabled: Boolean) {
        binding.etHost.isEnabled = enabled
        binding.etPort.isEnabled = enabled
        binding.rbUsb.isEnabled = enabled
        binding.rbWifi.isEnabled = enabled
        binding.rbBalanced.isEnabled = enabled
        binding.rbSmooth.isEnabled = enabled
        binding.rbCompatibility.isEnabled = enabled
    }

    override fun onDestroy() {
        if (isBound) {
            unbindService(serviceConnection)
            isBound = false
        }
        super.onDestroy()
    }

    data class Quad<A, B, C, D>(val first: A, val second: B, val third: C, val fourth: D)
}
