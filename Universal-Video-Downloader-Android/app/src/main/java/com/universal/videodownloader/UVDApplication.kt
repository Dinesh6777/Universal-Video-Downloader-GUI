package com.universal.videodownloader

import android.app.Application
import android.util.Log
import com.yausername.aria2c.Aria2c
import com.yausername.ffmpeg.FFmpeg
import com.yausername.youtubedl_android.YoutubeDL
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

class UVDApplication : Application() {

    private val applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onCreate() {
        super.onCreate()
        
        applicationScope.launch {
            try {
                Log.d("UVDApplication", "Initializing libraries asynchronously")
                YoutubeDL.init(this@UVDApplication)
                FFmpeg.init(this@UVDApplication)
                Aria2c.init(this@UVDApplication)
                Log.d("UVDApplication", "Libraries initialized successfully")

                // Auto-update yt-dlp binary to the latest stable release
                Log.d("UVDApplication", "Updating yt-dlp engine to latest stable version...")
                val status = YoutubeDL.getInstance().updateYoutubeDL(this@UVDApplication, YoutubeDL.UpdateChannel.STABLE)
                Log.d("UVDApplication", "yt-dlp update status: $status")
            } catch (e: Exception) {
                Log.e("UVDApplication", "Failed to initialize/update libraries", e)
            }
        }
    }
}
