using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using Stickel.Desktop;

namespace Stickel.ViewModelTests;

internal static class PlaybackChecks
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    internal static async Task Run()
    {
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Stickel;component/Themes/Minimal.xaml", UriKind.Relative) });
        Engine.Start(new EngineConfig { FFmpegPath = Path.Combine(AppContext.BaseDirectory, "runtime", "playback", "FFmpeg"),
            PluginsPath = Path.Combine(AppContext.BaseDirectory, "no-plugins"), UIRefresh = true, UIRefreshInterval = 50 });
        var window = new MainWindow(null);
        Application.Current.MainWindow = window;
        window.Show();
        var vm = window.VM;
        var player = Field<Player>(window, "player");
        try
        {
            var fixture = Path.GetFullPath("artifacts/playback-seeking"); Directory.CreateDirectory(fixture);
            var video = Path.Combine(fixture, "seek-with-audio.avi");
            await vm.Tools.Run(vm.Tools.Ffmpeg, ["-y", "-v", "error", "-f", "lavfi", "-i", "testsrc2=size=128x96:rate=30", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "10", "-c:v", "mpeg4", "-c:a", "mp3", video]);
            var still = Path.Combine(fixture, "blue.png");
            await vm.Tools.Run(vm.Tools.Ffmpeg, ["-y", "-v", "error", "-f", "lavfi", "-i", "color=c=blue:size=128x96", "-frames:v", "1", still]);
            await vm.Import([still, video]);
            vm.AddClip(vm.Images[0]);
            vm.AddClip(vm.Videos[0]); vm.Trim(2, 8);
            await Settle(window);
            // A preceding still and a nonzero in-point exercise timeline/source time conversion.
            await Exercise(window, player, false);
            vm.WholePreview = true; vm.Playhead = 2;
            await Settle(window);
            await Exercise(window, player, true);
            Console.WriteLine($"COMPLETE: {checks} playback checks.");
        }
        finally
        {
            typeof(MainWindow).GetMethod("Pause", Private)!.Invoke(window, null);
            typeof(MainWindow).GetField("closingApproved", Private)!.SetValue(window, true);
            player.Dispose(); window.Close();
        }
    }
    private static async Task Exercise(MainWindow window, Player player, bool whole)
    {
        var vm = window.VM;
        var mode = whole ? "rendered timeline" : "direct trimmed AVI";
        vm.Playhead = 2.5;
        await Settle(window);
        await (Task)typeof(MainWindow).GetMethod("Play", Private)!.Invoke(window, null)!;
        await Task.Delay(350);
        foreach (var tag in new[] { "forward", "back", "next", "prev" })
        {
            var before = vm.Playhead;
            var delta = tag == "forward" ? 1 : tag == "back" ? -1 : tag == "next" ? 1d / 30 : -1d / 30;
            typeof(MainWindow).GetMethod("Transport_Click", Private)!.Invoke(window, [new Button { Tag = tag }, new RoutedEventArgs()]);
            Check(Math.Abs(vm.Playhead - before - delta) < .005 && Field<bool>(window, "playing"), $"{mode}: {tag} moves playhead without pausing");
            await CheckAdvance(window, player, mode);
        }
        var slider = (Slider)window.FindName("SeekSlider");
        slider.Value = 4;
        slider.GetBindingExpression(Slider.ValueProperty)!.UpdateSource();
        Check(Math.Abs(vm.Playhead - 4) < .005, $"{mode}: seek bar updates timeline position");
        await CheckAdvance(window, player, mode);
        // SetClock must update the UI without sending a seek back into the player.
        var time = player.CurTime;
        vm.SetClock(3.25);
        Check(player.CurTime == time, $"{mode}: clock refresh does not trigger another seek");
        typeof(MainWindow).GetMethod("Transport_Click", Private)!.Invoke(window, [new Button { Tag = "stop" }, new RoutedEventArgs()]);
        Check(!Field<bool>(window, "playing") && Math.Abs(vm.Playhead - vm.SeekMin) < .005, $"{mode}: Stop still pauses and returns to start");
        await Settle(window);
        typeof(MainWindow).GetMethod("Transport_Click", Private)!.Invoke(window, [new Button { Tag = "forward" }, new RoutedEventArgs()]);
        await Settle(window);
        Check(!Field<bool>(window, "playing") && Math.Abs(vm.Playhead - vm.SeekMin - 1) < .005, $"{mode}: paused seeking remains paused");
    }
    private static async Task CheckAdvance(MainWindow window, Player player, string mode)
    {
        var target = window.VM.Playhead - Field<double>(window, "proxyStart");
        Check(Math.Abs(player.CurTime / (double)TimeSpan.TicksPerSecond - target) < .1, $"{mode}: engine receives source-relative seek time");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Field<bool>(window, "playing") && DateTime.UtcNow < deadline &&
            (player.Status != Status.Playing || player.CurTime / (double)TimeSpan.TicksPerSecond <= target + .08)) await Task.Delay(100);
        Check(Field<bool>(window, "playing") && player.Status == Status.Playing && player.CurTime / (double)TimeSpan.TicksPerSecond > target + .08,
            $"{mode}: actual video playback advances after seek (target={target:0.###}, actual={player.CurTime / (double)TimeSpan.TicksPerSecond:0.###}, status={player.Status})");
    }
    private static async Task Settle(MainWindow window)
    {
        await Task.Delay(100);
        var gate = Field<SemaphoreSlim>(window, "mediaGate"); await gate.WaitAsync(); gate.Release();
        await Task.Delay(100);
    }
    private static T Field<T>(MainWindow window, string name) => (T)typeof(MainWindow).GetField(name, Private)!.GetValue(window)!;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++; Console.WriteLine("PASS: " + label);
    }
}
