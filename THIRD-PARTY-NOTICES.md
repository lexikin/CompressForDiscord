# Third-party notices

Compress for Discord's own code is MIT-licensed (see [LICENSE](LICENSE)). It distributes or
depends on the following third-party components:

## FFmpeg (bundled binaries)

- Bundled `ffmpeg`/`ffprobe` binaries come from the [BtbN FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)
  project ("gpl" variants), licensed under the **GNU GPL v3** (see the `FFMPEG-LICENSE.txt`
  installed alongside the binaries).
- The binaries are invoked as separate processes (mere aggregation); this application's own code
  remains MIT.
- **Corresponding source:** the exact build tag, download URLs and checksums are pinned in
  [`packaging/ffmpeg/ffmpeg.lock.json`](packaging/ffmpeg/ffmpeg.lock.json). The matching FFmpeg
  source tarball and the binary archives are mirrored on this repository's
  [`third-party-sources` release](https://github.com/lexikin/CompressForDiscord/releases/tag/third-party-sources).
  Upstream source: <https://ffmpeg.org/download.html>.

## libvlc / VLC (video preview)

- Windows builds bundle libvlc via the `VideoLAN.LibVLC.Windows` NuGet package; the Flatpak
  builds libvlc from VLC source. **LGPL v2.1 or later.** <https://www.videolan.org/legal.html>
- On AppImage/portable Linux, libvlc is an optional system dependency (the app degrades to a
  thumbnail preview without it).

## LibVLCSharp

- .NET bindings for libvlc. **LGPL v2.1.** <https://github.com/videolan/libvlcsharp>

## Avalonia

- Cross-platform UI framework. **MIT.** <https://github.com/AvaloniaUI/Avalonia>

## CommunityToolkit.Mvvm

- MVVM source generators. **MIT.** <https://github.com/CommunityToolkit/dotnet>

## Inter font

- Bundled via `Avalonia.Fonts.Inter`. **SIL Open Font License 1.1.** <https://rsms.me/inter/>

---

"Discord" is a trademark of Discord Inc. This project is not affiliated with or endorsed by
Discord Inc.; the name describes the tool's purpose (preparing files *for* Discord).
