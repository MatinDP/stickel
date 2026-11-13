# Third-party components

Stickel uses the following independently licensed components. Their licenses remain in force; the license of a C# binding does not replace the license of its native dependencies.

| Component | Version | License / source |
|---|---|---|
| .NET Windows Desktop runtime | Published SDK runtime version | MIT and third-party notices; https://github.com/dotnet/runtime / https://github.com/dotnet/wpf |
| CommunityToolkit.Mvvm | 8.4.2 | MIT; https://github.com/CommunityToolkit/dotnet |
| Flyleaf | 3.11.18 | LGPL-3.0-or-later; https://github.com/SuRGeoNix/Flyleaf/tree/v3.11.18 |
| Flyleaf FFmpeg playback libraries | FFmpeg 9.0, commit 0056dd3, .NET patch | Original FFmpeg component licenses; https://github.com/SuRGeoNix/Flyleaf/releases/tag/v3.11.18 |
| FFmpeg / FFprobe export programs | 9.0.2 Gyan essentials build | GPL build, run as separate processes; included license and build README in `licenses/`; https://www.gyan.dev/ffmpeg/builds/ |
| FFmpeg source | 9.0.2 | https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz ; build configuration from `ffmpeg -buildconf` |
| Magick.NET Q8 x64 | 15.1.0 | Apache-2.0; https://github.com/dlemstra/Magick.NET ; ImageMagick and delegate notices also apply |
| Vortice bindings | Flyleaf transitive dependency versions in project.assets.json | MIT; https://github.com/amerkoleci/Vortice.Windows |
| Inno Setup (installer tool only) | 6.7.3 | https://github.com/jrsoftware/issrc ; not part of the installed application payload |

FFmpeg and Flyleaf are dynamically loaded or used as separate tools. Media engines are bundled; users are not expected to install system codec packs. No web, torrent, subtitle-download or YouTube plugins from the Flyleaf sample are packaged.

Before publicly distributing binaries, preserve the exact component notices, corresponding source/build configuration and the applicable redistribution obligations. This local build includes the FFmpeg export license and upstream build README; source pointers above identify the corresponding projects.
