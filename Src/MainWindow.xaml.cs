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
        try
        {
            var psi = new ProcessStartInfo { FileName = "ffmpeg", Arguments = "-version", CreateNoWindow = true, UseShellExecute = false };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();
            Log("✅ FFmpeg detected. High-quality merging and MP3 conversion enabled.");
        }
        catch
        {
            Log("⚠️ FFmpeg NOT detected! Downloads may fail to merge or convert to MP3.");
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
            Log("Checking for yt-dlp updates...");
            try
            {
                var proc = Process.Start(new ProcessStartInfo { FileName = _ytDlpPath, Arguments = "--update", CreateNoWindow = true, UseShellExecute = false });
                if (proc != null) await proc.WaitForExitAsync();
                await File.WriteAllTextAsync(_updateCheckPath, DateTime.Now.ToString());
                Log("Update check complete.");
            }
            catch { Log("Update check skipped."); }
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
        
        _isDownloading = true;
        _stopRequested = false;
        BtnDownload.Visibility = Visibility.Collapsed;
        BtnStop.Visibility = Visibility.Visible;
        LstLog.Items.Clear();

        for (int i = 0; i < urls.Count; i++)
        {
            if (_stopRequested) break;
            
            // Progress tracker update (e.g. 1/10)
            BatchStatus.Text = $"Downloading {i + 1}/{urls.Count} (Remaining: {urls.Count - (i + 1)})";
            PrgBar.Value = 0;
            await StartDownload(urls[i], quality);
        }

        _isDownloading = false;
        BtnDownload.Visibility = Visibility.Visible;
        BtnStop.Visibility = Visibility.Collapsed;
        BtnOpenFolder.Visibility = Visibility.Visible;
        BatchStatus.Text = _stopRequested ? "Batch Stopped" : "Batch Complete";
    }

    private async Task StartDownload(string url, string quality)
    {
        string outputTemplate = url.Contains("list=") 
            ? "Downloads/%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s" 
            : "Downloads/%(title)s.%(ext)s";

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

            psi.ArgumentList.Add("--newline"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(outputTemplate);
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
            try { _currentProcess.Kill(true); Log("🛑 Stop requested."); }
            catch { }
        }
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