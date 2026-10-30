package com.universal.videodownloader

import android.content.ContentValues
import android.content.Context
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import android.util.Log
import com.yausername.youtubedl_android.YoutubeDL
import com.yausername.youtubedl_android.YoutubeDLRequest
import com.yausername.youtubedl_android.YoutubeDLResponse
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileInputStream

object DownloadManager {
    private const val TAG = "DownloadManager"

    /**
     * Resolves the download directory.
     * Defaults to the application's external files directory so no permission prompts are required on Android 14+.
     * If public storage permission is granted, saves directly to the public Downloads/UVD Downloads directory.
     */
    fun getDownloadDir(context: Context): File {
        val hasPermission = if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
            Environment.isExternalStorageManager()
        } else {
            context.checkSelfPermission(android.Manifest.permission.WRITE_EXTERNAL_STORAGE) == android.content.pm.PackageManager.PERMISSION_GRANTED
        }

        return if (hasPermission) {
            val publicDir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), "UVD Downloads")
            if (!publicDir.exists()) publicDir.mkdirs()
            publicDir
        } else {
            val privateDir = File(context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "UVD Downloads")
            if (!privateDir.exists()) privateDir.mkdirs()
            privateDir
        }
    }

    suspend fun startDownload(
        context: Context,
        url: String,
        quality: String,
        format: String,
        customArgs: String,
        taskId: String,
        onProgress: (progress: Float, eta: Long, line: String) -> Unit,
        onSuccess: (output: String) -> Unit,
        onFailure: (error: Throwable) -> Unit
    ) {
        withContext(Dispatchers.IO) {
            try {
                val downloadDir = getDownloadDir(context)
                
                // Build output template
                val template = if (url.contains("list=")) {
                    File(downloadDir, "%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s").absolutePath
                } else {
                    File(downloadDir, "%(title)s.%(ext)s").absolutePath
                }

                val request = YoutubeDLRequest(url).apply {
                    addOption("-o", template)
                    addOption("--newline")
                    
                    // Apply cookies file if imported
                    val cookiesFile = File(context.filesDir, "cookies.txt")
                    if (cookiesFile.exists() && cookiesFile.length() > 0) {
                        addOption("--cookies", cookiesFile.absolutePath)
                    }
                    
                    if (format == "mp3") {
                        addOption("-x")
                        addOption("--audio-format", "mp3")
                        addOption("--audio-quality", "0")
                    } else {
                        val formatOption = if (quality == "best") {
                            "bestvideo+bestaudio/best"
                        } else {
                            "bestvideo[height<=$quality]+bestaudio/best"
                        }
                        addOption("-f", formatOption)
                        if (format != "default") {
                            addOption("--merge-output-format", format)
                        }
                    }

                    // Parse and append custom arguments
                    if (customArgs.isNotBlank()) {
                        val tokens = customArgs.split(Regex("\\s+")).filter { it.isNotBlank() }
                        for (token in tokens) {
                            addOption(token)
                        }
                    }
                }

                Log.d(TAG, "Starting download: URL=$url, TargetDir=${downloadDir.absolutePath}")

                val response = YoutubeDL.getInstance().execute(request, taskId) { progress, eta, line ->
                    onProgress(progress, eta, line)
                }

                onSuccess(response.out ?: "")
            } catch (e: Exception) {
                Log.e(TAG, "Download error", e)
                onFailure(e)
            }
        }
    }

    fun stopDownload(taskId: String): Boolean {
        return try {
            Log.d(TAG, "Stopping download for task: $taskId")
            YoutubeDL.getInstance().destroyProcessById(taskId)
        } catch (e: Exception) {
            Log.e(TAG, "Error stopping download process", e)
            false
        }
    }

    fun movePrivateFilesToPublic(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            return
        }
        val privateDir = File(context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "UVD Downloads")
        if (!privateDir.exists()) return

        privateDir.walkTopDown().forEach { file ->
            if (file.isFile && file.length() > 0) {
                val ext = file.extension.lowercase()
                if (ext != "part" && ext != "ytdl" && ext != "tmp" && ext != "temp") {
                    try {
                        val parentFile = file.parentFile
                        val relativePath = if (parentFile != null && parentFile != privateDir) {
                            parentFile.relativeTo(privateDir).path
                        } else {
                            ""
                        }
                        val success = copyFileToPublicDownloads(context, file, relativePath)
                        if (success) {
                            file.delete()
                            Log.d(TAG, "Moved private file ${file.name} to public downloads")
                        }
                    } catch (e: Exception) {
                        Log.e(TAG, "Failed to move file ${file.name} to public downloads", e)
                    }
                }
            }
        }
        cleanEmptyDirectories(privateDir)
    }

    private fun cleanEmptyDirectories(dir: File) {
        if (!dir.exists() || !dir.isDirectory) return
        dir.listFiles()?.forEach { child ->
            if (child.isDirectory) {
                cleanEmptyDirectories(child)
                if (child.listFiles()?.isEmpty() == true) {
                    child.delete()
                }
            }
        }
    }

    private fun copyFileToPublicDownloads(context: Context, file: File, subFolder: String): Boolean {
        val resolver = context.contentResolver
        val contentValues = ContentValues().apply {
            put(MediaStore.MediaColumns.DISPLAY_NAME, file.name)
            val mime = when (file.extension.lowercase()) {
                "mp4" -> "video/mp4"
                "webm" -> "video/webm"
                "mp3" -> "audio/mpeg"
                "m4a" -> "audio/mp4"
                else -> "video/*"
            }
            put(MediaStore.MediaColumns.MIME_TYPE, mime)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                val targetPath = if (subFolder.isNotEmpty()) {
                    Environment.DIRECTORY_DOWNLOADS + "/UVD Downloads/" + subFolder.replace("\\", "/")
                } else {
                    Environment.DIRECTORY_DOWNLOADS + "/UVD Downloads"
                }
                put(MediaStore.MediaColumns.RELATIVE_PATH, targetPath)
                put(MediaStore.MediaColumns.IS_PENDING, 1)
            }
        }

        val collectionUri = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Downloads.EXTERNAL_CONTENT_URI
        } else {
            MediaStore.Files.getContentUri("external")
        }

        val uri = resolver.insert(collectionUri, contentValues) ?: return false

        try {
            resolver.openOutputStream(uri)?.use { outputStream ->
                FileInputStream(file).use { inputStream ->
                    inputStream.copyTo(outputStream)
                }
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                contentValues.clear()
                contentValues.put(MediaStore.MediaColumns.IS_PENDING, 0)
                resolver.update(uri, contentValues, null, null)
            }
            return true
        } catch (e: Exception) {
            resolver.delete(uri, null, null)
            throw e
        }
    }
}
