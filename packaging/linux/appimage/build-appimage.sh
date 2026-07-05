#!/usr/bin/env bash
# Builds the AppImage from a self-contained linux-x64 publish (ffmpeg already copied in).
# Usage: VERSION=0.1.0 PUBLISH_DIR=artifacts/publish/linux-x64 OUT_DIR=artifacts ./build-appimage.sh
set -euo pipefail

VERSION="${VERSION:?set VERSION}"
PUBLISH_DIR="${PUBLISH_DIR:-artifacts/publish/linux-x64}"
OUT_DIR="${OUT_DIR:-artifacts}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# Pinned appimagetool (AppImageKit release 13). PIN the sha256 before first release —
# the script refuses to run unpinned unless APPIMAGETOOL_SHA256 is provided.
APPIMAGETOOL_URL="https://github.com/AppImage/AppImageKit/releases/download/13/appimagetool-x86_64.AppImage"
APPIMAGETOOL_SHA256="${APPIMAGETOOL_SHA256:-TODO-PIN}"

APPDIR="$OUT_DIR/AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/icons/hicolor/256x256/apps" "$OUT_DIR"

cp -a "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/CompressForDiscord" "$APPDIR/usr/bin/ffmpeg" "$APPDIR/usr/bin/ffprobe"

install -m755 "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/compressfordiscord.desktop" "$APPDIR/"
cp "$REPO_ROOT/assets/icons/hicolor/256x256/apps/io.github.lexikin.CompressForDiscord.png" \
   "$APPDIR/compressfordiscord.png"
cp "$APPDIR/compressfordiscord.png" "$APPDIR/.DirIcon"
cp "$REPO_ROOT/assets/icons/hicolor/256x256/apps/io.github.lexikin.CompressForDiscord.png" \
   "$APPDIR/usr/share/icons/hicolor/256x256/apps/compressfordiscord.png"
cp "$REPO_ROOT/THIRD-PARTY-NOTICES.md" "$APPDIR/usr/bin/"

# Fetch + verify appimagetool.
TOOL="$OUT_DIR/appimagetool-x86_64.AppImage"
if [ ! -f "$TOOL" ]; then
  curl -fsSL -o "$TOOL" "$APPIMAGETOOL_URL"
fi
if [ "$APPIMAGETOOL_SHA256" = "TODO-PIN" ]; then
  echo "ERROR: APPIMAGETOOL_SHA256 is not pinned." >&2
  exit 1
fi
echo "$APPIMAGETOOL_SHA256  $TOOL" | sha256sum -c -
chmod +x "$TOOL"

# --appimage-extract-and-run: works without FUSE on CI runners.
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" \
  "$OUT_DIR/CompressForDiscord-$VERSION-x86_64.AppImage"

echo "Built $OUT_DIR/CompressForDiscord-$VERSION-x86_64.AppImage"
