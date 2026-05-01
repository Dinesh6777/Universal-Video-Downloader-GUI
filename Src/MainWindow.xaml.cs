using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO.Compression; // Required for extraction

namespace YtDlpDownloader;

public partial class MainWindow : Window
{
    private readonly string _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
    private readonly string _updateCheckPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_update.txt");
    private Process? _currentProcess;
    private bool _isDownloading = false;
    private bool _stopRequested = false;

    public MainWindow() => InitializeComponent();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Log("Application Initialized.");
        
        // Start dependencies check
        await CheckFFmpeg();
        
        try
        {
            await CheckAndDownloadYtDlp();
            await AutoUpdateYtDlp();
        }
        catch (Exception ex) { Log($"Startup Alert: {ex.Message}"); }
    }

    private async Task CheckFFmpeg()
    {
        // 1. Check if ffmpeg.exe exists locally in the app folder
        string localFfmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(localFfmpeg))
        {
            Log("✅ FFmpeg detected in application folder.");
            return;
        }

        // 2. Check if ffmpeg is available in the system PATH
        try
        {
            var psi = new ProcessStartInfo { FileName = "ffmpeg", Arguments = "-version", CreateNoWindow = true, UseShellExecute = false };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();
            Log("✅ FFmpeg detected in system PATH.");
        }
        catch
        {
            // 3. Not found: Prompt user to download
            var result = MessageBox.Show(
                "FFmpeg is missing! Without it, high-quality video merging (4K/8K) and MP3 conversion will not work.\n\n" +
                "Would you like to download and install FFmpeg automatically?",
                "FFmpeg Required", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await DownloadAndInstallFFmpeg();
            }
            else
            {
                Log("⚠️ FFmpeg skipped. Downloads will be limited to 720p/1080p and MP3 conversion will fail.");
            }
        }
    }

    private async Task DownloadAndInstallFFmpeg()
    {
        // Using a reliable direct link to a GPL shared build
        string ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg_temp.zip");

        try
        {
            Log("🚀 Starting FFmpeg download...");
            TxtStatus.Text = "Downloading FFmpeg...";
            PrgBar.Value = 0;

            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(ffmpegUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var downloadStream = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[81920];
                        var totalRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes != -1)
                            {
                                var progress = (double)totalRead / totalBytes * 100;
                                Dispatcher.Invoke(() => PrgBar.Value = progress);
                            }
                        }
                    }
                }
            }

            Log("📦 Extracting ffmpeg.exe...");
            TxtStatus.Text = "Extracting...";

            await Task.Run(() =>
            {
                using (ZipArchive archive = ZipFile.OpenRead(tempZip))
                {
                    // Find ffmpeg.exe inside the zip (it's usually inside a /bin/ folder)
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        string destPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                        entry.ExtractToFile(destPath, true);
                    }
                }
            });

            Log("✅ FFmpeg installed successfully.");
            TxtStatus.Text = "Ready";
        }
        catch (Exception ex)
        {
            Log($"❌ FFmpeg Error: {ex.Message}");
            MessageBox.Show("Failed to download FFmpeg. You may need to install it manually.");
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            PrgBar.Value = 0;
        }
    }

    private async Task CheckAndDownloadYtDlp()
    {
        if (!File.Exists(_ytDlpPath))
        {
            Log("yt-dlp.exe missing. Downloading...");
            TxtStatus.Text = "Downloading tool...";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var data = await client.GetByteArrayAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");
            await File.WriteAllBytesAsync(_ytDlpPath, data);
            Log("yt-dlp downloaded.");
            TxtStatus.Text = "Ready";
        }
    }

    private async Task AutoUpdateYtDlp()
    {
        if (!File.Exists(_ytDlpPath)) return;
        bool shouldUpdate = !File.Exists(_updateCheckPath);
        if (!shouldUpdate && DateTime.TryParse(await File.ReadAllTextAsync(_updateCheckPath), out DateTime last))
        {
            if ((DateTime.Now - last).TotalDays >= 7) shouldUpdate = true;
        }

        if (shouldUpdate)
        {
            Log("Checking for updates...");
            try
            {
                var proc = Process.Start(new ProcessStartInfo { FileName = _ytDlpPath, Arguments = "--update", CreateNoWindow = true, UseShellExecute = false });
                if (proc != null) await proc.WaitForExitAsync();
                await File.WriteAllTextAsync(_updateCheckPath, DateTime.Now.ToString());
            }
            catch { Log("Update check failed/skipped."); }
        }
    }

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading) return;
        
        var urls = UrlInput.Text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToList();

        if (!urls.Any())
        {
            MessageBox.Show("Please enter at least one URL.");
            return;
        }

        string quality = (QualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best";
        
        ToggleUI(true);
        _stopRequested = false;
        LstLog.Items.Clear();

        for (int i = 0; i < urls.Count; i++)
        {
            if (_stopRequested) break;
            
            BatchStatus.Text = $"Downloading {i + 1}/{urls.Count} (Remaining: {urls.Count - (i + 1)})";
            PrgBar.Value = 0;
            await StartDownload(urls[i], quality);
        }

        ToggleUI(false);
        BtnOpenFolder.Visibility = Visibility.Visible;
        BatchStatus.Text = _stopRequested ? "Batch Stopped" : "All Downloads Finished";
    }

    private async Task StartDownload(string url, string quality)
    {
        try
        {
            var psi = new ProcessStartInfo {
                FileName = _ytDlpPath, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };

            if (quality == "mp3") {
                psi.ArgumentList.Add("-x"); 
                psi.ArgumentList.Add("--audio-format"); psi.ArgumentList.Add("mp3");
                psi.ArgumentList.Add("--audio-quality"); psi.ArgumentList.Add("0");
            } else if (quality == "best") {
                psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("bestvideo+bestaudio/best");
            } else {
                psi.ArgumentList.Add("-f"); psi.ArgumentList.Add($"bestvideo[height<={quality}]+bestaudio/best[height<={quality}]");
            }

            psi.ArgumentList.Add("--newline"); psi.ArgumentList.Add("-o"); 
            psi.ArgumentList.Add("Downloads/%(title)s.%(ext)s");
            psi.ArgumentList.Add(url);

            _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _currentProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Dispatcher.Invoke(() => { Log(e.Data); ParseProgress(e.Data); }); };
            _currentProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Dispatcher.Invoke(() => Log($"[ERROR] {e.Data}")); };

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();
            await _currentProcess.WaitForExitAsync();
        }
        catch (Exception ex) { Log($"[CRITICAL] {ex.Message}"); }
        finally { _currentProcess = null; }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _stopRequested = true;
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            try { _currentProcess.Kill(true); Log("🛑 User stopped the download."); }
            catch { }
        }
    }

    private void ToggleUI(bool downloading)
    {
        _isDownloading = downloading;
        BtnDownload.Visibility = downloading ? Visibility.Collapsed : Visibility.Visible;
        BtnStop.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        UrlInput.IsEnabled = !downloading;
        QualityCombo.IsEnabled = !downloading;
    }

    private void ParseProgress(string line)
    {
        var match = Regex.Match(line, @"(\d+(\.\d+)?)%");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double val)) PrgBar.Value = val;
    }

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => Log(message)); return; }
        LstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (LstLog.Items.Count > 0) LstLog.ScrollIntoView(LstLog.Items[LstLog.Items.Count - 1]);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => UrlInput.Clear();

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        Process.Start("explorer.exe", path);
    }
}