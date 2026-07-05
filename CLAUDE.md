# Compress for Discord — dev notes

Right-click media file → compress to just under Discord's upload limit → copy result file to
clipboard → preview window. C# / .NET 10 / Avalonia 11. Videos+animated GIFs → VP9/Opus WebM
(two-pass); static images → PNG (downscale search).

## Commands

```
dotnet build CompressForDiscord.sln
dotnet test                                   # unit + integration (integration auto-skips w/o ffmpeg)
dotnet test --filter Category!=Integration    # fast loop
dotnet run --project src/CompressForDiscord              # GUI (no-args) mode
dotnet run --project src/CompressForDiscord -- <file>    # compression flow
pwsh packaging/scripts/fetch-ffmpeg.ps1 -OutDir artifacts/ffmpeg
pwsh packaging/scripts/make-icons.ps1         # regenerate committed icon assets
```

## Hard constraints (do not "fix" these)

- **Avalonia stays on 11.3.x** — LibVLCSharp.Avalonia 3.10.0 requires ≥11.3.13 and does NOT
  support Avalonia 12. Pins live in `Directory.Packages.props`.
- **ffmpeg resolution order** (contract with all installers): `FFMPEG_PATH` env dir →
  `AppContext.BaseDirectory` (side-by-side) → `PATH`. See `FfmpegLocator`.
- **Size presets are MiB** (`AppSettings.BytesPerUnit`) — matches Discord's historical binary
  API values; verify empirically before changing.
- ffmpeg `-progress` quirk: `out_time_ms` is **microseconds**; `out_time_us` is preferred.
  PNG `-compression_level` is 0–9 (not 0–100).
- Two-pass VP9: the `-vf` chain must be byte-identical across passes; `-passlogfile` lives in
  the per-job temp dir (`%TEMP%/CompressForDiscord/<guid>`), never next to the source.
  Same-chain retries skip pass 1 (stats are bitrate-independent).
- `VideoView` (LibVLCSharp) is a NativeControlHost: **nothing can overlay it** (banner is its
  own grid row); attach `MediaPlayer` only after `Window.Opened`; detach + `Stop()`/`Dispose()`
  on a thread-pool thread (UI-thread stop deadlocks).
- App lifetime uses `ShutdownMode.OnExplicitShutdown`; `AppController` owns all window
  transitions and calls `desktop.Shutdown(exitCode)`. Open PreviewWindow BEFORE closing
  ProgressWindow.
- Windows clipboard is raw CF_HDROP P/Invoke (+ "Preferred DropEffect"=COPY) on purpose;
  Linux uses forked `wl-copy`/`xclip` with `text/uri-list` (X11 clipboards die with the owner).

## Exit codes

0 ok/skip · 1 unexpected · 2 bad args · 3 cancelled · 4 cannot-fit · 5 unsupported input · 6 no ffmpeg

## Layout

- `src/CompressForDiscord/Services/Planning/` — pure math (planner, PNG search, namer): unit-test here
- `src/CompressForDiscord/Services/` — process layer (locator/runner/prober), compressors, orchestrator
- `src/CompressForDiscord/Infrastructure/AppController.cs` — window/lifetime conductor
- `src/CompressForDiscord.Shell/` — Win11 IExplorerCommand C++ DLL (sparse MSIX)
- `packaging/` — MSI (WiX v6), sparse MSIX assets/scripts, AppImage, Flatpak, ffmpeg lockfile
- CLI flags: `<file>` compress; `--register-shell`/`--unregister-shell` (Win11 sparse package)

## Releases

Tag `v*` → `.github/workflows/release.yml` builds MSI / AppImage / .flatpak and attaches them
to a GitHub Release with SHA256SUMS. ffmpeg is downloaded+sha256-verified per
`packaging/ffmpeg/ffmpeg.lock.json` (BtbN builds; mirror at the `third-party-sources` release).
