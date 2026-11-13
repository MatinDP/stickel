using System.IO;
using System.Windows;
using Stickel.Desktop;
using Localization = Stickel.Desktop.Localization;

namespace Stickel.ViewModelTests;

public static class Program
{
    private static int checks;
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application();
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                var fixture = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/integration/20261114-022052");
                var vm = new MainViewModel();
                await vm.Import([Path.Combine(fixture, "قرمز اصلی.png"), Path.Combine(fixture, "blue.png"), Path.Combine(fixture, "movie with sound.mp4")]);
                Check(vm.Images.Count == 2 && vm.Videos.Count == 1, "Media bins separate images and videos");
                vm.AddClip(vm.Images[0]); var first = vm.SelectedClip!;
                Check(first.Duration == 1 && vm.Project.Width == 512 && vm.Project.Height == 320, "First media initializes canvas and still duration defaults to one second");
                vm.BeginGesture(); vm.SetPosition(.1, .2); vm.SetPosition(.2, .3); vm.EndGesture();
                vm.UndoCommand.Execute(null); Check(vm.SelectedClip!.OffsetX == 0, "One undo reverses a complete mouse gesture");
                vm.RedoCommand.Execute(null); Check(vm.SelectedClip!.OffsetX == .2, "Redo restores a mouse gesture");
                vm.CropPreset(1); Check(Math.Abs(vm.SelectedClip!.CropWidth - .625) < .001, "Square crop preserves source geometry");
                vm.AddClip(vm.Images[1]); var secondId = vm.SelectedClip!.Id; vm.LayoutIndex = 1;
                Check(vm.Project.Clips[0].Layout == Stickel.Core.LayoutMode.Fit && vm.SelectedClip.Layout == Stickel.Core.LayoutMode.Fill, "Layout settings remain independent per clip");
                vm.MoveClip(-1); Check(vm.Project.Clips[0].Id == secondId, "Timeline reorder updates underlying clip sequence");
                vm.UndoCommand.Execute(null); Check(vm.Project.Clips[1].Id == secondId, "Timeline reordering is undoable");
                vm.WholePreview = true; vm.SetClock(1.5); vm.FollowPreviewSelection();
                Check(vm.SelectedClip?.Id == secondId, "Whole-timeline preview keeps editing controls on the currently displayed clip");
                vm.WholePreview = false;
                vm.Playhead = -1; Check(vm.Playhead == 1 && vm.SeekMin == 1 && vm.SeekMax == 2, "Selected-clip seeking cannot move before that clip");
                vm.Playhead = 20; Check(Math.Abs(vm.Playhead - (2 - 1d / 30)) < .000001, "Selected-clip seeking stays on its last frame instead of the next clip");
                vm.SetWatermark(Path.Combine(fixture, "watermark.png")); vm.WatermarkSize = 25; vm.Transparency = 70;
                Check(Math.Abs(vm.Project.Watermark.Opacity - .3) < .001, "Transparency percentage maps to watermark alpha");
                vm.Individual = true; vm.ModeIndex = 0;
                Check(vm.MuteOutput && !vm.ManualAudio && !vm.ManualQuality, "Telegram forces silent output and automatic quality in UI state");
                vm.ModeIndex = 1; Check(vm.ManualAudio && vm.ManualQuality, "Free mode enables manual audio and quality");
                vm.ModeIndex = 0;
                vm.Select(secondId); vm.Trim(0, 4); Check(!vm.ValidDuration, "Duration status becomes invalid above three seconds");
                var blocked = false; try { vm.ValidateExport(); } catch (InvalidOperationException) { blocked = true; }
                Check(blocked, "View model export preflight rejects overlong Telegram timelines");
                vm.UndoCommand.Execute(null); Check(vm.ValidDuration, "Undo updates duration validity");
                var originalLanguage = vm.Language; var originalTheme = vm.LightTheme;
                vm.SetLanguage("en"); Check(Localization.T("Settings") == "SETTINGS", "English localization resources switch");
                vm.ToggleTheme(); Check(vm.LightTheme != originalTheme, "Theme preference changes");
                vm.SetLanguage("fa"); Check(vm.UiFlow == FlowDirection.RightToLeft && Localization.T("Settings") == "تنظیمات", "Persian content direction and labels switch");
                vm.SetLanguage(originalLanguage); if (vm.LightTheme != originalTheme) vm.ToggleTheme();
                var target = Path.Combine(fixture, "viewmodel-roundtrip.stickel"); await vm.Save(target);
                Check(!vm.Dirty && vm.ProjectPath == target, "Save clears dirty state and records project path");
                vm.New(); await vm.Open(target); Check(vm.Project.Clips.Count == 2 && vm.Project.Watermark.WidthPercent == 25 && vm.Project.ExportIndividual, "View model project round-trip retains timeline and global settings");
                vm.RemoveAsset(vm.Images[0]); vm.UndoCommand.Execute(null); Check(vm.Images.Count == 2 && vm.Project.Clips.Count == 2, "Removing media and its clip references is undoable");
                vm.AddClip(vm.Videos[0]); vm.ModeIndex = 0; vm.FormatIndex = 0; vm.Playhead = 0;
                await vm.Save(Path.Combine(fixture, "Starter.stickel"));
                Console.WriteLine($"COMPLETE: {checks} view-model checks.");
                app.Shutdown(0);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); app.Shutdown(1); }
        });
        app.Run();
    }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
}
