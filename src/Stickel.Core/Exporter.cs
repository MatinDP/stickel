namespace Stickel.Core;

public sealed record ExportProgress(double Fraction, string Stage);
public sealed record ExportResult(IReadOnlyList<string> Files, long TotalBytes);

public sealed class Exporter(MediaTools tools)
{
    public async Task<ExportResult> Export(ProjectDocument project, string outputDirectory, IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
    {
        ProjectRules.Validate(project, true);
        if (project.Format == ExportFormat.Mp4 && (project.Width % 2 != 0 || project.Height % 2 != 0))
            throw new InvalidOperationException("MP4/H.264 requires even canvas dimensions. Choose an even width and height.");
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(outputDirectory)) throw new IOException("The output project directory must be new to avoid overwriting existing files.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputDirectory)!);
        var staging = Directory.CreateDirectory(outputDirectory + ".partial-" + Guid.NewGuid().ToString("N")).FullName;
        var session = Directory.CreateDirectory(Path.Combine(tools.CacheDirectory, "export-" + Guid.NewGuid().ToString("N"))).FullName;
        var files = new List<string>();
        try
        {
            if (project.IsStatic)
            {
                var (clip, localTime) = ProjectRules.Locate(project, Math.Clamp(project.Playhead, 0, project.Duration));
                var frame = await RenderClip(project, clip, localTime, 1d / 30, session, "frame", ct);
                files.Add(await Encode(project, frame, staging, "timeline", session, progress, ct));
                if (project.ExportIndividual)
                {
                    var parts = Directory.CreateDirectory(Path.Combine(staging, "parts")).FullName;
                    for (var i = 0; i < project.Clips.Count; i++)
                    {
                        var item = project.Clips[i];
                        var image = await RenderClip(project, item, 0, 1d / 30, session, $"frame-{i}", ct);
                        files.Add(await Encode(project, image, parts, $"{i + 1:000}-{SafeName(project.Asset(item).Name)}", session, progress, ct));
                    }
                }
            }
            else
            {
                var intermediate = new List<string>();
                for (var i = 0; i < project.Clips.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(new((double)i / project.Clips.Count * .45, $"Rendering {i + 1}/{project.Clips.Count}"));
                    intermediate.Add(await RenderClip(project, project.Clips[i], 0, project.Clips[i].Duration, session, $"clip-{i}", ct));
                }
                var listFile = Path.Combine(session, "concat.txt");
                await File.WriteAllLinesAsync(listFile, intermediate.Select(p => "file '" + p.Replace("\\", "/").Replace("'", "'\\''") + "'"), ct);
                var combined = Path.Combine(session, "timeline.mkv");
                await tools.Run(tools.Ffmpeg, ["-y", "-hide_banner", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listFile, "-c", "copy", combined], ct);
                files.Add(await Encode(project, combined, staging, "timeline", session, progress, ct));
                if (project.ExportIndividual)
                {
                    var parts = Directory.CreateDirectory(Path.Combine(staging, "parts")).FullName;
                    for (var i = 0; i < project.Clips.Count; i++)
                    {
                        progress?.Report(new(.65 + .3 * i / project.Clips.Count, $"Exporting part {i + 1}/{project.Clips.Count}"));
                        files.Add(await Encode(project, intermediate[i], parts, $"{i + 1:000}-{SafeName(project.Asset(project.Clips[i]).Name)}", session, progress, ct));
                    }
                }
            }
            ct.ThrowIfCancellationRequested();
            var totalBytes = files.Sum(p => new FileInfo(p).Length);
            var committed = files.Select(p => Path.Combine(outputDirectory, Path.GetRelativePath(staging, p))).ToArray();
            Directory.Move(staging, outputDirectory);
            progress?.Report(new(1, "Complete"));
            return new(committed, totalBytes);
        }
        finally
        {
            // Only delete the private, randomly generated export workspace.
            if (Directory.Exists(session)) Directory.Delete(session, true);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public async Task<string> PreviewFrame(ProjectDocument project, TimelineClip clip, double localTime, string destination, CancellationToken ct = default)
    {
        var session = Directory.CreateDirectory(Path.Combine(tools.CacheDirectory, "preview-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var rendered = await RenderClip(project, clip, localTime, 1d / 30, session, "preview", ct);
            await tools.Run(tools.Ffmpeg, ["-y", "-hide_banner", "-loglevel", "error", "-i", rendered, "-frames:v", "1", destination], ct);
            return destination;
        }
        finally { if (Directory.Exists(session)) Directory.Delete(session, true); }
    }

    public async Task<string> RenderClip(ProjectDocument project, TimelineClip clip, double localTime, double duration, string session, string name, CancellationToken ct)
    {
        var asset = project.Asset(clip);
        var input = asset.Kind == MediaKind.Image ? await tools.NormalizeImage(asset.SourcePath, ct) : await tools.PrepareMotion(asset, ct);
        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-filter_complex_threads", "1" };
        if (asset.Kind == MediaKind.Image) args.AddRange(["-loop", "1", "-framerate", "30"]);
        else args.AddRange(["-ss", MediaTools.N(clip.InPoint + localTime)]);
        args.AddRange(["-i", input]);
        var background = project.Format == ExportFormat.Mp4 ? project.Background.Replace("#", "0x") : "black@0.0";
        args.AddRange(["-f", "lavfi", "-i", $"color=c={background}:s={project.Width}x{project.Height}:r=30:d={MediaTools.N(duration)},format=rgba"]);
        var watermark = project.Watermark;
        var hasWatermark = watermark.Enabled && watermark.SourcePath.Length > 0;
        if (hasWatermark) args.AddRange(["-loop", "1", "-i", await tools.NormalizeImage(watermark.SourcePath, ct)]);
        var audioIndex = hasWatermark ? 3 : 2;
        args.AddRange(["-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=48000"]);
        var g = RenderGeometry.Calculate(project, clip);
        var filter = $"[0:v]setpts=PTS-STARTPTS,crop={g.CropWidth}:{g.CropHeight}:{g.CropX}:{g.CropY},scale={g.Width}:{g.Height}:flags=lanczos,setsar=1,fps=30,format=rgba[fg];[1:v][fg]overlay=x={MediaTools.N(g.X)}:y={MediaTools.N(g.Y)}:format=auto:shortest=1[base]";
        var final = "base";
        if (hasWatermark)
        {
            var ww = Math.Max(1, (int)Math.Round(project.Width * watermark.WidthPercent / 100));
            filter += $";[2:v]scale={ww}:-1:flags=lanczos,format=rgba,colorchannelmixer=aa={MediaTools.N(watermark.Opacity)}[wm];[base][wm]overlay=x={MediaTools.N(project.Width * watermark.CenterX)}-overlay_w/2:y={MediaTools.N(project.Height * watermark.CenterY)}-overlay_h/2:format=auto:shortest=1[v]";
            final = "v";
        }
        args.AddRange(["-filter_complex", filter, "-map", $"[{final}]", "-map", asset.HasAudio && asset.Kind == MediaKind.Video ? "0:a:0" : $"{audioIndex}:a:0",
            "-af", $"aresample=48000,apad,atrim=duration={MediaTools.N(duration)},asetpts=PTS-STARTPTS", "-t", MediaTools.N(duration),
            "-c:v", "ffv1", "-level", "3", "-pix_fmt", "bgra", "-threads", "2", "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2"]);
        var target = Path.Combine(session, name + ".mkv");
        args.Add(target);
        await tools.Run(tools.Ffmpeg, args, ct);
        return target;
    }

    private async Task<string> Encode(ProjectDocument project, string input, string outputDirectory, string name, string session, IProgress<ExportProgress>? progress, CancellationToken ct)
    {
        var extension = project.Format switch { ExportFormat.WebM => ".webm", ExportFormat.Gif => ".gif", ExportFormat.Mp4 => ".mp4", ExportFormat.Png => ".png", _ => ".webp" };
        var target = Path.Combine(outputDirectory, name + extension);
        var temp = Path.Combine(session, "encoded-" + Guid.NewGuid().ToString("N") + extension);
        if (project.TelegramMode && project.Format == ExportFormat.WebM)
        {
            int low = 0, high = 63;
            string? best = null;
            while (low <= high)
            {
                var crf = (low + high) / 2;
                progress?.Report(new(.55, $"Optimizing Telegram quality · CRF {crf}"));
                var candidate = Path.Combine(session, $"candidate-{Guid.NewGuid():N}.webm");
                await EncodeOnce(project, input, candidate, crf, ct);
                if (new FileInfo(candidate).Length <= 256 * 1024)
                {
                    if (best is not null) File.Delete(best);
                    best = candidate; high = crf - 1;
                }
                else { File.Delete(candidate); low = crf + 1; }
            }
            if (best is null) throw new InvalidOperationException("This animation cannot fit the Telegram size limit. Reduce motion or crop the content.");
            File.Move(best, temp);
        }
        else await EncodeOnce(project, input, temp, -1, ct);
        if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("The media engine produced no output.");
        File.Move(temp, target, true);
        return target;
    }

    private async Task EncodeOnce(ProjectDocument project, string input, string output, int crf, CancellationToken ct)
    {
        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-filter_complex_threads", "1", "-i", input };
        var silent = project.MuteOutput || project.TelegramMode || project.IsStatic || project.Format == ExportFormat.Gif;
        if (silent) args.Add("-an");
        else args.AddRange(project.Format == ExportFormat.WebM ? ["-c:a", "libopus", "-b:a", "96k"] : ["-c:a", "aac", "-b:a", "128k"]);
        var quality = Math.Clamp(project.Quality, 0, 100);
        switch (project.Format)
        {
            case ExportFormat.WebM:
                args.AddRange(["-c:v", "libvpx-vp9", "-pix_fmt", "yuva420p", "-b:v", "0", "-crf", (crf >= 0 ? crf : (int)Math.Round(55 - quality * .45)).ToString(), "-deadline", "good", "-cpu-used", "3", "-row-mt", "1", "-threads", "4"]);
                if (project.TelegramMode) args.AddRange(["-t", "3"]);
                break;
            case ExportFormat.Mp4:
                args.AddRange(["-c:v", "libx264", "-preset", "medium", "-crf", ((int)Math.Round(38 - quality * .23)).ToString(), "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-threads", "4"]);
                break;
            case ExportFormat.Gif:
                int fps = (int)Math.Round(10 + quality * .2), colors = (int)Math.Round(32 + quality * 2.24);
                args.AddRange(["-filter_complex", $"[0:v]fps={fps},split[a][b];[a]palettegen=max_colors={colors}:reserve_transparent=1[p];[b][p]paletteuse=dither=sierra2_4a:alpha_threshold=128", "-loop", "0"]);
                break;
            case ExportFormat.Png:
                args.AddRange(["-frames:v", "1", "-pix_fmt", "rgba", "-compression_level", "9"]);
                break;
            case ExportFormat.WebP:
                var imageQuality = project.TelegramMode ? 100 : quality;
                args.AddRange(["-frames:v", "1", "-c:v", "libwebp", "-quality", MediaTools.N(imageQuality), "-lossless", imageQuality >= 99 ? "1" : "0"]);
                break;
        }
        args.Add(output);
        await tools.Run(tools.Ffmpeg, args, ct);
    }

    private static string SafeName(string name)
    {
        var safe = string.Concat(Path.GetFileNameWithoutExtension(name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return safe.Length > 60 ? safe[..60] : safe;
    }
}
