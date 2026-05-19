using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace UniversalVideoDownloader;

public partial class MainWindow : Window
{
    private readonly string _ytDlpFileName;
    private readonly string _ffmpegFileName;
    private readonly string _denoFileName;
    
    private readonly string _ytDlpPath;
    private Process? _currentProcess;
    private bool _isDownloading = false;
    private bool _stopRequested = false;
    
    public ObservableCollection<string> LogItems { get; } = new();

    private Action? _dialogYesAction;

    public MainWindow()
    {
        InitializeComponent();
        LstLog.ItemsSource = LogItems;

        _ytDlpFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
        _ffmpegFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        _denoFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "deno.exe" : "deno";

        _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _ytDlpFileName);

        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        Log("Application Initialized.");
        try {
            await CheckAndDownloadYtDlp();
            await CheckToolVersions(); 
            await Task.Delay(1000);
            _ = CheckForAppUpdates(); 
        } catch (Exception ex) { Log($"Startup Alert: {ex.Message}"); }
    }

    private async Task CheckToolVersions()
    {
        Log("🔍 Checking dependency versions...");
        
        // 1. YT-DLP CHECK
        string ytPath = File.Exists(_ytDlpPath) ? _ytDlpPath : _ytDlpFileName;
        string ytVersion = await GetToolVersion(ytPath);
        Log($"▶ Current yt-dlp: {ytVersion}");

        string latestYt = await GetLatestGithubRelease("yt-dlp/yt-dlp");
        if (!string.IsNullOrEmpty(latestYt) && !ytVersion.Contains(latestYt))
        {
            Log($"🚀 New yt-dlp version available: {latestYt}");
            if (File.Exists(_ytDlpPath)) {
                Log("🔄 Auto-updating yt-dlp in app folder...");
                var p = Process.Start(new ProcessStartInfo { FileName = _ytDlpPath, Arguments = "--update", CreateNoWindow = true });
                if (p != null) await p.WaitForExitAsync();
                Log($"✅ yt-dlp updated to: {await GetToolVersion(_ytDlpPath)}");
            } else {
                Log("⚠️ yt-dlp is running from system PATH. Please update it manually.");
            }
        }
        else if (!string.IsNullOrEmpty(latestYt)) {
            Log("✅ yt-dlp is up to date.");
        }

        // 2. FFMPEG CHECK 
        await CheckFFmpeg();
    }

    private async Task CheckFFmpeg()
    {
        bool inAppFolder = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _ffmpegFileName));
        bool inSystemPath = false;
        
        if (!inAppFolder)
        {
            inSystemPath = await IsToolInSystemPath(_ffmpegFileName);
        }

        if (!inAppFolder && !inSystemPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Dispatcher.UIThread.Post(() => {
                    ShowDialog("FFmpeg Missing", 
                        "FFmpeg is missing! High-quality merging and MP3s require it.\nDownload automatically?", 
                        async () => {
                            await DownloadAndInstallTool("FFmpeg", "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", "ffmpeg.exe");
                            try {
                                string latestFf = await GetLatestGithubRelease("yt-dlp/FFmpeg-Builds");
                                await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_tag.txt"), latestFf);
                            } catch { }
                        });
                });
            }
            else
            {
                Log("⚠️ FFmpeg missing. Please install it manually (e.g. 'sudo apt install ffmpeg').");
            }
            return;
        }

        string ffPath = inAppFolder ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _ffmpegFileName) : _ffmpegFileName;
        string ffVersion = await GetToolVersion(ffPath);
        Log($"▶ Current ffmpeg: {ffVersion}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string latestFf = await GetLatestGithubRelease("yt-dlp/FFmpeg-Builds");
            string localFfTagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg_tag.txt");
            string localFfTag = File.Exists(localFfTagPath) ? await File.ReadAllTextAsync(localFfTagPath) : "";

            if (!string.IsNullOrEmpty(latestFf) && localFfTag != latestFf)
            {
                Log($"🚀 New ffmpeg build available: {latestFf}");
                
                if (inAppFolder)
                {
                    Dispatcher.UIThread.Post(() => {
                        ShowDialog("FFmpeg Update Available", 
                            $"A new version of FFmpeg is available!\n(Current: {ffVersion})\n(Latest: {latestFf})\n\nDownload and update automatically?", 
                            async () => {
                                Log("🔄 Auto-updating ffmpeg in app folder...");
                                await DownloadAndInstallTool("FFmpeg", "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", _ffmpegFileName);
                                await File.WriteAllTextAsync(localFfTagPath, latestFf);
                                Log($"✅ ffmpeg updated to latest build.");
                            });
                    });
                }
                else if (inSystemPath)
                {
                    Log("⚠️ ffmpeg is running from system PATH. Please update it manually.");
                }
            }
            else if (!string.IsNullOrEmpty(latestFf))
            {
                Log("✅ ffmpeg is up to date.");
            }
        }
    }

    private async Task<string> GetToolVersion(string path)
    {
        try {
            var psi = new ProcessStartInfo { FileName = path, Arguments = "--version", RedirectStandardOutput = true, CreateNoWindow = true, UseShellExecute = false };
            if (path.Contains("ffmpeg")) psi.Arguments = "-version";
            
            using var p = Process.Start(psi);
            if (p != null) {
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                return output.Split('\n')[0].Trim();
            }
        } catch { }
        return "Unknown";
    }

    private async Task<string> GetLatestGithubRelease(string repo)
    {
        try {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "UVD-App");
            var response = await client.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        } catch { return ""; }
    }

    private async Task CheckForAppUpdates()
    {
        try
        {
            string fullPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string fileName = Path.GetFileName(fullPath);

            var currentMatch = Regex.Match(fileName, @"\.v(\d+\.\d+)");
            if (!currentMatch.Success) return;
            Version currentVersion = new Version(currentMatch.Groups[1].Value);

            string latestApp = await GetLatestGithubRelease("Dinesh6777/Universal-Video-Downloader-GUI");
            var githubMatch = Regex.Match(latestApp, @"UVDv(\d+\.\d+)");
            if (githubMatch.Success)
            {
                Version latestVersion = new Version(githubMatch.Groups[1].Value);
                if (latestVersion > currentVersion)
                {
                    Dispatcher.UIThread.Post(() => {
                        ShowDialog("Update Available", 
                            $"New version of Universal Video Downloader {latestVersion} is available!\n(Current: {currentVersion})\n\nWould you like to download it now?",
                            () => OpenUrl("https://github.com/Dinesh6777/Universal-Video-Downloader-GUI/releases"));
                    });
                }
                else { Log($"✅ App is up to date (v{currentVersion})."); }
            }
        }
        catch { }
    }

    private async void RbCookies_Checked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            Log($"Option selected: {rb.Content}");
            await CheckAndInstallDeno();
        }
    }

    private async Task CheckAndInstallDeno()
    {
        string localDeno = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _denoFileName);
        if (File.Exists(localDeno)) { Log("✅ Deno found in app folder."); return; }
        if (await IsToolInSystemPath(_denoFileName)) { Log("✅ Deno found in system PATH."); return; }

        Log("🔍 Deno missing. Starting auto-install...");
        Architecture arch = RuntimeInformation.OSArchitecture;
        string url = "";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            url = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            url = arch == Architecture.Arm64 
                ? "https://github.com/denoland/deno/releases/latest/download/deno-aarch64-apple-darwin.zip" 
                : "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-apple-darwin.zip";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && arch == Architecture.X64)
            url = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-unknown-linux-gnu.zip";

        if (!string.IsNullOrEmpty(url)) await DownloadAndInstallTool("Deno", url, _denoFileName);
        else Log("❌ Error: Unsupported OS/Architecture for automated Deno install.");
    }

    private async Task<bool> IsToolInSystemPath(string tool)
    {
        try {
            var psi = new ProcessStartInfo { FileName = tool, Arguments = "--version", CreateNoWindow = true, UseShellExecute = false };
            using var proc = Process.Start(psi);
            if (proc != null) { await proc.WaitForExitAsync(); return true; }
        } catch { }
        return false;
    }

    private async Task DownloadAndInstallTool(string tool, string url, string exe)
    {
        string zip = Path.Combine(Path.GetTempPath(), $"{tool}_temp.zip");
        try {
            Log($"🚀 Downloading {tool}...");
            using var client = new HttpClient();
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var total = resp.Content.Headers.ContentLength ?? -1L;
            
            using (var fs = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var ds = await resp.Content.ReadAsStreamAsync()) {
                var buffer = new byte[81920]; var read = 0L; int b;
                while ((b = await ds.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                    await fs.WriteAsync(buffer, 0, b); read += b;
                    if (total != -1) Dispatcher.UIThread.Post(() => PrgBar.Value = (double)read / total * 100);
                }
            }
            
            Log($"📦 Extracting {exe}...");
            string extractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exe);
            await Task.Run(() => {
                using var arch = ZipFile.OpenRead(zip);
                var entry = arch.Entries.FirstOrDefault(e => e.Name.Equals(exe, StringComparison.OrdinalIgnoreCase));
                if (entry != null) entry.ExtractToFile(extractPath, true);
            });

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("chmod", $"+x \"{extractPath}\"")?.WaitForExit();

            Log($"✅ {tool} Installed successfully.");
        } catch (Exception ex) { Log($"❌ {tool} install failed: {ex.Message}"); }
        finally { if (File.Exists(zip)) try { File.Delete(zip); } catch { } PrgBar.Value = 0; }
    }

    private async Task CheckAndDownloadYtDlp() 
    { 
        if (!File.Exists(_ytDlpPath)) { 
            Log("yt-dlp missing. Downloading..."); 
            
            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
                url = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux_aarch64" : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
                url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";

            using var c = new HttpClient(); 
            var d = await c.GetByteArrayAsync(url); 
            await File.WriteAllBytesAsync(_ytDlpPath, d); 

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("chmod", $"+x \"{_ytDlpPath}\"")?.WaitForExit();
        } 
    }

    private async void BtnDownload_Click(object? sender, RoutedEventArgs e) 
    { 
        if (_isDownloading) return; 
        var urls = UrlInput.Text?.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).ToList(); 
        if (urls == null || !urls.Any()) return; 

        string quality = (QualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";
        string format = (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";

        ToggleUI(true); 
        _stopRequested = false; 
        LogItems.Clear(); 
        
        for (int i = 0; i < urls.Count; i++) { 
            if (_stopRequested) break; 
            BatchStatus.Text = $"Downloading {i + 1}/{urls.Count}"; 
            await StartDownload(urls[i], quality, format); 
        } 
        ToggleUI(false); 
        BatchStatus.Text = "Finished"; 
        BtnOpenFolder.IsVisible = true; 
    }

    private async Task StartDownload(string url, string quality, string format)
    {
        string template = url.Contains("list=") ? "Downloads/%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s" : "Downloads/%(title)s.%(ext)s";
        try {
            var psi = new ProcessStartInfo { FileName = _ytDlpPath, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            
            if (format == "mp3") {
                psi.ArgumentList.Add("-x"); 
                psi.ArgumentList.Add("--audio-format"); psi.ArgumentList.Add("mp3");
                psi.ArgumentList.Add("--audio-quality"); psi.ArgumentList.Add("0");
            } else {
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add(quality == "best" ? "bestvideo+bestaudio/best" : $"bestvideo[height<={quality}]+bestaudio/best");
                if (format != "default") { psi.ArgumentList.Add("--merge-output-format"); psi.ArgumentList.Add(format); }
            }

            string extra = AdvancedArgsInput.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(extra)) {
                var matches = Regex.Matches(extra, @"[\""].+?[\""]|[^ ]+");
                foreach (Match m in matches) psi.ArgumentList.Add(m.Value.Replace("\"", ""));
            }

            if (RbFirefox.IsChecked == true) { psi.ArgumentList.Add("--cookies-from-browser"); psi.ArgumentList.Add("firefox"); }
            else if (RbChrome.IsChecked == true) { psi.ArgumentList.Add("--cookies-from-browser"); psi.ArgumentList.Add("chrome"); }

            psi.ArgumentList.Add("--newline"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(template); psi.ArgumentList.Add(url);
            _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            
            _currentProcess.OutputDataReceived += (s, ev) => { 
                if (ev.Data != null) 
                {
                    Log(ev.Data); 
                    ParseProgress(ev.Data); 
                }
            };
            
            _currentProcess.ErrorDataReceived += (s, ev) => { 
                if (ev.Data != null) Log($"[ERROR] {ev.Data}"); 
            };
            
            _currentProcess.Start(); _currentProcess.BeginOutputReadLine(); _currentProcess.BeginErrorReadLine();
            await _currentProcess.WaitForExitAsync();
        } catch (Exception ex) { Log($"Error: {ex.Message}"); } finally { _currentProcess = null; }
    }

    private void UrlInput_TextChanged(object? sender, TextChangedEventArgs e) => Placeholder.IsVisible = string.IsNullOrEmpty(UrlInput.Text);
    
    private async void BtnPaste_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
        {
            string? text = null;
            try
            {
                text = await cb.TryGetTextAsync();
            }
            catch { }

            if (!string.IsNullOrEmpty(text))
            {
                if (string.IsNullOrEmpty(UrlInput.Text))
                    UrlInput.Text = text;
                else
                    UrlInput.Text = UrlInput.Text.TrimEnd() + Environment.NewLine + text;
                
                UrlInput.CaretIndex = UrlInput.Text.Length;
            }
        }
    }

    private void BtnClear_Click(object? sender, RoutedEventArgs e) => UrlInput.Clear();
    
    private void BtnStop_Click(object? sender, RoutedEventArgs e) { 
        _stopRequested = true; 
        try { _currentProcess?.Kill(true); } catch { } 
    }
    
    private void ToggleUI(bool d) { _isDownloading = d; BtnDownload.IsVisible = !d; BtnStop.IsVisible = d; }
    
    private void Log(string m) 
    { 
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Log(m));
            return;
        }

        string trimmed = m.Trim();
        bool isProgress = trimmed.StartsWith("[download]") && trimmed.Contains("%");

        if (isProgress && LogItems.Count > 0)
        {
            string lastLog = LogItems.Last();
            if (lastLog.Contains("[download]") && lastLog.Contains("%"))
            {
                LogItems[LogItems.Count - 1] = $"[{DateTime.Now:HH:mm:ss}] {trimmed}";
                return;
            }
        }
        
        LogItems.Add($"[{DateTime.Now:HH:mm:ss}] {trimmed}");
        LstLog.ScrollIntoView(LogItems.Last()); 
    }

    private void ParseProgress(string? l) 
    { 
        var m = Regex.Match(l ?? "", @"(\d+(\.\d+)?)%"); 
        if (m.Success && double.TryParse(m.Groups[1].Value, out double v)) 
            Dispatcher.UIThread.Post(() => PrgBar.Value = v); 
    }
    
    private void BtnOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        Directory.CreateDirectory(dir);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start("explorer.exe", dir);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", dir);
        else Process.Start("xdg-open", dir);
    }
    
    private void BtnAdvancedToggle_PointerPressed(object? sender, PointerPressedEventArgs e) { AdvancedPanel.IsVisible = !AdvancedPanel.IsVisible; BtnAdvancedToggle.Text = AdvancedPanel.IsVisible ? "▼ Advanced Options" : "▶ Advanced Options"; }
    
    private void Link_PointerPressed(object? sender, PointerPressedEventArgs e) { if (sender is TextBlock tb && tb.Tag is string url) OpenUrl(url); }
    private void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { } }

    private async void LstLog_DoubleTapped(object? sender, TappedEventArgs e) => await CopySelectedLog();
    private async void MenuItemCopy_Click(object? sender, RoutedEventArgs e) => await CopySelectedLog();
    
    private async void MenuItemCopyAll_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
        {
            var allLogs = string.Join(Environment.NewLine, LogItems);
            await cb.SetTextAsync(allLogs);
        }
    }
    
    private async Task CopySelectedLog() { if (LstLog.SelectedItem != null && TopLevel.GetTopLevel(this)?.Clipboard is { } cb) await cb.SetTextAsync(LstLog.SelectedItem.ToString() ?? ""); }

    private void ShowDialog(string title, string message, Action onYes)
    {
        DialogTitle.Text = title; DialogMessage.Text = message;
        _dialogYesAction = onYes; DialogOverlay.IsVisible = true;
    }
    private void DialogYes_Click(object? sender, RoutedEventArgs e) { DialogOverlay.IsVisible = false; _dialogYesAction?.Invoke(); }
    private void DialogNo_Click(object? sender, RoutedEventArgs e) { DialogOverlay.IsVisible = false; }
}