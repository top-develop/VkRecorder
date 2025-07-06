# 🎧 VkRecorder

**VkRecorder** is an automatic music recording tool for [vk.com](https://vk.com) that saves played songs directly as MP3 files with ID3 tags (artist, title, cover). The application runs locally on Windows and requires no account access or API key.

---

## ✨ Features

- Automatic detection of **artist**, **title**, **play time**, and **cover** directly from the open `vk.com` browser window
- Records system audio (Wasapi Loopback)
- Saves directly as **MP3** (no temporary WAV step)
- Automatic **file naming** using `Artist - Title.mp3`
- Duplicate protection: identical tracks are not saved multiple times
- Configurable storage location for tracks and logs
- Automatic deletion of old log files (older than 30 days)

---

## 🚀 Getting Started

### 1. Start the application

A Chrome window will open automatically when you run the program.

> Manually navigate to [https://vk.com](https://vk.com)  
> and select the **„Музыка“** (Music) menu item.

Detection and recording will start automatically.

---

### 2. Configuration

You can change the storage path for MP3s and logs in `appsettings.json`:

```json
{
  "Paths": {
    "TracksDirectory": "%USERPROFILE%\\Documents\\VkTracks",
    "LogFilePath": "%USERPROFILE%\\Documents\\VkLogs"
  }
}
```

Use environment variables like `%USERPROFILE%` for flexible storage locations.

---

### 3. Requirements

- Windows 10/11
- .NET 8 SDK
- Google Chrome (for metadata detection)
- VK account (free)

---

## 🧠 How It Works

1. **Metadata Detection**: A Chromium browser reads the DOM content of the music page in real time.
2. **Track Start Detection**: When play time reaches `0:00`, a ring buffer starts.
3. **Recording Ends**: When time stops changing, the ring buffer is saved to an MP3 file.
4. **MP3 Tags**: Artist, title, and cover are automatically embedded.

---

## 📂 Project Structure

- `VkMetadataFetcher`: Controls the headless browser and extracts metadata
- `RingBufferRecorder`: Continuously stores system audio in a ring buffer
- `Mp3Metadata`: Writes ID3v2 tags including cover
- `ConfigHelper`: Reads the `appsettings.json`
- `Program.cs`: Main logic controlling recording, timing, and segmentation

---

## 📦 Output

Recorded MP3 files are saved in the configured directory, for example:

```
C:\Users\<Name>\Documents\VkTracks\Titel - Artist.mp3
```

---

## 📃 License

MIT License
