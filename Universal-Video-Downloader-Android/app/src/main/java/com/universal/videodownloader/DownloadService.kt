package com.universal.videodownloader

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import java.io.File

class DownloadService : Service() {

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    private var currentTaskId: String? = null
    private var stopRequested = false

    private lateinit var notificationManager: NotificationManager

    companion object {
        private const val TAG = "DownloadService"
        private const val CHANNEL_ID = "uvd_download_channel"
        private const val NOTIFICATION_ID = 1001

        const val ACTION_START = "com.universal.videodownloader.action.START"
        const val ACTION_STOP = "com.universal.videodownloader.action.STOP"

        const val EXTRA_URLS = "extra_urls"
        const val EXTRA_QUALITY = "extra_quality"
        const val EXTRA_FORMAT = "extra_format"
        const val EXTRA_CUSTOM_ARGS = "extra_custom_args"
    }

    override fun onCreate() {
        super.onCreate()
        notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        createNotificationChannel()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent == null) return START_NOT_STICKY

        when (intent.action) {
            ACTION_START -> {
                val urls = intent.getStringArrayListExtra(EXTRA_URLS) ?: emptyList<String>()
                val quality = intent.getStringExtra(EXTRA_QUALITY) ?: "best"
                val format = intent.getStringExtra(EXTRA_FORMAT) ?: "default"
                val customArgs = intent.getStringExtra(EXTRA_CUSTOM_ARGS) ?: ""

                startForegroundService(urls, quality, format, customArgs)
            }
            ACTION_STOP -> {
                performStop()
            }
        }

        return START_NOT_STICKY
    }

    private fun startForegroundService(
        urls: List<String>,
        quality: String,
        format: String,
        customArgs: String
    ) {
        stopRequested = false
        DownloadState.setDownloading(true)
        DownloadState.clearLogs()

        val notification = createNotification("Starting batch download...", 0)
        startForeground(NOTIFICATION_ID, notification)

        serviceScope.launch {
            Log.d(TAG, "Starting download of ${urls.size} URLs")
            for (index in urls.indices) {
                if (stopRequested) break

                val url = urls[index]
                val progressText = "Downloading ${index + 1}/${urls.size}"
                DownloadState.setBatchStatus(progressText)
                updateNotification(progressText, 0)
                
                val taskId = "uvd_task_${System.currentTimeMillis()}"
                currentTaskId = taskId

                DownloadState.addLog("[info] Starting URL: $url")
                
                DownloadManager.startDownload(
                    context = this@DownloadService,
                    url = url,
                    quality = quality,
                    format = format,
                    customArgs = customArgs,
                    taskId = taskId,
                    onProgress = { progress, _, line ->
                        DownloadState.updateProgress(progress, 0, line)
                        DownloadState.addLog(line)
                        updateNotification("Downloading ${index + 1}/${urls.size} (${progress.toInt()}%)", progress.toInt())
                    },
                    onSuccess = { out ->
                        DownloadState.addLog(out)
                        DownloadState.addLog("[info] Successfully finished URL: $url")
                        DownloadState.addLog("[info] Video(s) placed in Download/UVD Downloads folder.")
                        DownloadManager.movePrivateFilesToPublic(this@DownloadService)
                    },
                    onFailure = { error ->
                        DownloadState.addLog("[error] Failed URL: $url - ${error.localizedMessage}")
                    }
                )
            }

            Log.d(TAG, "Batch download complete")
            DownloadState.setDownloading(false)
            DownloadState.setBatchStatus("Finished")
            DownloadManager.movePrivateFilesToPublic(this@DownloadService)
            
            // Scan downloads directory so media scanner detects new video/audio files
            try {
                val dir = DownloadManager.getDownloadDir(this@DownloadService)
                android.media.MediaScannerConnection.scanFile(
                    this@DownloadService,
                    arrayOf(dir.absolutePath),
                    null,
                    null
                )
            } catch (e: Exception) {
                e.printStackTrace()
            }

            stopForeground(STOP_FOREGROUND_REMOVE)
            stopSelf()
        }
    }

    private fun performStop() {
        Log.d(TAG, "Stop requested")
        stopRequested = true
        DownloadState.addLog("[info] Stop requested. Killing active process...")
        currentTaskId?.let {
            DownloadManager.stopDownload(it)
        }
        DownloadState.setDownloading(false)
        DownloadState.setBatchStatus("Stopped")
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    private fun updateNotification(text: String, progress: Int) {
        val notification = createNotification(text, progress)
        notificationManager.notify(NOTIFICATION_ID, notification)
    }

    private fun createNotification(text: String, progress: Int): Notification {
        val notificationIntent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            notificationIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val stopIntent = Intent(this, DownloadService::class.java).apply {
            action = ACTION_STOP
        }
        val stopPendingIntent = PendingIntent.getService(
            this,
            1,
            stopIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Universal Video Downloader")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.stat_sys_download)
            .setContentIntent(pendingIntent)
            .setProgress(100, progress, false)
            .setOngoing(true)
            .addAction(android.R.drawable.ic_menu_close_clear_cancel, "Stop", stopPendingIntent)
            .build()
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.channel_desc)
            }
            notificationManager.createNotificationChannel(channel)
        }
    }

    override fun onDestroy() {
        serviceScope.cancel()
        super.onDestroy()
    }
}
