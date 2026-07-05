# Manual E2E checklist

Run before tagging a release. Platforms: **Windows 10**, **Windows 11 VM**, **Ubuntu (X11)**,
**Ubuntu (Wayland)**.

## Core compression flow

- [ ] Large mp4 (>50 MB) → right-click → Compress for Discord → progress advances (pass 1 → pass 2, ETA shown) → preview opens → **Ctrl+V into Discord uploads the webm**
- [ ] 4K 60 fps clip → output is downscaled + capped at 30 fps, still under limit
- [ ] Animated GIF → webm; plays looping & muted in preview
- [ ] Static GIF → png
- [ ] 20 MP photo (jpg) → png under limit, still sharp enough
- [ ] Screen recording with no audio track → compresses without error
- [ ] Already-small .webm (VP9) → "Already fits" skip; original goes to clipboard
- [ ] Small .mp4 → still converted (wrong container, no skip)
- [ ] 2-hour video at 10 MB → friendly "can't fit" dialog naming the max duration + preset suggestion

## Cancel & errors

- [ ] Cancel during pass 1 → app exits fast, Task Manager/`ps` shows **no orphan ffmpeg**, `%TEMP%/CompressForDiscord` has no leftover job dir
- [ ] Cancel during pass 2 → same
- [ ] Progress window ✕ behaves as Cancel
- [ ] Corrupt file renamed to .mp4 → "Couldn't read this file" with stderr details expandable
- [ ] Audio-only .mp3 → unsupported-input dialog

## Paths & files

- [ ] `Ünïcode video (final) #2.mp4` → compresses; clipboard paste works; output name keeps the unicode
- [ ] File in a path with spaces → works
- [ ] Read-only folder / UNC share input → output lands in Downloads, preview shows amber note
- [ ] Existing `x.discord.webm` → new file gets ` (2)` suffix

## Clipboard

- [ ] Windows: paste into Discord uploads; paste into Explorer copies the file (never moves)
- [ ] Windows: clipboard still pastes after the app is closed
- [ ] Linux X11: paste into Discord (with xclip installed)
- [ ] Linux Wayland: paste into Discord (with wl-clipboard installed)
- [ ] Linux without both tools → amber banner suggests installing them; text URI on clipboard
- [ ] "Copy again" re-copies and restarts the banner countdown

## Preview window

- [ ] Video: muted looping playback (VLC path)
- [ ] Linux without VLC: thumbnail + "Open in player" works
- [ ] Banner countdown bar shrinks toward ✕ over ~5 s, then banner dismisses (window stays)
- [ ] ✕ on banner dismisses immediately
- [ ] "Show in folder" highlights the output file
- [ ] Window sized sensibly for portrait video and huge images

## Settings & shell integration

- [ ] Preset change (10 → 50 MB) persists across launches; drag-drop respects it
- [ ] Win10: context menu entry appears top-level for mp4/mkv/mov/avi/webm/m4v/gif/png/jpg/jpeg/webp/bmp
- [ ] Win11: modern (top-level) menu entry appears after install; "Show more options" fallback also present
- [ ] Explorer multi-select of 3 files → 3 jobs run (one window each)
- [ ] Linux: app appears in "Open With" for videos/images
- [ ] No-args launch → drop zone works end-to-end

## Size-limit truth check

- [ ] Upload a file of exactly 10.3 *decimal* MB (≈9.82 MiB) to free Discord: if it uploads, the
      MiB constant is right; if Discord rejects >10^7 bytes, flip `AppSettings.BytesPerUnit`
      to 1,000,000 (one line) and re-verify

## Install lifecycle (Windows)

- [ ] MSI installs without errors; context menus live immediately
- [ ] Upgrade install (higher version) keeps working; no duplicate menu entries
- [ ] Uninstall removes menu entries, sparse package (Win11) and the TrustedPeople cert
