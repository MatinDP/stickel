using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stickel.Core;

public enum MediaKind { Image, Video }
public enum LayoutMode { Fit, Fill, Stretch }
public enum ExportFormat { WebM, Gif, Mp4, Png, WebP }

public sealed class MediaAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourcePath { get; set; } = "";
    public string Name { get; set; } = "";
    public MediaKind Kind { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public double FrameRate { get; set; } = 30;
    public bool HasAudio { get; set; }
    public long Bytes { get; set; }
    [JsonIgnore] public string ThumbnailPath { get; set; } = "";
    [JsonIgnore] public string DisplayInfo => $"{Width} × {Height}" + (Kind == MediaKind.Video ? $"  ·  {Duration:0.###}s" : "");
}

public sealed class TimelineClip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssetId { get; set; } = "";
    public double InPoint { get; set; }
    public double OutPoint { get; set; }
    public LayoutMode Layout { get; set; } = LayoutMode.Fit;
    public double Zoom { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double CropX { get; set; }
    public double CropY { get; set; }
    public double CropWidth { get; set; } = 1;
    public double CropHeight { get; set; } = 1;
    [JsonIgnore] public double Duration => Math.Max(0, OutPoint - InPoint);
}

public sealed class WatermarkSettings
{
    public string SourcePath { get; set; } = "";
    public double WidthPercent { get; set; } = 20;
    public double Opacity { get; set; } = 0.8;
    public double CenterX { get; set; } = 0.8;
    public double CenterY { get; set; } = 0.85;
    public bool Enabled { get; set; } = true;
}

public sealed class ProjectDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled";
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public bool CanvasInitialized { get; set; }
    public List<MediaAsset> Assets { get; set; } = [];
    public List<TimelineClip> Clips { get; set; } = [];
    public WatermarkSettings Watermark { get; set; } = new();
    public ExportFormat Format { get; set; } = ExportFormat.WebM;
    public bool TelegramMode { get; set; } = true;
    public bool MuteOutput { get; set; } = true;
    public bool ExportIndividual { get; set; }
    public double Quality { get; set; } = 85;
    public string Background { get; set; } = "#16191F";
    public double Playhead { get; set; }
    [JsonIgnore] public double Duration => Clips.Sum(x => x.Duration);
    [JsonIgnore] public bool IsStatic => Format is ExportFormat.Png or ExportFormat.WebP;
    public MediaAsset Asset(TimelineClip clip) => Assets.First(x => x.Id == clip.AssetId);
    public ProjectDocument Clone() => JsonSerializer.Deserialize<ProjectDocument>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
}

