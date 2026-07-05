#!/bin/sh
# Optional GNOME Files (Nautilus) right-click script.
# Install: copy to ~/.local/share/nautilus/scripts/"Compress for Discord" and chmod +x.
# Appears under right-click → Scripts → Compress for Discord.
for f in "$@"; do
    # Flatpak install:
    if command -v flatpak >/dev/null 2>&1 && flatpak info io.github.lexikin.CompressForDiscord >/dev/null 2>&1; then
        flatpak run io.github.lexikin.CompressForDiscord "$f" &
    else
        CompressForDiscord "$f" &
    fi
done
