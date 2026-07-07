# Compress for Discord

Right-click any image or video → **Compress for Discord** → get a copy that fits just under
Discord's upload limit, already on your clipboard. Paste it into Discord with Ctrl+V. Done.

- **Videos & animated GIFs** → MP4 (H.264 + AAC, GPU-accelerated when available, sized to the limit)
- **Images** → PNG (downscaled just enough to fit)
- **10 MB** free-tier limit by default; 50 MB / 500 MB / custom presets for Nitro
- Windows (MSI, Explorer context menu incl. the Windows 11 menu) and Linux (Flatpak, AppImage)
- Built with C# / .NET 10 / Avalonia; media handled by a bundled [FFmpeg](https://ffmpeg.org)

> 🚧 Under construction — first release coming soon.

## Install

Coming with the first release: MSI (Windows), Flatpak & AppImage (Linux) on the
[Releases](https://github.com/lexikin/CompressForDiscord/releases) page.

## Building from source

```
dotnet build
dotnet test
dotnet run --project src/CompressForDiscord
```

Requirements: .NET SDK 10.0.1xx. For compression at dev time, ffmpeg/ffprobe (≥ 7.1) must be
resolvable — the app looks in `FFMPEG_PATH` (directory), then next to its own executable, then `PATH`.

## How it sizes output

For video the planner computes a total bit budget from the size limit and duration, splits off
audio (96k → 64k → 48k mono as budget tightens), picks a resolution/fps rung that keeps
bits-per-pixel sane, then encodes single-pass on the fastest H.264 encoder the machine has
(NVIDIA NVENC / Intel Quick Sync / AMD AMF, falling back to x264) and verifies the
real file size — retrying
with a tighter margin if the encoder overshoots. Images binary-search a downscale factor until
the PNG fits.

## License

MIT for this project's code — see [LICENSE](LICENSE).
Bundled/linked third-party components (FFmpeg — GPL builds, libvlc/LibVLCSharp — LGPL, Avalonia — MIT)
are documented in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

"Discord" is a trademark of Discord Inc. This project is an independent tool that prepares files
*for* Discord and is not affiliated with or endorsed by Discord Inc.
