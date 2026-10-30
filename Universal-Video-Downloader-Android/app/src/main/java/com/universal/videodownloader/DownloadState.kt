package com.universal.videodownloader

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

object DownloadState {
    
    data class ProgressInfo(
        val progress: Float = 0f,
        val eta: Long = 0,
        val currentLine: String = "",
        val batchStatus: String = "Ready to download",
        val isDownloading: Boolean = false,
        val logItems: List<String> = emptyList()
    )

    private val _state = MutableStateFlow(ProgressInfo())
    val state = _state.asStateFlow()

    private val timeFormat = SimpleDateFormat("HH:mm:ss", Locale.getDefault())

    fun updateProgress(progress: Float, eta: Long, line: String) {
        _state.value = _state.value.copy(
            progress = progress,
            eta = eta,
            currentLine = line
        )
    }

    fun setDownloading(isDownloading: Boolean) {
        _state.value = _state.value.copy(
            isDownloading = isDownloading,
            progress = 0f,
            currentLine = ""
        )
    }

    fun setBatchStatus(status: String) {
        _state.value = _state.value.copy(batchStatus = status)
    }

    fun addLog(log: String) {
        val timestamp = timeFormat.format(Date())
        val trimmed = log.trim()
        
        // Match progress lines to avoid cluttering logs (identical to C# Desktop app logic)
        val isProgress = trimmed.startsWith("[download]") && trimmed.contains("%")
        
        val currentLogs = _state.value.logItems.toMutableList()
        if (isProgress && currentLogs.isNotEmpty()) {
            val lastLog = currentLogs.last()
            if (lastLog.contains("[download]") && lastLog.contains("%")) {
                // Replace the last progress line with the updated one
                currentLogs[currentLogs.size - 1] = "[$timestamp] $trimmed"
                _state.value = _state.value.copy(logItems = currentLogs)
                return
            }
        }
        
        currentLogs.add("[$timestamp] $trimmed")
        if (currentLogs.size > 1000) {
            currentLogs.removeAt(0)
        }
        _state.value = _state.value.copy(logItems = currentLogs)
    }

    fun clearLogs() {
        _state.value = _state.value.copy(logItems = emptyList())
    }
}
