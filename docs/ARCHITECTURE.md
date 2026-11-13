# Stickel architecture

## Domain and persistence

`Stickel.Core` owns versioned project metadata, source assets, independent timeline clip trims and transforms, global watermark and output settings. Source assets and clip instances have different identities, allowing repeated use of one file. The editor never rewrites source media.

`.stickel` is an atomic, versioned ZIP archive. `project.json` stores the model; `media/` embeds all original media and the watermark. Loading uses a private workspace, validates paths and manifest size, rejects duplicate entries, checks available disk space and enforces entry lengths. Saving writes a temporary sibling file before replacing the destination.

## Media pipeline

FFprobe discovers streams and metadata, falling back to packet timing where container duration is absent. Magick.NET broadens still-image import, applies orientation and creates lossless PNG working files. Animated WebP/GIF can be normalized to lossless FFV1 so animation is not silently reduced to a still frame. The originals remain in the project archive.

`RenderGeometry` computes crop, fitted/scaled content and output-relative position. FFmpeg renders each segment into uniform, alpha-preserving FFV1/BGRA with normalized audio. These segments form the combined sequence. Final encoding supports VP9/WebM, GIF, H.264/MP4, PNG and WebP. Static output uses the confirmed frame policy.

Telegram mode validates dimensions and duration before rendering, always removes audio, and searches CRF quality settings for the highest practical quality fitting 256 KiB. A failed or cancelled operation does not commit the staging export directory. Free mode exposes manual quality and audio options.

## Desktop

WPF physical columns remain left=settings, center=preview, right=media, bottom=timeline in both languages. Resource-driven labels and semantic brushes switch language and theme. `CommunityToolkit.Mvvm` supplies observable state and commands. UI-specific mouse/focus/drag and Flyleaf surface work lives in the view; media, export, persistence and model validation live outside it.

Flyleaf supplies the actual FFmpeg/Direct3D player, seeking and native video surface. Paused editing maps the model into the preview renderer and WPF watermark overlay. Playback uses a cached, rendered sequence to avoid gaps between source changes. These proxies reuse the export compositor; editing invalidates the corresponding cache signature.

Undo/redo keeps metadata snapshots, never decoded image buffers or copies of source media. Continuous gestures are grouped into one history entry. Operations use cancellation tokens, asynchronous stream handling and explicit error reporting.

## Build and distribution

The repository pins SDK and package versions. Build tools are local to `.tools`; native dependencies are local to `runtime`. The self-contained Windows x64 publication includes its .NET runtime. An Inno Setup per-user installer offers optional shortcuts and `.stickel` file association.
