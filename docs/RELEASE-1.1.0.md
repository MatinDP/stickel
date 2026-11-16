# Stickel 1.1.0

## Changes since 1.0.0

- Simple black and white dark/light themes, matching native title bars on Windows 11, and consistent buttons, sliders and scrollbars.
- Font Awesome icons throughout the editor, shorter toolbar labels, and icon-only transport controls.
- Media resize and rotation handles, 15-degree rotation snapping with Shift, numeric rotation, and matching rotation in saved projects and rendered exports.
- Direct selected-video playback for supported sources, including AVI, without rendering a full-length preview first.
- Preview audio is independent of silent export and Telegram mode. The player has its own volume slider and mute button.
- Seeking with transport buttons, the seek bar, arrow keys or the timeline background preserves playback. Selected trimmed sources and rendered timelines use the correct time offset.
- Play/pause icons update with playback state; the center-media button is at the right of the player controls.
- Media lists retain item scrolling and move one item per mouse wheel notch.
- Build validation includes theme and real playback regression checks.

## Downloads

- `Stickel-Setup-1.1.0-x64.exe`: Windows 10/11 x64 installer for the current user.
- `Stickel-1.1.0-win-x64.zip`: portable Windows x64 package; extract the complete archive and run `Stickel.exe`.
- `checksums-1.1.0.sha256`: SHA-256 checksums of the installer and ZIP.

.NET and the media engines are included. The app and installer are unsigned. Clean-machine installation and uninstall, other GPUs and every media format have not been exhaustively tested. See `VALIDATION.md` for executed checks and remaining coverage.
