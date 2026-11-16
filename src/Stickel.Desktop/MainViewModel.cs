using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stickel.Core;

namespace Stickel.Desktop;

public sealed class MainViewModel : ObservableObject
{
    public ProjectDocument Project { get; private set; } = new();
    public MediaTools Tools { get; }
    public Exporter Exporter { get; }
    public ObservableCollection<MediaAsset> Videos { get; } = [];
    public ObservableCollection<MediaAsset> Images { get; } = [];
    private readonly Stack<ProjectDocument> undo = new(), redo = new();
    private ProjectDocument? gestureStart;
    private string? lastChangeKey;
    private DateTime lastChange;
    private string? selectedId;
    public string? ProjectPath { get; private set; }
    public bool Dirty { get; private set; }
    private bool wholePreview;
    public bool WholePreview
    {
        get => wholePreview;
        set { if (SetProperty(ref wholePreview, value)) { OnPropertyChanged(nameof(SeekMin)); OnPropertyChanged(nameof(SeekMax)); OnPropertyChanged(nameof(TimeText)); } }
    }
    public string Language { get; private set; } = "fa";
    public bool LightTheme { get; private set; }
    public FlowDirection UiFlow => Language == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public TimelineClip? SelectedClip => Project.Clips.FirstOrDefault(x => x.Id == selectedId);
    public MediaAsset? SelectedAsset => SelectedClip is { } c ? Project.Asset(c) : null;
    public bool HasSelection => SelectedClip is not null;
    public string SelectedName => SelectedAsset?.Name ?? Localization.T("NoSelection");
    public string Title => $"{Project.Name}{(Dirty ? " •" : "")} — Stickel";
    public double TotalDuration => Project.Duration;
    public double SeekMin => !WholePreview && SelectedClip is { } clip ? StartOf(clip) : 0;
    public double SeekMax => !WholePreview && SelectedClip is { } clip ? StartOf(clip) + clip.Duration : TotalDuration;
    public double Playhead { get => Project.Playhead; set { var lastFrame = !WholePreview && SelectedClip is not null ? Math.Max(SeekMin, SeekMax - 1d / 30) : SeekMax; Project.Playhead = Math.Clamp(value, SeekMin, Math.Max(SeekMin, lastFrame)); OnPropertyChanged(); OnPropertyChanged(nameof(TimeText)); PositionChanged?.Invoke(); } }
    public string TimeText => $"{TimeSpan.FromSeconds(Math.Max(0, Playhead - SeekMin)):mm\\:ss\\.ff} / {TimeSpan.FromSeconds(Math.Max(0, SeekMax - SeekMin)):mm\\:ss\\.ff}";
    public string DurationText => $"{TotalDuration:0.###} s";
    public bool ValidDuration => Project.IsStatic || TotalDuration <= 3.000001;
    public string Estimate => Project.IsStatic ? "~ 20–800 KB" : Project.TelegramMode && Project.Format == ExportFormat.WebM ? "≤ 256 KB · auto" : $"~ {Math.Max(.01, TotalDuration * Project.Width * Project.Height * (Project.Format == ExportFormat.Gif ? .000002 : .00000025) * (.4 + Project.Quality / 100)):0.0} MB";
    public bool ManualQuality => !Project.TelegramMode && Project.Format != ExportFormat.Png;
    public bool ManualAudio => !Project.TelegramMode && Project.Format is ExportFormat.WebM or ExportFormat.Mp4;
    public bool IsStatic => Project.IsStatic;
    public int FormatIndex { get => (int)Project.Format; set => Change(() => Project.Format = (ExportFormat)Math.Clamp(value, 0, 4)); }
    public int ModeIndex { get => Project.TelegramMode ? 0 : 1; set => Change(() => Project.TelegramMode = value == 0); }
    public bool MuteOutput { get => Project.TelegramMode || Project.MuteOutput || Project.IsStatic || Project.Format == ExportFormat.Gif; set { if (ManualAudio) Change(() => Project.MuteOutput = value); } }
    public bool Individual { get => Project.ExportIndividual; set => Change(() => Project.ExportIndividual = value); }
    public double Quality { get => Project.Quality; set => Change(() => Project.Quality = Math.Clamp(value, 0, 100), "quality"); }
    public int CanvasWidth { get => Project.Width; set { if (value >= 2 && value <= 8192) Change(() => Project.Width = value, "canvas"); } }
    public int CanvasHeight { get => Project.Height; set { if (value >= 2 && value <= 8192) Change(() => Project.Height = value, "canvas"); } }
    public string Background { get => Project.Background; set { if (System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$")) Change(() => Project.Background = value!, "background"); } }
    public int LayoutIndex { get => (int)(SelectedClip?.Layout ?? LayoutMode.Fit); set { if (SelectedClip is { } c) Change(() => c.Layout = (LayoutMode)Math.Clamp(value, 0, 2)); } }
    public double Zoom { get => (SelectedClip?.Zoom ?? 1) * 100; set { if (SelectedClip is { } c) Change(() => c.Zoom = Math.Clamp(value / 100, .1, 8), "zoom"); } }
    public double Rotation { get => SelectedClip?.Rotation ?? 0; set { if (SelectedClip is { } c && double.IsFinite(value)) Change(() => c.Rotation = Math.Round(((value % 360) + 540) % 360 - 180), "rotation"); } }
    public double CropX { get => (SelectedClip?.CropX ?? 0) * 100; set => SetCrop(value / 100, null, null, null); }
    public double CropY { get => (SelectedClip?.CropY ?? 0) * 100; set => SetCrop(null, value / 100, null, null); }
    public double CropWidth { get => (SelectedClip?.CropWidth ?? 1) * 100; set => SetCrop(null, null, value / 100, null); }
    public double CropHeight { get => (SelectedClip?.CropHeight ?? 1) * 100; set => SetCrop(null, null, null, value / 100); }
    public bool WatermarkEnabled { get => Project.Watermark.Enabled; set => Change(() => Project.Watermark.Enabled = value); }
    public double Transparency { get => (1 - Project.Watermark.Opacity) * 100; set => Change(() => Project.Watermark.Opacity = 1 - Math.Clamp(value, 0, 100) / 100, "opacity"); }
    public double WatermarkSize { get => Project.Watermark.WidthPercent; set => Change(() => Project.Watermark.WidthPercent = Math.Clamp(value, 1, 100), "watermark-size"); }
    public string WatermarkName => Path.GetFileName(Project.Watermark.SourcePath);
    private bool busy;
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !IsBusy;
    private string status = "";
    public string Status { get => status; private set => SetProperty(ref status, value); }
    private double progress;
    public double Progress { get => progress; private set => SetProperty(ref progress, value); }
    public CancellationTokenSource? Operation { get; private set; }
    public string LastOutput { get; private set; } = "";
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public event Action? Changed;
    public event Action? PositionChanged;

    public MainViewModel()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stickel", "cache", Guid.NewGuid().ToString("N"));
        Tools = new(Path.Combine(AppContext.BaseDirectory, "runtime", "export"), cache);
        Exporter = new(Tools);
        UndoCommand = new RelayCommand(Undo, () => undo.Count > 0 && !IsBusy);
        RedoCommand = new RelayCommand(Redo, () => redo.Count > 0 && !IsBusy);
        LoadPreferences(); ApplyPreferences(); Status = Localization.T("Ready");
    }
    public void Select(string? id, bool seek = true)
    {
        selectedId = id;
        if (seek && SelectedClip is { } clip) Project.Playhead = StartOf(clip);
        Refresh(); PositionChanged?.Invoke();
    }
    public double StartOf(TimelineClip clip) => Project.Clips.TakeWhile(c => c.Id != clip.Id).Sum(c => c.Duration);
    public bool FollowPreviewSelection()
    {
        if (!WholePreview || Project.Clips.Count == 0) return false;
        var current = ProjectRules.Locate(Project, Project.Playhead).Clip;
        if (current.Id == selectedId) return false;
        selectedId = current.Id;
        foreach (var name in new[] { "SelectedClip", "SelectedAsset", "HasSelection", "SelectedName", "LayoutIndex", "Zoom", "Rotation", "CropX", "CropY", "CropWidth", "CropHeight" }) OnPropertyChanged(name);
        return true;
    }
    public void Change(Action action, string? key = null)
    {
        if (IsBusy) return;
        var before = Project.Clone(); action();
        if (JsonSerializer.Serialize(before) == JsonSerializer.Serialize(Project)) return;
        if (gestureStart is null && !(key is not null && key == lastChangeKey && (DateTime.UtcNow - lastChange).TotalMilliseconds < 650))
        { undo.Push(before); redo.Clear(); }
        lastChangeKey = key; lastChange = DateTime.UtcNow; Dirty = true;
        Project.Playhead = Math.Clamp(Project.Playhead, 0, Project.Duration);
        Refresh();
    }
    public void BeginGesture() { if (!IsBusy && gestureStart is null) gestureStart = Project.Clone(); }
    public void EndGesture()
    {
        if (gestureStart is null) return;
        if (JsonSerializer.Serialize(gestureStart) != JsonSerializer.Serialize(Project)) { undo.Push(gestureStart); redo.Clear(); Dirty = true; }
        gestureStart = null; lastChangeKey = null; Refresh();
    }
    private void Undo() { if (undo.Count == 0 || IsBusy) return; redo.Push(Project.Clone()); Restore(undo.Pop()); }
    private void Redo() { if (redo.Count == 0 || IsBusy) return; undo.Push(Project.Clone()); Restore(redo.Pop()); }
    private void Restore(ProjectDocument state) { Project = state; if (SelectedClip is null) selectedId = Project.Clips.FirstOrDefault()?.Id; Project.Playhead = Math.Clamp(Project.Playhead, SeekMin, Math.Max(SeekMin, SeekMax)); Dirty = true; AttachThumbnails(); RebuildLists(); Refresh(); PositionChanged?.Invoke(); }
    public void New()
    {
        Project = new(); selectedId = null; ProjectPath = null; Dirty = false; undo.Clear(); redo.Clear(); RebuildLists(); Refresh();
    }
    public async Task Import(IEnumerable<string> files)
    {
        await Work("Importing", async ct =>
        {
            var added = new List<MediaAsset>(); var errors = new List<string>();
            var paths = files.ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                Progress = i * 100d / Math.Max(1, paths.Length);
                try { added.Add(await Tools.Import(paths[i], ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add(Path.GetFileName(paths[i]) + ": " + ex.Message); }
            }
            if (added.Count > 0)
            {
                undo.Push(Project.Clone()); redo.Clear(); Project.Assets.AddRange(added); Dirty = true; RebuildLists(); Refresh();
            }
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n\n", errors));
        });
    }
    public void AddClip(MediaAsset asset)
    {
        string? newId = null;
        Change(() =>
        {
            if (!Project.CanvasInitialized)
            {
                double scale = 512d / Math.Max(asset.Width, asset.Height);
                Project.Width = Math.Max(2, (int)Math.Round(asset.Width * scale / 2) * 2);
                Project.Height = Math.Max(2, (int)Math.Round(asset.Height * scale / 2) * 2);
                Project.CanvasInitialized = true;
            }
            var clip = new TimelineClip { AssetId = asset.Id, OutPoint = asset.Kind == MediaKind.Image ? 1 : asset.Duration };
            Project.Clips.Add(clip); newId = clip.Id;
        });
        Select(newId);
    }
    public void RemoveClip() { if (SelectedClip is { } c) { Change(() => Project.Clips.Remove(c)); Select(Project.Clips.FirstOrDefault()?.Id); } }
    public void MoveClip(int delta) { if (SelectedClip is { } c) Reorder(c.Id, Math.Clamp(Project.Clips.IndexOf(c) + delta, 0, Project.Clips.Count - 1)); }
    public void Reorder(string id, int index)
    {
        var c = Project.Clips.FirstOrDefault(x => x.Id == id); if (c is null) return;
        Change(() => { Project.Clips.Remove(c); Project.Clips.Insert(Math.Clamp(index, 0, Project.Clips.Count), c); }); Select(id);
    }
    public void RemoveAsset(MediaAsset asset)
    {
        Change(() => { Project.Clips.RemoveAll(c => c.AssetId == asset.Id); Project.Assets.Remove(asset); }); RebuildLists(); Refresh();
    }
    public void Trim(double input, double output)
    {
        if (SelectedClip is not { } c || SelectedAsset is not { } a) return;
        if (!double.IsFinite(input) || !double.IsFinite(output) || input < 0 || output - input < 1d / 30 || a.Kind == MediaKind.Video && output > a.Duration + .001) throw new InvalidOperationException(Localization.T("SourceLimit"));
        Change(() => { c.InPoint = a.Kind == MediaKind.Image ? 0 : input; c.OutPoint = a.Kind == MediaKind.Image ? output : output; });
    }
    public void SetCrop(double? x, double? y, double? w, double? h)
    {
        if (SelectedClip is not { } c) return;
        Change(() => { c.CropX = Math.Clamp(x ?? c.CropX, 0, .99); c.CropY = Math.Clamp(y ?? c.CropY, 0, .99); c.CropWidth = Math.Clamp(w ?? c.CropWidth, .01, 1 - c.CropX); c.CropHeight = Math.Clamp(h ?? c.CropHeight, .01, 1 - c.CropY); }, "crop");
    }
    public void CropPreset(double ratio)
    {
        if (SelectedClip is not { } c || SelectedAsset is not { } a) return;
        if (ratio <= 0) { SetCrop(0, 0, 1, 1); return; }
        double w = 1, h = 1;
        if ((double)a.Width / a.Height > ratio) w = a.Height * ratio / a.Width; else h = a.Width / ratio / a.Height;
        SetCrop((1 - w) / 2, (1 - h) / 2, w, h);
    }
    public void SetPosition(double x, double y) { if (SelectedClip is { } c) Change(() => { c.OffsetX = Math.Clamp(x, -4, 4); c.OffsetY = Math.Clamp(y, -4, 4); }, "position"); }
    public void SetWatermarkPosition(double x, double y) => Change(() => { Project.Watermark.CenterX = Math.Clamp(x, 0, 1); Project.Watermark.CenterY = Math.Clamp(y, 0, 1); }, "wm-position");
    public void SetWatermark(string path) => Change(() => { Project.Watermark.SourcePath = path; Project.Watermark.Enabled = true; });
    public async Task Save(string path)
    {
        var snapshot = Project.Clone(); snapshot.Name = Path.GetFileNameWithoutExtension(path);
        await Work("Saving", async ct => { await Task.Run(() => ProjectArchive.Save(snapshot, path, ct), ct); ProjectPath = path; Project.Name = Path.GetFileNameWithoutExtension(path); Dirty = false; Status = Localization.T("Saved"); Refresh(); });
    }
    public async Task Open(string path)
    {
        await Work("Opening", async ct =>
        {
            var root = Path.Combine(Tools.CacheDirectory, "project-" + Guid.NewGuid().ToString("N"));
            var loaded = await Task.Run(() => ProjectArchive.Load(path, root, ct), ct);
            foreach (var asset in loaded.Assets) await Tools.MakeThumbnail(asset, ct);
            Project = loaded; ProjectPath = path; Dirty = false; undo.Clear(); redo.Clear(); selectedId = Project.Clips.FirstOrDefault()?.Id; RebuildLists(); Refresh(); PositionChanged?.Invoke();
        });
    }
    public void ValidateExport()
    {
        if (Project.TelegramMode && !Project.IsStatic && Project.Duration > 3.000001) throw new InvalidOperationException(Localization.T("TelegramError"));
        if (Project.TelegramMode && Math.Max(Project.Width, Project.Height) != 512) throw new InvalidOperationException(Localization.T("SizeError"));
        if (Project.TelegramMode && Project.Format is ExportFormat.Gif or ExportFormat.Mp4) throw new InvalidOperationException(Localization.T("FormatError"));
        ProjectRules.Validate(Project, true);
    }
    public async Task Export(string directory)
    {
        ValidateExport();
        var snapshot = Project.Clone();
        await Work("Exporting", async ct =>
        {
            var target = Path.Combine(directory, "Stickel-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..4]);
            var result = await Exporter.Export(snapshot, target, new Progress<ExportProgress>(p => { Progress = p.Fraction * 100; Status = Localization.T("Exporting") + " · " + p.Stage; }), ct);
            LastOutput = target; Status = $"{Localization.T("Complete")} · {result.Files.Count} · {result.TotalBytes / 1024d:0} KB";
        });
    }
    public async Task<string?> PreparePreview(bool whole)
    {
        string? output = null;
        var snapshot = Project.Clone();
        if (!whole) snapshot.Clips = SelectedClip is { } selected ? [snapshot.Clips.First(c => c.Id == selected.Id)] : [];
        if (snapshot.Clips.Count == 0) return null;
        await Work("Preparing", async ct =>
        {
            var json = JsonSerializer.Serialize(new { snapshot.Width, snapshot.Height, snapshot.Clips, snapshot.Watermark, snapshot.Format, Sources = snapshot.Assets.Select(a => new { a.Id, a.SourcePath }) });
            var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))[..24];
            output = Path.Combine(Tools.CacheDirectory, "playback-" + key + ".mkv");
            if (File.Exists(output)) return;
            var session = Directory.CreateDirectory(Path.Combine(Tools.CacheDirectory, "build-preview-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var clips = new List<string>();
                for (int i = 0; i < snapshot.Clips.Count; i++) { Progress = i * 100d / snapshot.Clips.Count; clips.Add(await Exporter.RenderClip(snapshot, snapshot.Clips[i], 0, snapshot.Clips[i].Duration, session, "preview-" + i, ct)); }
                var list = Path.Combine(session, "list.txt");
                await File.WriteAllLinesAsync(list, clips.Select(p => "file '" + p.Replace("\\", "/").Replace("'", "'\\''") + "'"), ct);
                var temporary = output + ".tmp.mkv";
                await Tools.Run(Tools.Ffmpeg, ["-y", "-hide_banner", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", list, "-c", "copy", temporary], ct);
                File.Move(temporary, output, true);
            }
            finally { if (Directory.Exists(session)) Directory.Delete(session, true); }
        });
        return output;
    }
    private async Task Work(string key, Func<CancellationToken, Task> work)
    {
        if (IsBusy) throw new InvalidOperationException(Localization.T("Busy"));
        IsBusy = true; Progress = 0; Status = Localization.T(key); Operation = new(); RefreshCommands();
        try { await work(Operation.Token); Progress = 100; if (Status == Localization.T(key)) Status = Localization.T("Ready"); }
        catch (OperationCanceledException) { Status = Localization.T("Cancelled"); throw; }
        finally { Operation.Dispose(); Operation = null; IsBusy = false; RefreshCommands(); }
    }
    public void SetStatus(string text) => Status = text;
    public void SetClock(double time) { Project.Playhead = Math.Clamp(time, 0, Project.Duration); OnPropertyChanged(nameof(Playhead)); OnPropertyChanged(nameof(TimeText)); }
    public void Refresh()
    {
        foreach (var name in new[] { "Project", "SelectedClip", "SelectedAsset", "HasSelection", "SelectedName", "Title", "TotalDuration", "SeekMin", "SeekMax", "Playhead", "TimeText", "DurationText", "ValidDuration", "Estimate", "ManualQuality", "ManualAudio", "IsStatic", "FormatIndex", "ModeIndex", "MuteOutput", "Individual", "Quality", "CanvasWidth", "CanvasHeight", "Background", "LayoutIndex", "Zoom", "Rotation", "CropX", "CropY", "CropWidth", "CropHeight", "WatermarkEnabled", "Transparency", "WatermarkSize", "WatermarkName" }) OnPropertyChanged(name);
        RefreshCommands(); Changed?.Invoke();
    }
    private void RefreshCommands() { UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged(); }
    private void AttachThumbnails() { foreach (var a in Project.Assets) a.ThumbnailPath = Path.Combine(Tools.CacheDirectory, a.Id + "-thumb.png"); }
    private void RebuildLists() { Videos.Clear(); Images.Clear(); foreach (var asset in Project.Assets) (asset.Kind == MediaKind.Video ? Videos : Images).Add(asset); }
    public void ApplyPreferences()
    {
        Localization.Apply(Language, LightTheme); OnPropertyChanged(nameof(UiFlow));
        try { File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(new { Language, LightTheme })); } catch (IOException) { }
        Refresh();
    }
    public void SetLanguage(string language) { Language = language; ApplyPreferences(); }
    public void ToggleTheme() { LightTheme = !LightTheme; ApplyPreferences(); }
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stickel", "preferences.json");
    private void LoadPreferences()
    {
        try { if (File.Exists(PreferencesPath)) { using var json = JsonDocument.Parse(File.ReadAllText(PreferencesPath)); Language = json.RootElement.GetProperty("Language").GetString() == "en" ? "en" : "fa"; LightTheme = json.RootElement.GetProperty("LightTheme").GetBoolean(); } } catch { }
    }
}
