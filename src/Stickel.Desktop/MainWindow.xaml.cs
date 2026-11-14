using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using Microsoft.Win32;
using Stickel.Core;
using Path = System.IO.Path;
using KeyBinding = System.Windows.Input.KeyBinding;

namespace Stickel.Desktop;

public partial class MainWindow : Window
{
    public MainViewModel VM { get; }
    private readonly Player player;
    private readonly Config config;
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly SemaphoreSlim mediaGate = new(1, 1);
    private readonly string? initialProject;
    private bool ready, clockUpdate, playing, proxyActive, closingApproved, cropMode;
    private double proxyStart;
    private string sourcePath = "", watermarkPath = "";
    private int sourceVersion;
    private BitmapSource? sourceBitmap, watermarkBitmap;
    private Point dragStart;
    private double initialX, initialY;
    private string? dragKind;
    private Rect watermarkBounds;
    private string? timelineDragId, timelineEdge;
    private double timelineDragX, timelineIn, timelineOut;
    private Border? timelineCaptured;
    private Line? playheadLine;
    private double PixelsPerSecond => TimelineZoom?.Value ?? 100;

    public MainWindow(string? initialProject)
    {
        this.initialProject = initialProject;
        VM = new();
        config = new Config(); config.Player.AutoPlay = false; config.Player.SeekAccurate = true;
        config.Decoder.VideoThreads = Math.Min(Environment.ProcessorCount, 4);
        player = new Player(config);
        InitializeComponent(); DataContext = VM; VideoHost.Player = player;
        LanguagePicker.SelectedIndex = VM.Language == "fa" ? 0 : 1;
        VM.Changed += EditorChanged; VM.PositionChanged += PositionChanged;
        Overlay.InputBindings.Add(new KeyBinding(VM.UndoCommand, Key.Z, ModifierKeys.Control));
        Overlay.InputBindings.Add(new KeyBinding(VM.RedoCommand, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
        Overlay.PreviewKeyDown += Window_KeyDown;
        Overlay.LostMouseCapture += (_, _) => { if (dragKind is not null) { dragKind = null; cropMode = false; CropRectangle.Visibility = Visibility.Collapsed; VM.EndGesture(); } };
        clock.Tick += Clock_Tick;
        PreviewKeyDown += Window_KeyDown;
        ready = true; UpdateThemeButton(); EditorChanged();
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialProject is not null) await Guard(() => VM.Open(initialProject));
    }
    private void UpdateThemeButton() => ThemeButton.Content = Localization.T(VM.LightTheme ? "Dark" : "Light");
    private async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { VM.SetStatus(Localization.T("Cancelled")); }
        catch (Exception ex) { App.Log(ex); VM.SetStatus(Localization.T("Error")); ShowError(ex.Message); }
    }
    private void ShowError(string message)
    {
        if (message.Length < 400) { MessageBox.Show(this, message, Localization.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var text = new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) };
        var dialog = new Window { Title = Localization.T("Error"), Owner = this, Width = 700, Height = 440, Content = text, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.ShowDialog();
    }
    private async Task<bool> ConfirmChanges()
    {
        if (!VM.Dirty) return true;
        var answer = MessageBox.Show(this, Localization.T("Unsaved"), "Stickel", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.No) return true;
        return await SaveProject();
    }
    private async Task<bool> SaveProject(bool saveAs = false)
    {
        if (VM.IsBusy) return false;
        var path = saveAs ? null : VM.ProjectPath;
        if (path is null)
        {
            var dialog = new SaveFileDialog { Filter = "Stickel project (*.stickel)|*.stickel", DefaultExt = ".stickel", AddExtension = true, FileName = VM.Project.Name == "Untitled" ? "My Sticker" : VM.Project.Name };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }
        await Guard(() => VM.Save(path)); return !VM.Dirty;
    }
    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveProject(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    private async void New_Click(object sender, RoutedEventArgs e) { if (await ConfirmChanges()) { Pause(); VM.New(); } }
    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmChanges()) return;
        var dialog = new OpenFileDialog { Filter = "Stickel project (*.stickel)|*.stickel" };
        if (dialog.ShowDialog(this) == true) { Pause(); await Guard(() => VM.Open(dialog.FileName)); }
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*", Multiselect = true };
        if (dialog.ShowDialog(this) == true) await Guard(() => VM.Import(dialog.FileNames));
    }
    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled || VM.IsBusy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Handled = true;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 1 && files[0].EndsWith(".stickel", StringComparison.OrdinalIgnoreCase))
        { if (await ConfirmChanges()) { Pause(); await Guard(() => VM.Open(files[0])); } return; }
        await Guard(() => VM.Import(files.SelectMany(p => Directory.Exists(p) ? Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly) : [p])));
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        Pause(); Keyboard.ClearFocus(); Focus();
        await Guard(async () =>
        {
            VM.ValidateExport();
            var dialog = new OpenFolderDialog { Title = Localization.T("Export") };
            if (dialog.ShowDialog(this) == true) await VM.Export(dialog.FolderName);
        });
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => VM.Operation?.Cancel();
    private void Language_Changed(object sender, SelectionChangedEventArgs e) { if (!ready) return; VM.SetLanguage(((ComboBoxItem)LanguagePicker.SelectedItem).Tag.ToString()!); UpdateThemeButton(); }
    private void Theme_Click(object sender, RoutedEventArgs e) { VM.ToggleTheme(); UpdateThemeButton(); }
    private void Color_Click(object sender, RoutedEventArgs e) => VM.Background = ((Button)sender).Tag.ToString()!;
    private MediaAsset? CurrentLibraryAsset => (MediaTabs.SelectedIndex == 0 ? VideoList : ImageList).SelectedItem as MediaAsset;
    private void Asset_DoubleClick(object sender, MouseButtonEventArgs e) { if (!VM.IsBusy && ((ListBox)sender).SelectedItem is MediaAsset asset) VM.AddClip(asset); }
    private void AddClip_Click(object sender, RoutedEventArgs e) { if (CurrentLibraryAsset is { } a) VM.AddClip(a); }
    private void RemoveMedia_Click(object sender, RoutedEventArgs e) { if (CurrentLibraryAsset is { } a) VM.RemoveAsset(a); }
    private void Asset_MouseMove(object sender, MouseEventArgs e)
    {
        if (!VM.IsBusy && e.LeftButton == MouseButtonState.Pressed && ((ListBox)sender).SelectedItem is MediaAsset a)
            DragDrop.DoDragDrop((ListBox)sender, new DataObject(typeof(MediaAsset), a), DragDropEffects.Copy);
    }
    private void Timeline_Drop(object sender, DragEventArgs e)
    {
        if (VM.IsBusy) return;
        if (e.Data.GetDataPresent(typeof(MediaAsset))) { VM.AddClip((MediaAsset)e.Data.GetData(typeof(MediaAsset))); e.Handled = true; }
    }
    private void DeleteClip_Click(object sender, RoutedEventArgs e) => VM.RemoveClip();
    private void MoveLeft_Click(object sender, RoutedEventArgs e) => VM.MoveClip(-1);
    private void MoveRight_Click(object sender, RoutedEventArgs e) => VM.MoveClip(1);
    private void ApplyTrim_Click(object sender, RoutedEventArgs e)
    {
        try { VM.Trim(double.Parse(TrimIn.Text, CultureInfo.InvariantCulture), double.Parse(TrimOut.Text, CultureInfo.InvariantCulture)); }
        catch (Exception ex) { ShowError(ex.Message); }
    }
    private async void Watermark_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Image files|*.png;*.webp;*.jpg;*.jpeg;*.gif;*.bmp;*.tiff;*.svg;*.heic;*.avif|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await Guard(async () => { await VM.Tools.NormalizeImage(dialog.FileName); VM.SetWatermark(dialog.FileName); });
    }
    private void WatermarkMove_Click(object sender, RoutedEventArgs e)
    {
        var w = VM.Project.Watermark; var direction = ((Button)sender).Tag.ToString();
        VM.SetWatermarkPosition(w.CenterX + (direction == "left" ? -.01 : direction == "right" ? .01 : 0), w.CenterY + (direction == "up" ? -.01 : direction == "down" ? .01 : 0));
    }
    private void CropPreset_Click(object sender, RoutedEventArgs e) => VM.CropPreset(double.Parse(((Button)sender).Tag.ToString()!, CultureInfo.InvariantCulture));
    private void ResetCrop_Click(object sender, RoutedEventArgs e) { cropMode = false; VM.CropPreset(0); VM.Zoom = 100; VM.SetPosition(0, 0); }
    private void DrawCrop_Click(object sender, RoutedEventArgs e) { cropMode = !cropMode; VM.SetStatus(Localization.T(cropMode ? "FrameCropHint" : "Ready")); _ = ShowSource(); }
    private void PreviewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return; Pause(); VM.WholePreview = WholeMode.IsChecked == true; PositionChanged();
    }
    private async void EditorChanged()
    {
        if (!ready) return;
        if (playing || proxyActive) Pause();
        DurationDot.Fill = new SolidColorBrush(VM.ValidDuration ? Color.FromRgb(69, 210, 147) : Color.FromRgb(247, 98, 111));
        DurationDot.ToolTip = Localization.T(VM.ValidDuration ? "Valid" : "Invalid");
        AutomationProperties.SetName(DurationDot, Localization.T(VM.ValidDuration ? "Valid" : "Invalid"));
        QualityNote.Text = Localization.T(VM.Project.Format == ExportFormat.Png ? "Lossless" : VM.Project.TelegramMode && VM.Project.IsStatic ? "BestImageQuality" : VM.Project.TelegramMode ? "AutoQuality" : "Quality");
        StaticNote.Visibility = VM.Project.IsStatic ? Visibility.Visible : Visibility.Collapsed;
        EmptyPreview.Visibility = VM.Project.Clips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TrimIn.Text = MediaTools.N(VM.SelectedClip?.InPoint ?? 0); TrimOut.Text = MediaTools.N(VM.SelectedClip?.OutPoint ?? 0);
        if (timelineDragId is null) DrawTimeline();
        ResizePreview();
        try { await ShowSource(); } catch (Exception ex) { App.Log(ex); VM.SetStatus(ex.Message); }
    }
    private async void PositionChanged()
    {
        if (!ready || playing) return;
        if (VM.FollowPreviewSelection()) { DrawTimeline(); TrimIn.Text = MediaTools.N(VM.SelectedClip?.InPoint ?? 0); TrimOut.Text = MediaTools.N(VM.SelectedClip?.OutPoint ?? 0); }
        UpdatePlayhead();
        try { await ShowSource(); } catch (Exception ex) { App.Log(ex); VM.SetStatus(ex.Message); }
    }
    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) { if (ready) { ResizePreview(); ApplyGeometry(); } }
    private void ResizePreview()
    {
        double scale = Math.Min(Math.Max(1, PreviewSpace.ActualWidth - 4) / VM.Project.Width, Math.Max(1, PreviewSpace.ActualHeight - 4) / VM.Project.Height);
        double w = VM.Project.Width * scale, h = VM.Project.Height * scale;
        PreviewBorder.Width = w + 2; PreviewBorder.Height = h + 2; VideoHost.Width = w; VideoHost.Height = h; Overlay.Width = w; Overlay.Height = h;
        EmptyPreview.Width = w; Canvas.SetTop(EmptyPreview, Math.Max(0, (h - 110) / 2)); Canvas.SetLeft(EmptyPreview, 0);
    }
    private (TimelineClip? Clip, double Local) CurrentClip()
    {
        if (VM.Project.Clips.Count == 0) return (null, 0);
        if (VM.WholePreview) { var p = ProjectRules.Locate(VM.Project, VM.Playhead); return (p.Clip, p.LocalTime); }
        var c = VM.SelectedClip ?? VM.Project.Clips[0]; return (c, Math.Clamp(VM.Playhead - VM.StartOf(c), 0, Math.Max(0, c.Duration - 1d / 30)));
    }
    private async Task ShowSource()
    {
        if (playing || VM.Project.Clips.Count == 0) { if (VM.Project.Clips.Count == 0) { StillImage.Visibility = WatermarkImage.Visibility = Visibility.Collapsed; StillBackground.Visibility = Visibility.Visible; StillBackground.Fill = (Brush)FindResource("Bg"); StillBackground.Width = Overlay.Width; StillBackground.Height = Overlay.Height; player.Pause(); } return; }
        var (clip, local) = CurrentClip(); if (clip is null) return;
        var asset = VM.Project.Asset(clip); int version = ++sourceVersion;
        await mediaGate.WaitAsync();
        try
        {
            if (version != sourceVersion) return;
            if (asset.Kind == MediaKind.Image)
            {
                player.Pause(); proxyActive = false;
                var normalized = await VM.Tools.NormalizeImage(asset.SourcePath);
                if (version != sourceVersion) return;
                if (sourcePath != normalized || sourceBitmap is null) sourceBitmap = LoadBitmap(normalized);
                sourcePath = normalized; StillBackground.Visibility = StillImage.Visibility = Visibility.Visible;
            }
            else
            {
                StillBackground.Visibility = StillImage.Visibility = Visibility.Collapsed;
                var videoSource = await VM.Tools.PrepareMotion(asset);
                if (sourcePath != videoSource || proxyActive)
                {
                    var result = await Task.Run(() => player.Open(videoSource));
                    if (!result.Success) throw new IOException(result.Error);
                    sourcePath = videoSource; proxyActive = false;
                }
                if (version != sourceVersion) return;
                player.SeekAccurate((int)((clip.InPoint + local) * 1000));
            }
            var wm = VM.Project.Watermark;
            if (wm.Enabled && wm.SourcePath.Length > 0 && watermarkPath != wm.SourcePath)
            {
                watermarkBitmap = LoadBitmap(await VM.Tools.NormalizeImage(wm.SourcePath)); watermarkPath = wm.SourcePath;
            }
            ApplyGeometry();
        }
        finally { mediaGate.Release(); }
    }
    private void ApplyGeometry()
    {
        if (!ready || playing || proxyActive) return;
        var (clip, _) = CurrentClip(); if (clip is null) return;
        var asset = VM.Project.Asset(clip); var effective = clip;
        if (cropMode) effective = new TimelineClip { AssetId = clip.AssetId, InPoint = clip.InPoint, OutPoint = clip.OutPoint };
        var g = RenderGeometry.Calculate(VM.Project, effective);
        double s = Overlay.Width / VM.Project.Width;
        if (!double.IsFinite(s) || s <= 0) return;
        config.Video.Crop = new CropRect((uint)g.CropY, (uint)g.CropX, (uint)Math.Max(0, asset.Height - g.CropY - g.CropHeight), (uint)Math.Max(0, asset.Width - g.CropX - g.CropWidth));
        config.Video.AspectRatio = effective.Layout == LayoutMode.Stretch ? AspectRatio.Fill : AspectRatio.Keep;
        double fitScale = Math.Min((double)VM.Project.Width / g.CropWidth, (double)VM.Project.Height / g.CropHeight);
        config.Video.Zoom = effective.Layout == LayoutMode.Stretch ? effective.Zoom * 100 : g.Width / (g.CropWidth * fitScale) * 100;
        config.Video.PanXOffset = effective.OffsetX; config.Video.PanYOffset = effective.OffsetY;
        config.Video.BackColor = VM.Project.Format == ExportFormat.Mp4 ? (Color)ColorConverter.ConvertFromString(VM.Project.Background) : Color.FromRgb(28, 32, 39);
        player.Audio.Mute = VM.Project.MuteOutput || VM.Project.TelegramMode;
        if (asset.Kind == MediaKind.Image && sourceBitmap is not null)
        {
            var sx = Math.Clamp((int)Math.Round((double)g.CropX / asset.Width * sourceBitmap.PixelWidth), 0, sourceBitmap.PixelWidth - 1);
            var sy = Math.Clamp((int)Math.Round((double)g.CropY / asset.Height * sourceBitmap.PixelHeight), 0, sourceBitmap.PixelHeight - 1);
            var sw = Math.Clamp((int)Math.Round((double)g.CropWidth / asset.Width * sourceBitmap.PixelWidth), 1, sourceBitmap.PixelWidth - sx);
            var sh = Math.Clamp((int)Math.Round((double)g.CropHeight / asset.Height * sourceBitmap.PixelHeight), 1, sourceBitmap.PixelHeight - sy);
            StillImage.Source = new CroppedBitmap(sourceBitmap, new Int32Rect(sx, sy, sw, sh));
            StillImage.Width = g.Width * s; StillImage.Height = g.Height * s; Canvas.SetLeft(StillImage, g.X * s); Canvas.SetTop(StillImage, g.Y * s);
            StillBackground.Width = Overlay.Width; StillBackground.Height = Overlay.Height;
            StillBackground.Fill = VM.Project.Format == ExportFormat.Mp4 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(VM.Project.Background)) : CheckerBrush();
        }
        var w = VM.Project.Watermark;
        WatermarkImage.Visibility = w.Enabled && w.SourcePath.Length > 0 && watermarkBitmap is not null && !cropMode ? Visibility.Visible : Visibility.Collapsed;
        if (watermarkBitmap is not null)
        {
            var ww = Overlay.Width * w.WidthPercent / 100; var wh = ww * watermarkBitmap.PixelHeight / watermarkBitmap.PixelWidth;
            WatermarkImage.Source = watermarkBitmap; WatermarkImage.Width = ww; WatermarkImage.Height = wh; WatermarkImage.Opacity = w.Opacity;
            watermarkBounds = new(w.CenterX * Overlay.Width - ww / 2, w.CenterY * Overlay.Height - wh / 2, ww, wh);
            Canvas.SetLeft(WatermarkImage, watermarkBounds.X); Canvas.SetTop(WatermarkImage, watermarkBounds.Y);
        }
    }
    private static Brush CheckerBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(43, 49, 58)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(54, 61, 72)), null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(54, 61, 72)), null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
        return new DrawingBrush(group) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 16, 16) };
    }
    public static BitmapSource LoadBitmap(string path)
    {
        var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.GetFullPath(path)); b.EndInit(); b.Freeze(); return b;
    }
    private async void Transport_Click(object sender, RoutedEventArgs e)
    {
        var tag = ((Button)sender).Tag.ToString();
        if (tag == "center") { VM.SetPosition(0, 0); return; }
        if (tag == "play") { if (playing) Pause(); else await Guard(Play); return; }
        Pause();
        var delta = tag == "prev" ? -1d / 30 : tag == "next" ? 1d / 30 : tag == "back" ? -1 : tag == "forward" ? 1 : 0;
        VM.Playhead = tag == "stop" ? VM.WholePreview ? 0 : VM.SelectedClip is { } c ? VM.StartOf(c) : 0 : VM.Playhead + delta;
    }
    private async Task Play()
    {
        if (VM.Project.Clips.Count == 0 || VM.IsBusy) return;
        cropMode = false; var whole = VM.WholePreview; var start = whole ? 0 : VM.SelectedClip is { } c ? VM.StartOf(c) : 0;
        var duration = whole ? VM.TotalDuration : VM.SelectedClip?.Duration ?? 0;

        if (!whole && VM.SelectedClip is { } selected)
        {
            var asset = VM.Project.Asset(selected);
            var extension = Path.GetExtension(asset.SourcePath).ToLowerInvariant();
            if (asset.Kind == MediaKind.Video && extension is not ".gif" and not ".webp")
            {
                var source = await VM.Tools.PrepareMotion(asset);
                await mediaGate.WaitAsync();
                try
                {
                    if (sourcePath != source || proxyActive)
                    {
                        var result = await Task.Run(() => player.Open(source));
                        if (!result.Success) throw new IOException(result.Error);
                        sourcePath = source; proxyActive = false;
                    }
                    var local = Math.Clamp(VM.Playhead - start, 0, Math.Max(0, duration - 1d / 30));
                    player.SeekAccurate((int)((selected.InPoint + local) * 1000));
                    ApplyGeometry();
                    player.Audio.Mute = VM.Project.MuteOutput || VM.Project.TelegramMode;
                    proxyStart = start - selected.InPoint;
                    playing = true; clock.Start(); player.Play();
                }
                finally { mediaGate.Release(); }
                return;
            }
        }

        var target = await VM.PreparePreview(whole); if (target is null) return;
        await mediaGate.WaitAsync();
        try
        {
            ++sourceVersion; var result = await Task.Run(() => player.Open(target));
            if (!result.Success) throw new IOException(result.Error);
            config.Video.ResetViewport(); config.Video.BackColor = Color.FromRgb(28, 32, 39);
            StillImage.Visibility = StillBackground.Visibility = WatermarkImage.Visibility = Visibility.Collapsed;
            proxyStart = start; proxyActive = true;
            var local = Math.Clamp(VM.Playhead - start, 0, Math.Max(0, duration - .033));
            if (local >= duration - .05) local = 0;
            player.SeekAccurate((int)(local * 1000)); player.Audio.Mute = VM.Project.MuteOutput || VM.Project.TelegramMode;
            playing = true; clock.Start(); player.Play();
        }
        finally { mediaGate.Release(); }
    }
    private void Pause() { playing = false; clock.Stop(); player.Pause(); }
    private void Clock_Tick(object? sender, EventArgs e)
    {
        if (!playing) return;
        clockUpdate = true; VM.SetClock(proxyStart + (double)player.CurTime / TimeSpan.TicksPerSecond); UpdatePlayhead(); clockUpdate = false;
        if (VM.FollowPreviewSelection()) { DrawTimeline(); TrimIn.Text = MediaTools.N(VM.SelectedClip?.InPoint ?? 0); TrimOut.Text = MediaTools.N(VM.SelectedClip?.OutPoint ?? 0); }
        var end = VM.WholePreview ? VM.TotalDuration : VM.SelectedClip is { } c ? VM.StartOf(c) + c.Duration : VM.TotalDuration;
        if (VM.Playhead >= end - .04 || player.Status == Status.Ended) { Pause(); VM.Playhead = Math.Max(proxyStart, end - 1d / 30); }
    }
    private void Seek_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || clockUpdate) return;
        if (playing && proxyActive) player.SeekAccurate((int)(Math.Max(0, e.NewValue - proxyStart) * 1000));
    }
    private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (VM.IsBusy || CurrentClip().Clip is null) return;
        Pause(); if (proxyActive) _ = ShowSource();
        Overlay.Focus();
        dragStart = e.GetPosition(Overlay); VM.BeginGesture();
        if (cropMode) { dragKind = "crop"; CropRectangle.Visibility = Visibility.Visible; Canvas.SetLeft(CropRectangle, dragStart.X); Canvas.SetTop(CropRectangle, dragStart.Y); CropRectangle.Width = CropRectangle.Height = 0; }
        else if (WatermarkImage.Visibility == Visibility.Visible && watermarkBounds.Contains(dragStart)) { dragKind = "watermark"; initialX = VM.Project.Watermark.CenterX; initialY = VM.Project.Watermark.CenterY; }
        else { dragKind = "media"; var clip = CurrentClip().Clip!; if (VM.SelectedClip?.Id != clip.Id) VM.Select(clip.Id, false); initialX = clip.OffsetX; initialY = clip.OffsetY; }
        Overlay.CaptureMouse(); e.Handled = true;
    }
    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (!Overlay.IsMouseCaptured || dragKind is null) return;
        var p = e.GetPosition(Overlay); var dx = (p.X - dragStart.X) / Overlay.Width; var dy = (p.Y - dragStart.Y) / Overlay.Height;
        if (dragKind == "watermark") VM.SetWatermarkPosition(initialX + dx, initialY + dy);
        else if (dragKind == "media") VM.SetPosition(initialX + dx, initialY + dy);
        else { Canvas.SetLeft(CropRectangle, Math.Min(p.X, dragStart.X)); Canvas.SetTop(CropRectangle, Math.Min(p.Y, dragStart.Y)); CropRectangle.Width = Math.Abs(p.X - dragStart.X); CropRectangle.Height = Math.Abs(p.Y - dragStart.Y); }
    }
    private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (dragKind is null) return;
        if (dragKind == "crop" && VM.SelectedAsset is { } a && CropRectangle.Width > 5 && CropRectangle.Height > 5)
        {
            double scale = Math.Min(Overlay.Width / a.Width, Overlay.Height / a.Height);
            double left = (Overlay.Width - a.Width * scale) / 2, top = (Overlay.Height - a.Height * scale) / 2;
            var x = Math.Clamp((Canvas.GetLeft(CropRectangle) - left) / (a.Width * scale), 0, .99);
            var y = Math.Clamp((Canvas.GetTop(CropRectangle) - top) / (a.Height * scale), 0, .99);
            VM.SetCrop(x, y, Math.Clamp(CropRectangle.Width / (a.Width * scale), .01, 1 - x), Math.Clamp(CropRectangle.Height / (a.Height * scale), .01, 1 - y));
        }
        cropMode = false; CropRectangle.Visibility = Visibility.Collapsed; dragKind = null; Overlay.ReleaseMouseCapture(); VM.EndGesture(); VM.SetStatus(Localization.T("Ready"));
    }
    private void TimelineZoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (ready) DrawTimeline(); }
    private void Timeline_SizeChanged(object sender, SizeChangedEventArgs e) { if (ready && timelineDragId is null) DrawTimeline(); }
    private void DrawTimeline()
    {
        TimelineCanvas.Children.Clear(); TimelineCanvas.Width = Math.Max(Math.Max(500, TimelineScroll.ActualWidth - 22), VM.TotalDuration * PixelsPerSecond + 32);
        var muted = (Brush)FindResource("Muted"); var accent = (Brush)FindResource("Accent");
        for (int i = 0; i <= Math.Min(2000, Math.Ceiling(VM.TotalDuration)); i++)
        {
            var tick = new TextBlock { Text = i + "s", FontSize = 10, Foreground = muted, IsHitTestVisible = false }; Canvas.SetLeft(tick, i * PixelsPerSecond + 4); Canvas.SetTop(tick, 2); TimelineCanvas.Children.Add(tick);
        }
        double x = 0;
        foreach (var clip in VM.Project.Clips)
        {
            var asset = VM.Project.Asset(clip); var width = Math.Max(3, clip.Duration * PixelsPerSecond - 3);
            var card = new Border { Width = width, Height = 68, CornerRadius = new(5), Background = (Brush)FindResource("Raised"), BorderBrush = clip.Id == VM.SelectedClip?.Id ? accent : (Brush)FindResource("Line"), BorderThickness = new(clip.Id == VM.SelectedClip?.Id ? 2 : 1), Tag = clip.Id, ToolTip = $"{asset.Name}\n{clip.InPoint:0.###} → {clip.OutPoint:0.###} s", Focusable = true, Cursor = Cursors.Hand, ClipToBounds = true };
            AutomationProperties.SetName(card, "Clip " + asset.Name); AutomationProperties.SetAutomationId(card, "Clip-" + clip.Id);
            var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(6) }); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new() { Width = new GridLength(6) });
            grid.Children.Add(new Border { Background = accent, Opacity = .55 }); var right = new Border { Background = accent, Opacity = .55 }; Grid.SetColumn(right, 2); grid.Children.Add(right);
            var content = new StackPanel { Margin = new(7, 5, 7, 3), IsHitTestVisible = false }; Grid.SetColumn(content, 1);
            if (File.Exists(asset.ThumbnailPath)) content.Children.Add(new Image { Source = LoadBitmap(asset.ThumbnailPath), Height = 28, Stretch = Stretch.UniformToFill, Opacity = .75 });
            content.Children.Add(new TextBlock { Text = asset.Name, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = clip.Duration.ToString("0.##") + " s", FontSize = 9, Foreground = muted }); grid.Children.Add(content); card.Child = grid;
            card.MouseLeftButtonDown += Clip_MouseDown; card.MouseMove += Clip_MouseMove; card.MouseLeftButtonUp += Clip_MouseUp;
            card.KeyDown += (_, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { VM.Select(clip.Id); e.Handled = true; } };
            Canvas.SetLeft(card, x); Canvas.SetTop(card, 25); TimelineCanvas.Children.Add(card); x += clip.Duration * PixelsPerSecond;
        }
        if (VM.Project.Clips.Count == 0) { var text = new TextBlock { Text = Localization.T("EmptyTimeline"), Foreground = muted, FontSize = 13, IsHitTestVisible = false }; Canvas.SetLeft(text, 22); Canvas.SetTop(text, 48); TimelineCanvas.Children.Add(text); }
        playheadLine = new Line { Stroke = accent, StrokeThickness = 2, Y1 = 19, Y2 = 102, IsHitTestVisible = false }; TimelineCanvas.Children.Add(playheadLine); UpdatePlayhead();
    }
    private void UpdatePlayhead() { if (playheadLine is not null) playheadLine.X1 = playheadLine.X2 = VM.Playhead * PixelsPerSecond; }
    private void Timeline_MouseDown(object sender, MouseButtonEventArgs e) { if (!VM.IsBusy) { Pause(); VM.Playhead = e.GetPosition(TimelineCanvas).X / PixelsPerSecond; } }
    private void Clip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (VM.IsBusy) return; var card = (Border)sender; var id = (string)card.Tag; var p = e.GetPosition(card);
        timelineDragId = id; timelineCaptured = card; timelineDragX = e.GetPosition(TimelineCanvas).X;
        VM.Select(id); VM.BeginGesture(); timelineIn = VM.SelectedClip!.InPoint; timelineOut = VM.SelectedClip.OutPoint;
        timelineEdge = p.X < 9 ? "in" : p.X > card.Width - 9 ? "out" : "move";
        card.CaptureMouse(); e.Handled = true;
    }
    private void Clip_MouseMove(object sender, MouseEventArgs e)
    {
        if (timelineDragId is null || timelineCaptured is null || !timelineCaptured.IsMouseCaptured || VM.SelectedClip is not { } c || VM.SelectedAsset is not { } a) return;
        double delta = (e.GetPosition(TimelineCanvas).X - timelineDragX) / PixelsPerSecond;
        if (timelineEdge == "in" && a.Kind == MediaKind.Video)
            VM.Change(() => c.InPoint = Math.Clamp(timelineIn + delta, 0, timelineOut - 1d / 30), "trim");
        else if (timelineEdge == "out")
            VM.Change(() => c.OutPoint = Math.Clamp(timelineOut + delta, c.InPoint + 1d / 30, a.Kind == MediaKind.Image ? 3600 : a.Duration), "trim");
        timelineCaptured.Width = Math.Max(3, c.Duration * PixelsPerSecond - 3);
    }
    private void Clip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (timelineDragId is null) return;
        var id = timelineDragId; var position = e.GetPosition(TimelineCanvas).X; var edge = timelineEdge;
        timelineCaptured?.ReleaseMouseCapture(); timelineDragId = null; timelineCaptured = null; VM.EndGesture();
        if (edge == "move" && Math.Abs(position - timelineDragX) > 8)
        {
            double t = 0; int index = VM.Project.Clips.Count - 1;
            for (int i = 0; i < VM.Project.Clips.Count; i++) { t += VM.Project.Clips[i].Duration * PixelsPerSecond; if (position < t) { index = i; break; } }
            VM.Reorder(id, index);
        }
        DrawTimeline(); e.Handled = true;
    }
    private void OutputFolder_Click(object sender, RoutedEventArgs e) { if (Directory.Exists(VM.LastOutput)) Process.Start(new ProcessStartInfo("explorer.exe", VM.LastOutput) { UseShellExecute = true }); }
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (VM.IsBusy) { if (e.Key == Key.Escape) VM.Operation?.Cancel(); return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { e.Handled = true; await SaveProject(); }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S) { e.Handled = true; await SaveProject(true); }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O) { e.Handled = true; Open_Click(this, new RoutedEventArgs()); }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I) { e.Handled = true; Import_Click(this, new RoutedEventArgs()); }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { e.Handled = true; New_Click(this, new RoutedEventArgs()); }
        if (Keyboard.FocusedElement is TextBox) return;
        if (e.Key == Key.Space) { e.Handled = true; if (playing) Pause(); else await Guard(Play); }
        else if (e.Key == Key.Delete) { VM.RemoveClip(); e.Handled = true; }
        else if (e.Key is Key.Left or Key.Right) { Pause(); VM.Playhead += e.Key == Key.Left ? -1d / 30 : 1d / 30; e.Handled = true; }
    }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closingApproved) return;
        e.Cancel = true;
        if (VM.IsBusy) { VM.Operation?.Cancel(); return; }
        if (!await ConfirmChanges()) return;
        Pause(); ++sourceVersion; await mediaGate.WaitAsync();
        try { player.Dispose(); VideoHost.Dispose(); }
        finally { mediaGate.Release(); }
        closingApproved = true; Close();
        try { if (Directory.Exists(VM.Tools.CacheDirectory)) Directory.Delete(VM.Tools.CacheDirectory, true); } catch (IOException) { }
    }
}
