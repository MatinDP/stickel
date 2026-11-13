using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ImageMagick;

namespace Stickel.Core;

public sealed class MediaTools(string toolsDirectory, string cacheDirectory)
{
    public string ToolsDirectory { get; } = toolsDirectory;
    public string CacheDirectory { get; } = Directory.CreateDirectory(cacheDirectory).FullName;
    public string Ffmpeg => Path.Combine(ToolsDirectory, "ffmpeg.exe");
    public string Ffprobe => Path.Combine(ToolsDirectory, "ffprobe.exe");
    public static string N(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    public async Task<string> Run(string executable, IEnumerable<string> arguments, CancellationToken ct = default, Action<string>? progress = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new IOException("Could not start media engine.");
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        var outputTask = Task.Run(async () =>
        {
            var output = new System.Text.StringBuilder();
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                if (progress is not null) progress(line); else output.AppendLine(line);
            }
            return output.ToString();
        }, ct);
        try
        {
            await process.WaitForExitAsync(ct);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0) throw new IOException($"Media engine failed ({process.ExitCode}).\n{error[^Math.Min(error.Length, 6000)..]}");
            return output;
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(outputTask, errorTask); } catch { }
            throw;
        }
    }

    public async Task<MediaAsset> Import(string path, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Media file does not exist.", fullPath);
        var asset = new MediaAsset { SourcePath = fullPath, Name = Path.GetFileName(fullPath), Bytes = new FileInfo(fullPath).Length };
        Exception? probeError = null;
        try
        {
            var json = await Run(Ffprobe, ["-v", "error", "-show_streams", "-show_format", "-of", "json", fullPath], ct);
            using var data = JsonDocument.Parse(json);
            var streams = data.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            var video = streams.FirstOrDefault(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "video" && (!s.TryGetProperty("disposition", out var disp) || !disp.TryGetProperty("attached_pic", out var attached) || attached.GetInt32() == 0));
            if (video.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("No image/video stream found.");
            asset.Width = video.GetProperty("width").GetInt32(); asset.Height = video.GetProperty("height").GetInt32();
            var rotation = 0d;
            if (video.TryGetProperty("side_data_list", out var sides)) foreach (var side in sides.EnumerateArray()) if (side.TryGetProperty("rotation", out var rot)) rotation = rot.GetDouble();
            if (Math.Abs(rotation % 180) > 45 && Math.Abs(rotation % 180) < 135) (asset.Width, asset.Height) = (asset.Height, asset.Width);
            asset.FrameRate = video.TryGetProperty("avg_frame_rate", out var rate) ? Fraction(rate.GetString()) : 30;
            if (asset.FrameRate <= 0) asset.FrameRate = 30;
            asset.Duration = Number(video, "duration");
            if (asset.Duration <= 0 && data.RootElement.TryGetProperty("format", out var fmt)) asset.Duration = Number(fmt, "duration");
            asset.HasAudio = streams.Any(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio");
            var formatName = data.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("format_name", out var fn) ? fn.GetString() ?? "" : "";
            if (asset.Duration <= 0 && !formatName.Contains("image2") && !formatName.EndsWith("_pipe"))
            {
                var maximumEnd = 0d;
                await Run(Ffprobe, ["-v", "error", "-select_streams", "v:0", "-show_packets", "-show_entries", "packet=pts_time,duration_time", "-of", "csv=p=0", fullPath], ct, line =>
                {
                    var fields = line.Split(',');
                    if (fields.Length > 0 && double.TryParse(fields[0], CultureInfo.InvariantCulture, out var pts) && double.IsFinite(pts))
                    {
                        var packetDuration = fields.Length > 1 && double.TryParse(fields[1], CultureInfo.InvariantCulture, out var pd) && double.IsFinite(pd) ? pd : 1 / asset.FrameRate;
                        maximumEnd = Math.Max(maximumEnd, pts + packetDuration);
                    }
                });
                asset.Duration = maximumEnd;
            }
            asset.Kind = asset.Duration > 0 && !formatName.Contains("image2") && !formatName.EndsWith("_pipe") ? MediaKind.Video : MediaKind.Image;
            if (video.TryGetProperty("nb_frames", out var nf) && nf.GetString() == "1") asset.Kind = MediaKind.Image;
            if (asset.Width <= 0 || asset.Height <= 0) throw new InvalidDataException("Invalid image dimensions.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { probeError = ex; }
        if (probeError is not null || asset.Kind == MediaKind.Image)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var images = new MagickImageCollection();
                    images.Read(fullPath);
                    var image = images[0]; image.AutoOrient();
                    asset.Width = (int)image.Width; asset.Height = (int)image.Height;
                    if (images.Count > 1 && images.Any(x => x.AnimationDelay > 0))
                    {
                        asset.Kind = MediaKind.Video;
                        asset.Duration = images.Sum(x => (double)x.AnimationDelay / Math.Max(1, x.AnimationTicksPerSecond));
                        asset.FrameRate = Math.Clamp(images.Count / Math.Max(.001, asset.Duration), 1, 60);
                    }
                    else { asset.Kind = MediaKind.Image; asset.Duration = 0; }
                }, ct);
                probeError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (probeError is not null) throw new InvalidDataException($"Unsupported or damaged media: {asset.Name}\n{probeError.Message}\n{ex.Message}", ex);
            }
        }
        await MakeThumbnail(asset, ct);
        return asset;
    }

    public async Task<string> NormalizeImage(string source, CancellationToken ct = default)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source + new FileInfo(source).LastWriteTimeUtc.Ticks)))[..24];
        var target = Path.Combine(CacheDirectory, key + ".png");
        if (!File.Exists(target)) await Task.Run(() =>
        {
            using var image = new MagickImage(source);
            image.AutoOrient(); image.ColorSpace = ColorSpace.sRGB; image.Format = MagickFormat.Png32; image.Write(target);
        }, ct);
        return target;
    }

    public async Task MakeThumbnail(MediaAsset asset, CancellationToken ct = default)
    {
        var playbackPath = await PrepareMotion(asset, ct);
        var thumb = Path.Combine(CacheDirectory, asset.Id + "-thumb.png");
        if (!File.Exists(thumb))
        {
            if (asset.Kind == MediaKind.Image)
            {
                await Task.Run(() => { using var image = new MagickImage(asset.SourcePath); image.AutoOrient(); image.Thumbnail(240, 160); image.Format = MagickFormat.Png32; image.Write(thumb); }, ct);
            }
            else await Run(Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-i", playbackPath, "-frames:v", "1", "-vf", "scale=240:160:force_original_aspect_ratio=decrease", thumb], ct);
        }
        asset.ThumbnailPath = thumb;
    }

    public string PlaybackSource(MediaAsset asset)
    {
        var cached = Path.Combine(CacheDirectory, asset.Id + "-motion.mkv");
        return File.Exists(cached) ? cached : asset.SourcePath;
    }

    public async Task<string> PrepareMotion(MediaAsset asset, CancellationToken ct = default)
    {
        if (asset.Kind != MediaKind.Video || !new[] { ".gif", ".webp" }.Contains(Path.GetExtension(asset.SourcePath).ToLowerInvariant())) return asset.SourcePath;
        var target = Path.Combine(CacheDirectory, asset.Id + "-motion.mkv");
        if (File.Exists(target)) return target;
        var folder = Directory.CreateDirectory(Path.Combine(CacheDirectory, "animation-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var list = new List<string>();
            await Task.Run(() =>
            {
                using var images = new MagickImageCollection(asset.SourcePath); images.Coalesce();
                for (int i = 0; i < images.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = images[i]; var framePath = Path.Combine(folder, $"{i:000000}.png");
                    frame.Format = MagickFormat.Png32; frame.Write(framePath);
                    list.Add("file '" + framePath.Replace("\\", "/").Replace("'", "'\\''") + "'");
                    list.Add("option framerate 1000");
                    list.Add("duration " + N(Math.Max(1d / 1000, (double)frame.AnimationDelay / Math.Max(1, frame.AnimationTicksPerSecond))));
                }
                if (images.Count > 0) { list.Add("file '" + Path.Combine(folder, $"{images.Count - 1:000000}.png").Replace("\\", "/").Replace("'", "'\\''") + "'"); list.Add("option framerate 1000"); }
            }, ct);
            var listPath = Path.Combine(folder, "frames.txt"); await File.WriteAllLinesAsync(listPath, list, ct);
            var partial = target + ".tmp.mkv";
            await Run(Ffmpeg, ["-y", "-hide_banner", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listPath, "-vf", "fps=30", "-t", N(asset.Duration), "-c:v", "ffv1", "-pix_fmt", "bgra", "-threads", "2", partial], ct);
            File.Move(partial, target, true);
            return target;
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    public static double Number(JsonElement element, string key) => element.TryGetProperty(key, out var value) && double.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var result) && double.IsFinite(result) ? result : 0;
    public static double Fraction(string? value)
    {
        var parts = (value ?? "0").Split('/');
        if (!double.TryParse(parts[0], CultureInfo.InvariantCulture, out var a)) return 0;
        return parts.Length == 1 ? a : double.TryParse(parts[1], CultureInfo.InvariantCulture, out var b) && b != 0 ? a / b : 0;
    }
}
