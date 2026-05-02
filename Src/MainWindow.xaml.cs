using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Navigation;

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
        try {
            await CheckAndDownloadYtDlp();
            await AutoUpdateYtDlp();
        } catch (Exception ex) { Log($"Startup Alert: {ex.Message}"); }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        } catch (Exception ex) { Log($"Link Error: {ex.Message}"); }
    }

    // Triggered when RadioButtons for Firefox or Chrome are selected
    private async void RbCookies_Checked(object sender, RoutedEventArgs e)
    {
        if (RbFirefox.IsChecked == true || RbChrome.IsChecked == true)
        {
            await CheckAndInstallDeno();
        }
    }

    private async Task CheckAndInstallDeno()
    {
        // 1. Check App Folder
        if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deno.exe")))
        {
            Log("✅ Deno found locally.");
            return;
        }

        // 2. Check System PATH
        if (await IsToolInSystemPath("deno"))
        {
            Log("✅ Deno detected in system environment.");
            return;
        }

        // 3. Auto-Install based on Architecture
        Log("🔍 Deno not found. Preparing automated installation...");
        Architecture arch = RuntimeInformation.OSArchitecture;
        string url = arch switch
        {
            Architecture.X64 => "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip",
            Architecture.Arm64 => "https://github.com/denoland/deno/releases/latest/download/deno-aarch64-pc-windows-msvc.zip",
            _ => ""
        };

        if (string.IsNullOrEmpty(url))
        {
            Log("⚠️ Architecture not supported for Deno auto-install.");
            return;
        }

        await DownloadAndInstallTool("Deno", url, "deno.exe");
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

    private async Task DownloadAndInstallTool(string tool, string url, string exeName)
    {
        string zip = Path.Combine(Path.GetTempPath(), $"{tool}_temp.zip");
        try {
            Log($"🚀 Downloading {tool}...");
            using (var client = new HttpClient()) {
                using (var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) {
                    resp.EnsureSuccessStatusCode();
                    var total = resp.Content.Headers.ContentLength ?? -1L;
                    using (var fs = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var ds = await resp.Content.ReadAsStreamAsync()) {
                        var buffer = new byte[81920]; var read = 0L; int b;
                        while ((b = await ds.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                            await fs.WriteAsync(buffer, 0, b); read += b;
                            if (total != -1) Dispatcher.Invoke(() => PrgBar.Value = (double)read / total * 100);
                        }
                    }
                }
            } // File handle is closed here

            Log($"📦 Extracting {exeName}...");
            await Task.Run(() => {
                using (var arch = ZipFile.OpenRead(zip)) {
                    // Find the exe by Name (ignores nested folders in zip)
                    var entry = arch.Entries.FirstOrDefault(e => e.Name.Equals(exeName, StringComparison.OrdinalIgnoreCase));
                    if (entry != null) entry.ExtractToFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName), true);
                }
            });
            Log($"✅ {tool} successfully installed.");
        } catch (Exception ex) { Log($"❌ {tool} Install Error: {ex.Message}"); }
        finally { if (File.Exists(zip)) try { File.Delete(zip); } catch { } PrgBar.Value = 0; }
    }

    private async Task CheckFFmpeg()
    {
        if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")) || await IsToolInSystemPath("ffmpeg")) return;
        
        // VERBOSE DIALOG BOX
        var res = MessageBox.Show(
            "FFmpeg is missing! High-quality merging and MP3s require it.\nDownload automatically?", 
            "FFmpeg Missing", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (res == MessageBoxResult.Yes) 
            await DownloadAndInstallTool("FFmpeg", "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", "ffmpeg.exe");
    }

    private async Task StartDownload(string url, string quality)
    {
        string template = url.Contains("list=") ? "Downloads/%(playlist)s/%(playlist_index)s - %(title)s.%(ext)s" : "Downloads/%(title)s.%(ext)s";
        try {
            var psi = new ProcessStartInfo { FileName = _ytDlpPath, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            if (quality == "mp3") { psi.ArgumentList.Add("-x"); psi.ArgumentList.Add("--audio-format"); psi.ArgumentList.Add("mp3"); }
            else { psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(quality == "best" ? "bestvideo+bestaudio/best" : $"bestvideo[height<={quality}]+bestaudio/best"); }
            
            string extra = AdvancedArgsInput.Text.Trim();
            if (!string.IsNullOrEmpty(extra)) {
                var matches = Regex.Matches(extra, @"[\""].+?[\""]|[^ ]+");
                foreach (Match m in matches) psi.ArgumentList.Add(m.Value.Replace("\"", ""));
            }

            if (RbFirefox.IsChecked == true) { psi.ArgumentList.Add("--cookies-from-browser"); psi.ArgumentList.Add("firefox"); }
            else if (RbChrome.IsChecked == true) { psi.ArgumentList.Add("--cookies-from-browser"); psi.ArgumentList.Add("chrome"); }

            psi.ArgumentList.Add("--newline"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(template); psi.ArgumentList.Add(url);
            _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _currentProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Dispatcher.Invoke(() => { Log(e.Data); ParseProgress(e.Data); }); };
            _currentProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Dispatcher.Invoke(() => Log($"[ERROR] {e.Data}")); };
            _currentProcess.Start(); _currentProcess.BeginOutputReadLine(); _currentProcess.BeginErrorReadLine();
            await _currentProcess.WaitForExitAsync();
        } catch (Exception ex) { Log($"Error: {ex.Message}"); } finally { _currentProcess = null; }
    }

    // --- standard logic remains ---
    private async Task CheckAndDownloadYtDlp() { if (!File.Exists(_ytDlpPath)) { Log("yt-dlp missing. Downloading..."); using var c = new HttpClient(); var d = await c.GetByteArrayAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"); await File.WriteAllBytesAsync(_ytDlpPath, d); } }
    private async Task AutoUpdateYtDlp() { if (File.Exists(_ytDlpPath)) { try { var p = Process.Start(new ProcessStartInfo { FileName = _ytDlpPath, Arguments = "--update", CreateNoWindow = true }); if (p != null) await p.WaitForExitAsync(); } catch { } } }
    private async void BtnDownload_Click(object sender, RoutedEventArgs e) { if (_isDownloading) return; var urls = UrlInput.Text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(u => u.Trim()).ToList(); if (!urls.Any()) return; ToggleUI(true); _stopRequested = false; LstLog.Items.Clear(); for (int i = 0; i < urls.Count; i++) { if (_stopRequested) break; BatchStatus.Text = $"Downloading {i + 1}/{urls.Count}"; await StartDownload(urls[i], (QualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "best"); } ToggleUI(false); BatchStatus.Text = "Finished"; BtnOpenFolder.Visibility = Visibility.Visible; }
    private void BtnStop_Click(object sender, RoutedEventArgs e) { _stopRequested = true; if (_currentProcess != null) _currentProcess.Kill(true); }
    private void ToggleUI(bool d) { _isDownloading = d; BtnDownload.Visibility = d ? Visibility.Collapsed : Visibility.Visible; BtnStop.Visibility = d ? Visibility.Visible : Visibility.Collapsed; }
    private void Log(string m) { if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => Log(m)); return; } LstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {m}"); LstLog.ScrollIntoView(LstLog.Items[LstLog.Items.Count - 1]); }
    private void ParseProgress(string l) { var m = Regex.Match(l, @"(\d+(\.\d+)?)%"); if (m.Success && double.TryParse(m.Groups[1].Value, out double v)) PrgBar.Value = v; }
    private void BtnClear_Click(object sender, RoutedEventArgs e) => UrlInput.Clear();
    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads"));
    private void BtnAdvancedToggle_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { AdvancedPanel.Visibility = AdvancedPanel.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed; BtnAdvancedToggle.Text = AdvancedPanel.Visibility == Visibility.Visible ? "▼ Advanced Options" : "▶ Advanced Options"; }
    private void LstLog_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => CopySelectedLog();
    private void MenuItemCopy_Click(object sender, RoutedEventArgs e) => CopySelectedLog();
    private void CopySelectedLog() { if (LstLog.SelectedItem != null) Clipboard.SetText(LstLog.SelectedItem.ToString() ?? ""); }
}