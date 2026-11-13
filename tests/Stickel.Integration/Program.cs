using System.IO.Compression;
using System.Text.Json;
using ImageMagick;
using Stickel.Core;

var root = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/integration");
Directory.CreateDirectory(root);
var run = Directory.CreateDirectory(Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss"))).FullName;
var runtime = Path.GetFullPath("runtime/export");
var tools = new MediaTools(runtime, Path.Combine(run, "cache"));
var exporter = new Exporter(tools);
var passed = new List<string>();
void Assert(bool condition, string message) { if (!condition) throw new Exception("ASSERTION FAILED: " + message); passed.Add(message); Console.WriteLine("PASS " + message); }
async Task F(params string[] options) => await tools.Run(tools.Ffmpeg, new[] { "-y", "-hide_banner", "-loglevel", "error" }.Concat(options));
async Task<JsonDocument> Probe(string path) => JsonDocument.Parse(await tools.Run(tools.Ffprobe, ["-v", "error", "-show_streams", "-show_format", "-of", "json", path]));

var red = Path.Combine(run, "قرمز اصلی.png");
var blue = Path.Combine(run, "blue.png");
var wm = Path.Combine(run, "watermark.png");
using (var image = new MagickImage(MagickColors.Red, 160, 100)) image.Write(red);
using (var image = new MagickImage(MagickColors.Blue, 100, 160)) image.Write(blue);
using (var image = new MagickImage(MagickColors.Lime, 60, 30)) image.Write(wm);
var video = Path.Combine(run, "movie with sound.mp4");
await F("-f", "lavfi", "-i", "testsrc2=s=320x180:r=30:d=1", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", video);
foreach (var ext in new[] { "avi", "mkv", "mov", "webm" })
{
    var file = Path.Combine(run, "input." + ext);
    if (ext == "webm") await F("-i", video, "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "35", "-an", file);
    else if (ext == "avi") await F("-i", video, "-c:v", "mpeg4", "-c:a", "pcm_s16le", file);
    else await F("-i", video, "-c", "copy", file);
    var imported = await tools.Import(file);
    Assert(imported.Kind == MediaKind.Video && imported.Width == 320 && imported.Duration > .9, "Import " + ext.ToUpperInvariant());
}
foreach (var ext in new[] { "jpg", "bmp", "tiff", "webp", "svg" })
{
    var file = Path.Combine(run, "input." + ext);
    if (ext == "svg") await File.WriteAllTextAsync(file, "<svg xmlns='http://www.w3.org/2000/svg' width='160' height='100'><rect width='160' height='100' fill='red'/></svg>");
    else { using var image = new MagickImage(red); image.Write(file); }
    var imported = await tools.Import(file);
    Assert(imported.Kind == MediaKind.Image && imported.Width == 160, "Import " + ext.ToUpperInvariant());
}
var ra = await tools.Import(red); var ba = await tools.Import(blue); var va = await tools.Import(video);
Assert(va.HasAudio, "Probe original audio stream");
var project = new ProjectDocument { Width = 512, Height = 320, CanvasInitialized = true, Format = ExportFormat.Png, TelegramMode = true, ExportIndividual = true, Playhead = .75 };
project.Assets.AddRange([ra, ba]); project.Clips.AddRange([new() { AssetId = ra.Id, OutPoint = .5 }, new() { AssetId = ba.Id, OutPoint = .5 }]);
var result = await exporter.Export(project, Path.Combine(run, "static"));
Assert(result.Files.Count == 3, "Combined static frame plus separate files");
using (var image = new MagickImage(result.Files[0]))
{
    var color = image.GetPixels().GetPixel(256, 160).ToColor()!;
    Assert(color.B > 240 && color.R < 10, "Combined PNG uses playhead frame in second clip");
    var corner = image.GetPixels().GetPixel(0, 0).ToColor()!;
    Assert(corner.A == 0, "Fit preserves transparent empty canvas");
}
using (var image = new MagickImage(result.Files[1])) { var c = image.GetPixels().GetPixel(256, 160).ToColor()!; Assert(c.R > 240 && c.B < 10, "Separate PNG uses first frame of first clip"); }
project.ExportIndividual = false; project.Watermark = new() { SourcePath = wm, WidthPercent = 20, CenterX = .5, CenterY = .5, Opacity = .5 };
project.Clips[1].OffsetX = .05; project.Clips[1].Zoom = .9;
var marked = await exporter.Export(project, Path.Combine(run, "watermarked"));
using (var image = new MagickImage(marked.Files[0])) { var c = image.GetPixels().GetPixel(256, 160).ToColor()!; Assert(c.G > 110 && c.G < 150 && c.B > 110, "Watermark position, relative scale and 50% opacity are rendered"); }
using (var image = new MagickImage(marked.Files[0])) { var inside = image.GetPixels().GetPixel(300, 160).ToColor()!; var outside = image.GetPixels().GetPixel(320, 160).ToColor()!; Assert(inside.G > 100 && outside.G < 10, "Watermark width is 20 percent of output canvas width"); }
project.Format = ExportFormat.WebM;
var webm = await exporter.Export(project, Path.Combine(run, "telegram"));
using (var meta = await Probe(webm.Files[0]))
{
    var streams = meta.RootElement.GetProperty("streams").EnumerateArray().ToArray();
    Assert(streams.Length == 1 && streams[0].GetProperty("codec_name").GetString() == "vp9", "Telegram output uses VP9 with no audio stream");
    Assert(new FileInfo(webm.Files[0]).Length <= 256 * 1024, "Telegram output fits 256 KB");
    Assert(MediaTools.Number(meta.RootElement.GetProperty("format"), "duration") <= 3.00001, "Telegram output duration is valid");
}
var alphaFrame = Path.Combine(run, "webm-alpha.png");
await F("-c:v", "libvpx-vp9", "-i", webm.Files[0], "-ss", "0.75", "-frames:v", "1", alphaFrame);
using (var image = new MagickImage(alphaFrame)) Assert(image.GetPixels().GetPixel(0, 0).ToColor()!.A == 0, "VP9 preserves transparent empty canvas after decoding");
project.TelegramMode = false; project.Format = ExportFormat.Gif;
var gif = await exporter.Export(project, Path.Combine(run, "gif"));
var gifAsset = await tools.Import(gif.Files[0]); Assert(gifAsset.Kind == MediaKind.Video && gifAsset.Duration > .9, "Animated GIF export is playable and classified as motion");
foreach (var extension in new[] { "webp", "apng" })
{
    var animation = Path.Combine(run, "animated." + extension);
    if (extension == "webp") { using var frames = new MagickImageCollection(gif.Files[0]); frames.Write(animation); }
    else await F("-i", gif.Files[0], "-plays", "0", "-f", "apng", animation);
    var animatedAsset = await tools.Import(animation);
    Assert(animatedAsset.Kind == MediaKind.Video && animatedAsset.Duration > .8 && File.Exists(tools.PlaybackSource(animatedAsset)), "Animated " + extension.ToUpperInvariant() + " import with motion-preserving playback source");
}
project.Format = ExportFormat.WebP;
var webp = await exporter.Export(project, Path.Combine(run, "webp"));
using (var image = new MagickImage(webp.Files[0])) Assert(image.Width == 512 && image.Height == 320, "WEBP static export dimensions");
var geometryProject = project.Clone(); geometryProject.Watermark.Enabled = false; geometryProject.Format = ExportFormat.Png; geometryProject.Playhead = 0;
geometryProject.Clips = [new() { AssetId = ba.Id, OutPoint = 1, CropY = .25, CropHeight = .5 }];
var cropResult = await exporter.Export(geometryProject, Path.Combine(run, "crop"));
using (var image = new MagickImage(cropResult.Files[0])) { Assert(image.GetPixels().GetPixel(256, 160).ToColor()!.B > 240 && image.GetPixels().GetPixel(0, 160).ToColor()!.A == 0, "Source crop and Fit compose correctly in actual output"); }
geometryProject.Clips[0].Layout = LayoutMode.Fill;
var fillResult = await exporter.Export(geometryProject, Path.Combine(run, "fill"));
using (var image = new MagickImage(fillResult.Files[0])) Assert(image.GetPixels().GetPixel(0, 0).ToColor()!.A == 255, "Fill covers the complete output canvas");
geometryProject.Clips[0].Layout = LayoutMode.Fit; geometryProject.Clips[0].OffsetX = .25;
var movedResult = await exporter.Export(geometryProject, Path.Combine(run, "moved"));
using (var image = new MagickImage(movedResult.Files[0])) Assert(image.GetPixels().GetPixel(100, 160).ToColor()!.A == 0 && image.GetPixels().GetPixel(400, 160).ToColor()!.B > 240, "Output-relative media positioning matches expected pixels");
project.Format = ExportFormat.Mp4; project.MuteOutput = false; project.Background = "#FFFFFF"; project.Assets.Add(va); project.Clips = [new() { AssetId = va.Id, InPoint = .2, OutPoint = .7 }, new() { AssetId = ra.Id, OutPoint = .5 }];
var mp4 = await exporter.Export(project, Path.Combine(run, "mp4"));
using (var meta = await Probe(mp4.Files[0])) { var streams = meta.RootElement.GetProperty("streams").EnumerateArray().ToArray(); Assert(streams.Any(s => s.GetProperty("codec_type").GetString() == "audio"), "MP4 retains audio and supplies silence for still clips"); Assert(Math.Abs(MediaTools.Number(meta.RootElement.GetProperty("format"), "duration") - 1) < .08, "Trimmed video and image concatenate to expected duration"); }
project.MuteOutput = true;
var silentMp4 = await exporter.Export(project, Path.Combine(run, "silent-mp4"));
using (var meta = await Probe(silentMp4.Files[0])) Assert(meta.RootElement.GetProperty("streams").EnumerateArray().All(s => s.GetProperty("codec_type").GetString() != "audio"), "Silent MP4 output contains no audio stream");
var damaged = Path.Combine(run, "damaged.avi"); await File.WriteAllTextAsync(damaged, "not a real AVI file");
bool rejected = false; try { await tools.Import(damaged); } catch (InvalidDataException) { rejected = true; }
Assert(rejected, "Damaged media is rejected with a clear import failure");
project.TelegramMode = true; project.Format = ExportFormat.WebM; project.Clips = [new() { AssetId = ra.Id, OutPoint = 3.1 }];
bool blocked = false; try { await exporter.Export(project, Path.Combine(run, "must-not-exist")); } catch (InvalidOperationException) { blocked = true; }
Assert(blocked && !Directory.Exists(Path.Combine(run, "must-not-exist")), "Over-3-second Telegram export is rejected before rendering");
project.Clips[0].OutPoint = 3; ProjectRules.Validate(project, true); Assert(true, "Exactly 3 seconds is accepted");
var archive = Path.Combine(run, "پروژه کامل.stickel"); await ProjectArchive.Save(project, archive);
var loaded = await ProjectArchive.Load(archive, Path.Combine(run, "opened"));
Assert(loaded.Assets.Count == 3 && loaded.Clips.Count == 1 && File.Exists(loaded.Watermark.SourcePath), "Portable .stickel preserves media, watermark and timeline");
Assert(loaded.Assets.All(a => a.SourcePath.StartsWith(Path.Combine(run, "opened"))) && File.ReadAllBytes(loaded.Assets[0].SourcePath).SequenceEqual(File.ReadAllBytes(red)), "Portable archive restores original bytes independently of old paths");
var malicious = Path.Combine(run, "bad.stickel");
using (var zip = ZipFile.Open(malicious, ZipArchiveMode.Create))
{
    var bad = project.Clone(); bad.Assets[0].SourcePath = "media/../../escape.png";
    using (var writer = new StreamWriter(zip.CreateEntry("project.json").Open())) writer.Write(JsonSerializer.Serialize(bad, ProjectDocument.JsonOptions));
}
bool safe = false; try { await ProjectArchive.Load(malicious, Path.Combine(run, "rejected")); } catch (InvalidDataException) { safe = true; }
Assert(safe && !File.Exists(Path.Combine(run, "escape.png")), "Malicious archive path cannot escape workspace");
project.TelegramMode = false; project.Clips[0].OutPoint = 20;
using (var cancel = new CancellationTokenSource(100))
{
    bool cancelled = false; try { await exporter.Export(project, Path.Combine(run, "cancelled"), ct: cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Assert(cancelled && !Directory.Exists(Path.Combine(run, "cancelled")), "Cancelled export never commits a partial final output");
}
await File.WriteAllTextAsync(Path.Combine(run, "results.json"), JsonSerializer.Serialize(new { Passed = passed.Count, Checks = passed, Run = run }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"COMPLETE: {passed.Count} checks. Artifacts: {run}");
