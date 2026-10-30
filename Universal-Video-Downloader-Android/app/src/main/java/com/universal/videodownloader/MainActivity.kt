package com.universal.videodownloader

import android.Manifest
import android.annotation.SuppressLint
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.Settings
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.ContentPaste
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import java.io.File

class MainActivity : ComponentActivity() {

    // Storage permission launcher for Android 11+ (MANAGE_EXTERNAL_STORAGE)
    private val requestManageStorageLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            if (Environment.isExternalStorageManager()) {
                Toast.makeText(this, "Storage Access Granted!", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this, "Running in Sandbox mode. Downloads will be saved in App folders.", Toast.LENGTH_LONG).show()
            }
        }
    }

    // Permission launcher for Android 13+ Notifications and older storage permissions
    private val requestPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val postNotificationGranted = permissions[Manifest.permission.POST_NOTIFICATIONS] ?: true
        if (!postNotificationGranted) {
            Toast.makeText(this, "Notifications disabled. Background progress won't be shown.", Toast.LENGTH_SHORT).show()
        }
    }

    // Cookies import launcher
    private val importCookiesLauncher = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) {
            try {
                contentResolver.openInputStream(uri)?.use { inputStream ->
                    val cookiesFile = File(filesDir, "cookies.txt")
                    cookiesFile.outputStream().use { outputStream ->
                        inputStream.copyTo(outputStream)
                    }
                    Toast.makeText(this, "Cookies imported successfully!", Toast.LENGTH_SHORT).show()
                    hasCookiesFileState.value = true
                }
            } catch (e: Exception) {
                Toast.makeText(this, "Failed to import cookies: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private val hasCookiesFileState = mutableStateOf(false)

    // Shared state variables
    private var urlInputTextState = mutableStateOf("")
    private val hasStoragePermissionState = mutableStateOf(false)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        hasStoragePermissionState.value = checkStoragePermission()
        hasCookiesFileState.value = File(filesDir, "cookies.txt").exists()
        requestPermissions()
        handleSharedIntent(intent)

        setContent {
            val isDark = isSystemInDarkTheme()
            val colors = if (isDark) {
                darkColorScheme(
                    primary = Color(0xFF42A5F5),
                    background = Color(0xFF121214),
                    surface = Color(0xFF1E1E24),
                    onBackground = Color(0xFFE2E2E6),
                    onSurface = Color(0xFFE2E2E6)
                )
            } else {
                lightColorScheme(
                    primary = Color(0xFF0078D7),
                    background = Color(0xFFF4F4F9),
                    surface = Color(0xFFFFFFFF),
                    onBackground = Color(0xFF222222),
                    onSurface = Color(0xFF222222)
                )
            }

            MaterialTheme(colorScheme = colors) {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    MainScreen(
                        urlInputText = urlInputTextState.value,
                        onUrlInputChange = { urlInputTextState.value = it },
                        onStoragePermissionRequest = { promptStoragePermission() },
                        hasCookiesFile = hasCookiesFileState.value,
                        onImportCookies = { importCookiesLauncher.launch(arrayOf("*/*")) },
                        onClearCookies = {
                            val file = File(filesDir, "cookies.txt")
                            if (file.exists()) file.delete()
                            hasCookiesFileState.value = false
                            Toast.makeText(this@MainActivity, "Cookies cleared", Toast.LENGTH_SHORT).show()
                        },
                        hasStoragePermission = hasStoragePermissionState.value
                    )
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleSharedIntent(intent)
    }

    private fun handleSharedIntent(intent: Intent?) {
        if (intent?.action == Intent.ACTION_SEND && intent.type == "text/plain") {
            val sharedText = intent.getStringExtra(Intent.EXTRA_TEXT)
            if (!sharedText.isNullOrBlank()) {
                val current = urlInputTextState.value
                urlInputTextState.value = if (current.isBlank()) {
                    sharedText
                } else {
                    current.trim() + "\n" + sharedText
                }
            }
        }
    }

    private fun requestPermissions() {
        val permissions = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            permissions.add(Manifest.permission.POST_NOTIFICATIONS)
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
            permissions.add(Manifest.permission.WRITE_EXTERNAL_STORAGE)
        }

        if (permissions.isNotEmpty()) {
            requestPermissionLauncher.launch(permissions.toTypedArray())
        }
    }

    private fun promptStoragePermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            if (!Environment.isExternalStorageManager()) {
                try {
                    val intent = Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION).apply {
                        data = Uri.parse("package:$packageName")
                    }
                    requestManageStorageLauncher.launch(intent)
                } catch (e: Exception) {
                    val intent = Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION)
                    requestManageStorageLauncher.launch(intent)
                }
            } else {
                Toast.makeText(this, "Storage access is already granted", Toast.LENGTH_SHORT).show()
            }
        } else {
            Toast.makeText(this, "Storage access already configured", Toast.LENGTH_SHORT).show()
        }
    }

    private fun checkStoragePermission(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Environment.isExternalStorageManager()
        } else {
            ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
        }
    }

    override fun onResume() {
        super.onResume()
        hasStoragePermissionState.value = checkStoragePermission()
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalFoundationApi::class)
@Composable
fun MainScreen(
    urlInputText: String,
    onUrlInputChange: (String) -> Unit,
    onStoragePermissionRequest: () -> Unit,
    hasCookiesFile: Boolean,
    onImportCookies: () -> Unit,
    onClearCookies: () -> Unit,
    hasStoragePermission: Boolean
) {
    val context = LocalContext.current
    val clipboardManager = LocalClipboardManager.current
    
    val downloadState by DownloadState.state.collectAsStateWithLifecycle()

    var qualityExpanded by remember { mutableStateOf(false) }
    val qualityOptions = listOf(
        "Best (Highest)" to "best",
        "8K (4320p)" to "4320",
        "4K (2160p)" to "2160",
        "2K (1440p)" to "1440",
        "1080p" to "1080",
        "720p" to "720",
        "480p" to "480",
        "360p" to "360"
    )
    var selectedQuality by remember { mutableStateOf(qualityOptions[0]) }

    var formatExpanded by remember { mutableStateOf(false) }
    val formatOptions = listOf(
        "Default" to "default",
        "MP4" to "mp4",
        "WebM" to "webm",
        "MP3" to "mp3"
    )
    var selectedFormat by remember { mutableStateOf(formatOptions[0]) }

    var advancedExpanded by remember { mutableStateOf(false) }
    var customArguments by remember { mutableStateOf("") }

    val lazyListState = rememberLazyListState()

    // Auto-scroll logs to bottom as they arrive
    LaunchedEffect(downloadState.logItems.size) {
        if (downloadState.logItems.isNotEmpty()) {
            lazyListState.animateScrollToItem(downloadState.logItems.size - 1)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Universal Video Downloader", fontWeight = FontWeight.Bold) },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                    titleContentColor = MaterialTheme.colorScheme.onSurface
                )
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .padding(paddingValues)
                .fillMaxSize()
                .padding(10.dp)
        ) {
            // Header Info - subtitle removed as requested, using space instead
            Spacer(modifier = Modifier.height(4.dp))

            // URL input block
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                shape = RoundedCornerShape(8.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Enter Video URLs (one per line):",
                            fontWeight = FontWeight.SemiBold,
                            fontSize = 14.sp
                        )
                        Row(
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            IconButton(
                                onClick = {
                                    clipboardManager.getText()?.let {
                                        if (urlInputText.isBlank()) {
                                            onUrlInputChange(it.text)
                                        } else {
                                            onUrlInputChange(urlInputText.trim() + "\n" + it.text)
                                        }
                                    }
                                },
                                modifier = Modifier.size(32.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.ContentPaste,
                                    contentDescription = "Paste",
                                    tint = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                            Spacer(modifier = Modifier.width(4.dp))
                            IconButton(
                                onClick = { onUrlInputChange("") },
                                modifier = Modifier.size(32.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Clear,
                                    contentDescription = "Clear",
                                    tint = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f),
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                    }
                    
                    Spacer(modifier = Modifier.height(8.dp))

                    OutlinedTextField(
                        value = urlInputText,
                        onValueChange = onUrlInputChange,
                        placeholder = {
                            Text(
                                "Paste youtube, fb, vimeo links etc.\n(Supports 10000+ batch)",
                                color = Color.LightGray
                            )
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(90.dp),
                        maxLines = 10,
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = MaterialTheme.colorScheme.primary,
                            unfocusedBorderColor = Color.LightGray
                        )
                    )
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Dropdown parameters Row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                // Quality ComboBox
                Column(modifier = Modifier.weight(1.2f)) {
                    Text("Quality:", fontSize = 12.sp, fontWeight = FontWeight.Medium, modifier = Modifier.padding(bottom = 4.dp))
                    Box {
                        OutlinedButton(
                            onClick = { qualityExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(6.dp),
                            contentPadding = PaddingValues(horizontal = 10.dp, vertical = 8.dp)
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(selectedQuality.first, color = MaterialTheme.colorScheme.onBackground, fontSize = 13.sp)
                                Icon(Icons.Default.ArrowDropDown, contentDescription = null, tint = MaterialTheme.colorScheme.onBackground)
                            }
                        }
                        DropdownMenu(
                            expanded = qualityExpanded,
                            onDismissRequest = { qualityExpanded = false },
                            modifier = Modifier.fillMaxWidth(0.5f)
                        ) {
                            qualityOptions.forEach { option ->
                                DropdownMenuItem(
                                    text = { Text(option.first, fontSize = 13.sp) },
                                    onClick = {
                                        selectedQuality = option
                                        qualityExpanded = false
                                    }
                                )
                            }
                        }
                    }
                }

                // Format ComboBox
                Column(modifier = Modifier.weight(0.8f)) {
                    Text("Format:", fontSize = 12.sp, fontWeight = FontWeight.Medium, modifier = Modifier.padding(bottom = 4.dp))
                    Box {
                        OutlinedButton(
                            onClick = { formatExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(6.dp),
                            contentPadding = PaddingValues(horizontal = 10.dp, vertical = 8.dp)
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(selectedFormat.first, color = MaterialTheme.colorScheme.onBackground, fontSize = 13.sp)
                                Icon(Icons.Default.ArrowDropDown, contentDescription = null, tint = MaterialTheme.colorScheme.onBackground)
                            }
                        }
                        DropdownMenu(
                            expanded = formatExpanded,
                            onDismissRequest = { formatExpanded = false },
                            modifier = Modifier.fillMaxWidth(0.35f)
                        ) {
                            formatOptions.forEach { option ->
                                DropdownMenuItem(
                                    text = { Text(option.first, fontSize = 13.sp) },
                                    onClick = {
                                        selectedFormat = option
                                        formatExpanded = false
                                    }
                                )
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Advanced options toggle
            Column {
                Text(
                    text = if (advancedExpanded) "▼ Advanced Options" else "▶ Advanced Options",
                    color = MaterialTheme.colorScheme.primary,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier
                        .clickable { advancedExpanded = !advancedExpanded }
                        .padding(vertical = 4.dp)
                )
                AnimatedVisibility(visible = advancedExpanded) {
                    Column(modifier = Modifier.padding(vertical = 8.dp)) {
                        Text("Custom Arguments (e.g. --proxy URL):", fontSize = 12.sp, color = Color.Gray, modifier = Modifier.padding(bottom = 4.dp))
                        OutlinedTextField(
                            value = customArguments,
                            onValueChange = { customArguments = it },
                            modifier = Modifier.fillMaxWidth(),
                            placeholder = { Text("--no-playlist --restrict-filenames", color = Color.LightGray) },
                            singleLine = true
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = if (hasStoragePermission) {
                                "✅ Public Downloads: Downloads/UVD Downloads"
                            } else {
                                "⚠️ Private Storage Fallback (Click to Enable)"
                            },
                            color = if (hasStoragePermission) Color(0xFF28A745) else Color(0xFFD32F2F),
                            fontSize = 12.sp,
                            fontWeight = FontWeight.SemiBold,
                            modifier = Modifier
                                .clickable { onStoragePermissionRequest() }
                                .padding(vertical = 4.dp)
                        )
                        Spacer(modifier = Modifier.height(10.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = if (hasCookiesFile) "Cookies: Loaded" else "Cookies: Not Loaded",
                                fontSize = 12.sp,
                                color = if (hasCookiesFile) Color(0xFF28A745) else Color.Gray
                            )
                            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                                Text(
                                    text = "Import cookies.txt",
                                    color = MaterialTheme.colorScheme.primary,
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.SemiBold,
                                    modifier = Modifier.clickable { onImportCookies() }
                                )
                                if (hasCookiesFile) {
                                    Text(
                                        text = "Clear",
                                        color = Color(0xFFD32F2F),
                                        fontSize = 12.sp,
                                        fontWeight = FontWeight.SemiBold,
                                        modifier = Modifier.clickable { onClearCookies() }
                                    )
                                }
                            }
                        }
                    }
                }
            }

            if (!hasStoragePermission) {
                Card(
                    modifier = Modifier.fillMaxWidth().padding(bottom = 6.dp),
                    colors = CardDefaults.cardColors(containerColor = Color(0xFFFFF3CD)), // warning light yellow
                    shape = RoundedCornerShape(4.dp)
                ) {
                    Text(
                        text = "⚠️ Files are saving to app-private folder. Click here to grant storage permission so you can manage them in public Downloads/UVD Downloads.",
                        color = Color(0xFF856404),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier
                            .clickable { onStoragePermissionRequest() }
                            .padding(8.dp)
                    )
                }
            } else {
                Spacer(modifier = Modifier.height(8.dp))
            }

            // Action Button: Download now / Stop
            Box(modifier = Modifier.fillMaxWidth()) {
                if (!downloadState.isDownloading) {
                    Button(
                        onClick = {
                            if (urlInputText.isNotBlank()) {
                                val urlsList = urlInputText.split("\n")
                                    .map { it.trim() }
                                    .filter { it.isNotEmpty() }
                                
                                val intent = Intent(context, DownloadService::class.java).apply {
                                    action = DownloadService.ACTION_START
                                    putStringArrayListExtra(DownloadService.EXTRA_URLS, ArrayList(urlsList))
                                    putExtra(DownloadService.EXTRA_QUALITY, selectedQuality.second)
                                    putExtra(DownloadService.EXTRA_FORMAT, selectedFormat.second)
                                    putExtra(DownloadService.EXTRA_CUSTOM_ARGS, customArguments)
                                }
                                ContextCompat.startForegroundService(context, intent)
                            }
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(44.dp),
                        shape = RoundedCornerShape(6.dp)
                    ) {
                        Text("Download now", fontSize = 15.sp, fontWeight = FontWeight.Bold, color = Color.White)
                    }
                } else {
                    Button(
                        onClick = {
                            val intent = Intent(context, DownloadService::class.java).apply {
                                action = DownloadService.ACTION_STOP
                            }
                            context.startService(intent)
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFD32F2F)),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(44.dp),
                        shape = RoundedCornerShape(6.dp)
                    ) {
                        Text("Stop Batch", fontSize = 15.sp, fontWeight = FontWeight.Bold, color = Color.White)
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Status and Log header
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("Log & Progress : ", fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                Text(
                    text = downloadState.batchStatus,
                    color = MaterialTheme.colorScheme.primary,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 14.sp
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Progress bar, status string, and console logging card
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                shape = RoundedCornerShape(6.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    LinearProgressIndicator(
                        progress = { downloadState.progress / 100f },
                        color = Color(0xFF28A745),
                        trackColor = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.12f),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(10.dp)
                            .clip(RoundedCornerShape(4.dp))
                    )
                    
                    Spacer(modifier = Modifier.height(4.dp))
                    
                    Text(
                        text = if (downloadState.isDownloading) downloadState.currentLine.ifBlank { "Downloading..." } else "Ready",
                        fontSize = 12.sp,
                        color = Color.Gray,
                        maxLines = 1,
                        modifier = Modifier.padding(bottom = 8.dp)
                    )

                    // Live console terminal block
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                            .background(Color(0xFF1E1E1E), shape = RoundedCornerShape(4.dp))
                            .combinedClickable(
                                onLongClick = {
                                    val logsText = downloadState.logItems.joinToString("\n")
                                    clipboardManager.setText(androidx.compose.ui.text.AnnotatedString(logsText))
                                    Toast.makeText(context, "All logs copied to clipboard", Toast.LENGTH_SHORT).show()
                                },
                                onClick = {}
                            )
                            .padding(8.dp)
                    ) {
                        LazyColumn(
                            state = lazyListState,
                            modifier = Modifier.fillMaxSize()
                        ) {
                            items(downloadState.logItems) { item ->
                                Text(
                                    text = item,
                                    color = Color(0xFF00FF00), // green matrix console text
                                    fontFamily = FontFamily.Monospace,
                                    fontSize = 11.sp,
                                    modifier = Modifier.padding(vertical = 1.dp)
                                )
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(6.dp))

            // Footer Links
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "❤️ Join",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable {
                            val intent = Intent(Intent.ACTION_VIEW, Uri.parse("https://www.reddit.com/r/youtubedl_gui/"))
                            context.startActivity(intent)
                        }
                    )
                    Text("|", fontSize = 12.sp, color = Color.Gray)
                    Text(
                        text = "🐙 Support",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable {
                            val intent = Intent(Intent.ACTION_VIEW, Uri.parse("https://github.com/Dinesh6777/Universal-Video-Downloader-GUI/issues"))
                            context.startActivity(intent)
                        }
                    )
                }

                val downloadDir = remember { DownloadManager.getDownloadDir(context) }
                Text(
                    text = "📂 Open Downloads",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.primary,
                    textDecoration = TextDecoration.Underline,
                    modifier = Modifier.clickable {
                        val dir = DownloadManager.getDownloadDir(context)
                        val isPublic = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                            Environment.isExternalStorageManager()
                        } else {
                            ContextCompat.checkSelfPermission(context, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
                        }
                        
                        fun openFallbackPicker(ctx: Context, directory: File) {
                            try {
                                val uri = androidx.core.content.FileProvider.getUriForFile(
                                    ctx,
                                    "${ctx.packageName}.provider",
                                    directory
                                )
                                val intent = Intent(Intent.ACTION_VIEW).apply {
                                    setDataAndType(uri, "vnd.android.document/directory")
                                    addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                                    addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                }
                                ctx.startActivity(intent)
                            } catch (e: Exception) {
                                try {
                                    val uri = androidx.core.content.FileProvider.getUriForFile(
                                        ctx,
                                        "${ctx.packageName}.provider",
                                        directory
                                    )
                                    val intent = Intent(Intent.ACTION_VIEW).apply {
                                        setDataAndType(uri, "*/*")
                                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                                        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                    }
                                    ctx.startActivity(intent)
                                } catch (e2: Exception) {
                                    try {
                                        val intent = Intent(Intent.ACTION_GET_CONTENT).apply {
                                            type = "*/*"
                                            addCategory(Intent.CATEGORY_OPENABLE)
                                        }
                                        ctx.startActivity(intent)
                                    } catch (e3: Exception) {
                                        Toast.makeText(ctx, "Could not open downloads directory", Toast.LENGTH_SHORT).show()
                                    }
                                }
                            }
                        }

                        if (isPublic) {
                            try {
                                val uri = Uri.parse("content://com.android.externalstorage.documents/document/primary%3ADownload%2FUVD%20Downloads")
                                val intent = Intent(Intent.ACTION_VIEW).apply {
                                    setDataAndType(uri, "vnd.android.document/directory")
                                    addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                                    addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                }
                                context.startActivity(intent)
                            } catch (ex1: Exception) {
                                try {
                                    val uri = Uri.parse("content://com.android.externalstorage.documents/document/primary%3ADownload")
                                    val intent = Intent(Intent.ACTION_VIEW).apply {
                                        setDataAndType(uri, "vnd.android.document/directory")
                                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                                        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                    }
                                    context.startActivity(intent)
                                } catch (ex2: Exception) {
                                    try {
                                        val intent = Intent(android.app.DownloadManager.ACTION_VIEW_DOWNLOADS)
                                        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                        context.startActivity(intent)
                                    } catch (ex3: Exception) {
                                        openFallbackPicker(context, dir)
                                    }
                                }
                            }
                        } else {
                            openFallbackPicker(context, dir)
                        }
                    }
                )
            }
        }
    }
}
