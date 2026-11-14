# Stickel 1.0.1 — Validation report

Date: 2026-11-14. This report distinguishes executed checks from remaining coverage. Passing these cases does not prove compatibility with every media file or Windows machine.

## Build and automated checks

- Release solution build: successful, zero warnings and zero errors.
- Real media integration suite: **37 passing checks**. Latest recorded run: `artifacts/integration/20261114-025328/results.json`.
- View-model integration suite: **23 passing checks**. Executed after the final view-model behavior changes.
- Total: **60 automated checks passed**.
- Windows x64 self-contained publication and portable ZIP generated. The published application was launched and opened its bundled project without using the local SDK.
- Inno Setup per-user installer compiled. Installation coverage is recorded separately below.

## Real media checks

| Area | Executed evidence |
|---|---|
| Video imports | AVI, MKV, MOV, WEBM and MP4 with actual decoded thumbnails and metadata |
| Selected AVI playback regression | The supplied 7:18 Xvid/MP3 AVI plays directly in selected-clip preview; playback advanced beyond 10 seconds without preparing a full-length lossless proxy |
| Still imports | PNG, JPG, BMP, TIFF, WEBP and SVG |
| Animated images | Animated GIF, APNG and WebP correctly classified as motion; animated playback sources retained |
| Static frame policy | Combined PNG follows playhead into the second clip; individual PNG follows the first trimmed frame |
| Alpha | PNG empty Fit canvas transparent; VP9 decoded with libvpx retains transparent empty canvas |
| Crop and layout | Actual rendered crop/Fit pixels, Fill covering the canvas, and translated media pixels checked |
| Watermark | Rendered position, 50% opacity and width relative to output canvas checked using output pixels |
| WEBM/VP9 | VP9 video, no audio stream, duration within limit and file size at most 256 KiB |
| GIF / WebP | GIF re-imported as playable motion; static WebP dimensions checked |
| MP4 | Trimmed video and still sequence duration checked; audio retained with silence for still clips; mute output has no audio stream |
| Telegram rejection | Over-three-second timeline rejected before rendering; exactly three seconds accepted |
| Project archive | Embedded source bytes, watermark and timeline survive portable save/open independently of old paths |
| Input and archive errors | Damaged AVI rejected; archive path traversal rejected without writing outside private workspace |
| Cancellation | Cancelled export does not commit a partial final output directory |

## Editor-state checks

The 23 view-model checks cover media separation, one-second default still duration, initial canvas ratio, grouping a drag as one Undo step, Redo, square crop, per-clip transforms, reordering and its Undo, following the displayed clip during whole-timeline preview, restricting selected-clip seeking to its own frames, global watermark transparency, Telegram audio/quality state, manual Free settings, duration validity, export preflight, language/theme state, save/open and undoing media removal.

## Actual window inspection

The Windows app was inspected through Computer Use. Observed interactions included:

- Persian dark and English light layouts; fixed physical positions of settings, preview and media panes.
- Switching language and theme in the running application.
- Opening a portable `.stickel` project in the app.
- Previewing a still image and an actual video with the shared watermark.
- Dragging media and watermark with the mouse.
- Actual Ctrl+Z and Ctrl+Shift+Z after a preview drag.
- Trimming a still clip through a timeline handle and seeing its duration change.
- Drawing a free crop on video and observing the numeric crop fields and native video viewport update.
- A visible green indicator at three seconds and red indicator above three seconds.
- The actual over-duration export error before choosing an output folder or rendering.
- Playing the rendered selected-clip preview and the complete three-clip image/video timeline to the end.
- Playing the supplied long Xvid AVI in selected-clip mode while the displayed frame and playhead advanced to 10 seconds; the whole-timeline preview keeps its existing render-first behavior.

The window tests found and led to fixes for startup XAML bindings, dark control contrast, timeline sizing, native video overlay hit testing, and export validation before the folder dialog. These were corrections found during testing, not features assumed to work from compilation alone.

## Distribution coverage

The self-contained directory and installer are built from the same publication. Installer compilation does not by itself prove all install/uninstall paths. A clean-machine installation, file-association integration and uninstall are not yet tested. The installer is unsigned.

## Not exhaustively tested

- A separate Windows 10 computer, other GPUs/drivers, all DPI scales and multiple-monitor layouts.
- Every supported decoder/container combination, proprietary formats, DRM, malformed-file fuzzing or every HEIC/RAW delegate.
- Very large media/project archives, low-disk simulations and long-running stress tests.
- HDR, advanced color profiles, rotation metadata variants and professional color accuracy between the GPU preview and the software export pipeline.
- Frame-by-frame timing for every variable-frame-rate source.

No claim is made that these untested scenarios are fault-free. The tested flows and current automated suites passed as reported above.
