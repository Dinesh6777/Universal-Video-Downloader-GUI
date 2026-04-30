# Universal Video Downloader

![License](https://img.shields.io/badge/license-GPLv3-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)

A modern, high-performance GUI tool for `yt-dlp` built with **C#** and **.NET 10 (WPF)** . This utility provides a seamless way to download high-quality videos or entire playlists from YouTube, Facebook, and hundreds of other supported platforms.

<img width="722" height="542" alt="image" src="https://github.com/user-attachments/assets/0670f6a5-ac26-4f9f-893e-85647eaf2795" />

---
## [Download - click here](https://github.com/Dinesh6777/Universal-Video-Downloader-GUI/raw/refs/heads/main/Downloads/Universal%20Video%20Downloader%20v1.1.exe)
---
## 🚀 Key Features

*   **Universal Platform Support**: Leverages the power of `yt-dlp` to download media from almost any video-sharing site.
*   **Intelligent Playlist Handling**: Automatically creates a dedicated subfolder for playlists and prefixes files with their index for perfect organization.
*   **Manual Control**: Features a dedicated "Stop Download" button that safely terminates the process tree, including active FFmpeg merges.
*   **Automatic Maintenance**: The app automatically fetches the latest `yt-dlp.exe` on startup and performs weekly update checks to keep site extractors current.
*   **Quality Presets**: Quickly toggle between 360p, 480p, 720p, and 1080p output.
*   **Verbose Logging**: Real-time terminal output is displayed within the GUI to help troubleshoot failed downloads or geo-restricted content.
*   **FFmpeg Detection**: Built-in system check to ensure FFmpeg is available for high-quality audio/video merging.

---

## 📖 How to Use

1.  **Paste URL**: Enter the video or playlist link into the URL input box.
2.  **Choose Quality**: Select your preferred resolution from the dropdown menu.
3.  **Download**: Click **Download Now**. The button will toggle to **Stop Download** while the process is running.
4.  **Manage Files**: Once finished, click **Open Downloads Folder** to access your media.

---
## 🛠️ Build and Installation

### Prerequisites
*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
*   [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) (Recommended for merging 1080p+ content).

### Generating a Portable App
To build a standalone, zero-dependency version that runs on any 64-bit Windows machine without requiring a .NET installation, run the following command in the project root:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./PortableApp
```

---

## ⚖️ License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.

### GPLv3 Summary:
*   **Permissions**: Commercial use, modification, and distribution are allowed.
*   **Conditions**: The source code must be made available under the same license when distributed.
*   **Limitations**: The software is provided with no warranty or liability.


---
## 🛡️ Disclaimer

This tool is a wrapper for `yt-dlp`. Users are responsible for ensuring their use of the software complies with the Terms of Service of the platforms they are downloading from and their local copyright laws.