public static class ProjectRules
{
    public static void Validate(ProjectDocument project, bool forExport = false)
    {
        if (project.SchemaVersion != 1) throw new InvalidDataException("Unsupported .stickel project version.");
        if (!Enum.IsDefined(project.Format) || project.Assets.Any(a => !Enum.IsDefined(a.Kind)) || project.Clips.Any(c => !Enum.IsDefined(c.Layout))) throw new InvalidDataException("Unknown project media or layout mode.");
        if (project.Width < 2 || project.Height < 2 || project.Width > 8192 || project.Height > 8192)
            throw new InvalidDataException("Canvas dimensions must be between 2 and 8192 pixels.");
        if (project.Assets.Select(a => a.Id).Distinct().Count() != project.Assets.Count)
            throw new InvalidDataException("Duplicate media identifiers.");
        if (project.Assets.Any(a => !Guid.TryParseExact(a.Id, "N", out _) || a.Width <= 0 || a.Height <= 0 || !double.IsFinite(a.Duration) || a.Duration < 0) ||
            project.Clips.Any(c => !Guid.TryParseExact(c.Id, "N", out _)) || project.Clips.Select(c => c.Id).Distinct().Count() != project.Clips.Count)
            throw new InvalidDataException("Invalid media or clip metadata.");
        if (!double.IsFinite(project.Quality) || project.Quality < 0 || project.Quality > 100 || !double.IsFinite(project.Playhead)) throw new InvalidDataException("Invalid project settings.");
        foreach (var clip in project.Clips)
        {
            var asset = project.Assets.Find(a => a.Id == clip.AssetId) ?? throw new InvalidDataException("Missing media reference.");
            if (!double.IsFinite(clip.InPoint) || !double.IsFinite(clip.OutPoint) || clip.InPoint < 0 || clip.Duration < 0.033 - 0.000001)
                throw new InvalidDataException("Each clip needs a valid duration of at least one frame.");
            if (asset.Kind == MediaKind.Video && clip.OutPoint > asset.Duration + 0.05)
                throw new InvalidDataException("Trim point exceeds the original media duration.");
            if (!double.IsFinite(clip.Zoom) || clip.Zoom < .1 || clip.Zoom > 8 ||
                !double.IsFinite(clip.OffsetX) || !double.IsFinite(clip.OffsetY) || Math.Abs(clip.OffsetX) > 4 || Math.Abs(clip.OffsetY) > 4 ||
                !double.IsFinite(clip.CropX) || !double.IsFinite(clip.CropY) || !double.IsFinite(clip.CropWidth) || !double.IsFinite(clip.CropHeight) ||
                clip.CropX < 0 || clip.CropY < 0 || clip.CropWidth < .01 || clip.CropHeight < .01 || clip.CropX + clip.CropWidth > 1.00001 || clip.CropY + clip.CropHeight > 1.00001)
                throw new InvalidDataException("Invalid crop or transform.");
        }
        var wm = project.Watermark;
        if (!double.IsFinite(wm.WidthPercent) || wm.WidthPercent < 1 || wm.WidthPercent > 100 ||
            !double.IsFinite(wm.Opacity) || wm.Opacity < 0 || wm.Opacity > 1 ||
            !double.IsFinite(wm.CenterX) || !double.IsFinite(wm.CenterY) || wm.CenterX < 0 || wm.CenterX > 1 || wm.CenterY < 0 || wm.CenterY > 1)
            throw new InvalidDataException("Invalid watermark settings.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(project.Background, "^#[0-9a-fA-F]{6}$"))
            throw new InvalidDataException("Background must be a six-digit hex color.");
        if (!forExport) return;
        if (project.Clips.Count == 0) throw new InvalidOperationException("The timeline is empty.");
        foreach (var asset in project.Assets.Where(a => project.Clips.Any(c => c.AssetId == a.Id)))
            if (!File.Exists(asset.SourcePath)) throw new FileNotFoundException("Source media was moved or deleted.", asset.SourcePath);
        if (wm.Enabled && wm.SourcePath.Length > 0 && !File.Exists(wm.SourcePath)) throw new FileNotFoundException("Watermark file was moved or deleted.", wm.SourcePath);
        if (project.TelegramMode)
        {
            if (Math.Max(project.Width, project.Height) != 512) throw new InvalidOperationException("Telegram requires one side to be exactly 512 pixels, with the other side at most 512.");
            if (!project.IsStatic && project.Duration > 3.000001) throw new InvalidOperationException("Telegram video cannot exceed 3 seconds. Trim the timeline or switch to Free export.");
            if (project.Format is ExportFormat.Mp4 or ExportFormat.Gif) throw new InvalidOperationException("Telegram sticker export requires WEBM/VP9, PNG or WEBP. Use Free export for GIF/MP4.");
        }
    }

    public static (TimelineClip Clip, double LocalTime) Locate(ProjectDocument project, double time)
    {
        if (project.Clips.Count == 0) throw new InvalidOperationException("The timeline is empty.");
        var start = 0d;
        foreach (var clip in project.Clips)
        {
            if (time < start + clip.Duration - .000001) return (clip, Math.Clamp(time - start, 0, Math.Max(0, clip.Duration - .001)));
            start += clip.Duration;
        }
        var last = project.Clips[^1];
        return (last, Math.Max(0, last.Duration - 1d / 30));
    }
}

public readonly record struct RenderGeometry(int CropX, int CropY, int CropWidth, int CropHeight, int Width, int Height, double X, double Y)
{
    public static RenderGeometry Calculate(ProjectDocument project, TimelineClip clip)
    {
        var a = project.Asset(clip);
        int cx = (int)Math.Round(clip.CropX * a.Width), cy = (int)Math.Round(clip.CropY * a.Height);
        int cw = Math.Clamp((int)Math.Round(clip.CropWidth * a.Width), 1, a.Width - cx);
        int ch = Math.Clamp((int)Math.Round(clip.CropHeight * a.Height), 1, a.Height - cy);
        var scale = clip.Layout == LayoutMode.Fill ? Math.Max((double)project.Width / cw, (double)project.Height / ch) : Math.Min((double)project.Width / cw, (double)project.Height / ch);
        int w = Math.Max(1, (int)Math.Round((clip.Layout == LayoutMode.Stretch ? project.Width : cw * scale) * clip.Zoom));
        int h = Math.Max(1, (int)Math.Round((clip.Layout == LayoutMode.Stretch ? project.Height : ch * scale) * clip.Zoom));
        return new(cx, cy, cw, ch, w, h, (project.Width - w) / 2d + clip.OffsetX * project.Width, (project.Height - h) / 2d + clip.OffsetY * project.Height);
    }
}
