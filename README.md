# Universal Video Downloader

![License](https://img.shields.io/badge/license-GPLv3-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)

A portable, modern and simplistic graphical interface for the command line tool [`yt-dlp`](https://github.com/yt-dlp/yt-dlp) built with **C#** and **.NET 10 (WPF)** .
This utility provides a seamless way to download high-quality videos or entire playlists from YouTube, Facebook, and hundreds of other supported platforms.

<img width="600" height="500" alt="image" src="https://github.com/user-attachments/assets/ca293dd1-7f3e-4970-8abe-a701356af1ad" />

---
## [Download - check releases here](https://github.com/Dinesh6777/Universal-Video-Downloader-GUI/releases)
---
## 🚀 Key Features

*   **Multi-platform support** - Download from YouTube, Vimeo, Facebook, Instagram, TikTok, and 1000+ sites. Leverages the power of `yt-dlp` to download media from almost any video-sharing site.
*   **Intelligent Playlist Handling**: Automatically creates a dedicated subfolder for playlists and prefixes files with their index for perfect organization.
*   **Manual Control**: Features a dedicated "Stop Download" button that safely terminates the process tree, including active FFmpeg merges.
*   **Batch Downloading**: Supports a multi-line input field where you can paste a list of URLs or even full playlist links.
*   **Intelligent Dependency Management**: The app automatically fetches the latest `yt-dlp.exe` on startup and performs weekly update checks to keep site extractors current.
*   **Quality Presets**: Quickly toggle between 360p, 480p, 720p, 1080p, 2K, 4K, 8K output.
*   **Verbose Logging**: Real-time terminal output is displayed within the GUI to help troubleshoot failed downloads or geo-restricted content.
*   **FFmpeg Detection**: Built-in system check to ensure FFmpeg is available for high-quality audio/video merging.
*   **Toggleable Advanced Options**: Includes a hidden panel to input custom parameters that are intelligently parsed into the downloader command.
*   **Automated FFmpeg Installer**: Detects missing requirements and handles the download and extraction of ffmpeg.exe directly to the app folder.
*   **Deno-Powered Cookie Support**: Automatically detects system architecture to install Deno, enabling advanced cookie extraction from Firefox and Chrome for age-restricted or private content.
*   **Dynamic Video Format Selection**: dedicated dropdown to force output into MP4, WebM, or high-quality MP3 audio-only modes, alongside a smart "Default" option.
*   **Intelligent Version Validator**: Performs a background check by parsing your local filename's version and cross-referencing it with the latest GitHub release tags.  

---

## 📖 How to Use

1.  **Paste URL**: Enter the video or playlist link into the URL input box.
2.  **Choose Quality**: Select your preferred resolution from the dropdown menu.
3.  **Download**: Click **Download Now**. The button will toggle to **Stop Download** while the process is running.
4.  **Manage Files**: Once finished, click **Open Downloads Folder** to access your media.

---
## 🛠️ Developer Build and Setup

### Prerequisites
*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
*   [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) (Recommended for merging 1080p+ content).

### Generating a Portable App
To build a standalone, zero-dependency version that runs on any 64-bit Windows machine without requiring a .NET installation, run the following command in the project root:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./PortableApp
```

---
## ❤️Donate
* Paypal: NA
* Bitcoin: bc1qdy0p2ecg6cqhrn3v7n0hepu7md2fedsegz3m2j
* Ethereum(ERC20): 0x6CB452DAb1C580Cc1395dF68f80e071E27102146
---

## ⚖️ License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.

### GPLv3 Summary:
*   **Permissions**: Commercial use, modification, and distribution are allowed.
*   **Conditions**: The source code must be made available under the same license when distributed.
*   **Limitations**: The software is provided with no warranty or liability.

## SEO keyword list:
youtube downloader, youtube video downloader, free youtube downloader, youtube downloader software, yt downloader, download youtube videos, yt-dlp gui, yt-dlp wrapper, youtube downloader gui, video downloader for pc, youtube to mp4 downloader, youtube to mp3 converter, open source youtube downloader, best youtube downloader 2026, fast youtube downloader, batch youtube download, playlist downloader youtube, 4k youtube downloader alternative, yt-dlp frontend, cross platform youtube downloader, windows youtube downloader, mac youtube downloader, linux youtube downloader, safe youtube downloader, no ads youtube downloader, command line youtube downloader, gui for yt-dlp, youtube downloader app, high quality youtube download, hd video downloader youtube, youtube subtitles downloader, youtube audio extractor, youtube playlist downloader software, youtube batch downloader

---
## 🛡️ Disclaimer

This tool is a wrapper for [`yt-dlp`](https://github.com/yt-dlp/yt-dlp). Users are responsible for ensuring their use of the software complies with the Terms of Service of the platforms they are downloading from and their local copyright laws.
