using System.IO;
using System.Windows;
using FlyleafLib;

namespace Stickel.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { Log(args.Exception); MessageBox.Show(args.Exception.Message, "Stickel", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        try
        {
            Engine.Start(new EngineConfig
            {
                FFmpegPath = Path.Combine(AppContext.BaseDirectory, "runtime", "playback", "FFmpeg"),
                FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Error,
                LogLevel = LogLevel.Error, LogOutput = Path.Combine(Path.GetTempPath(), $"stickel-player-{Environment.ProcessId}.log"),
                PluginsPath = Path.Combine(AppContext.BaseDirectory, "no-plugins"), UIRefresh = true, UIRefreshInterval = 50
            });
            var window = new MainWindow(e.Args.FirstOrDefault(a => a.EndsWith(".stickel", StringComparison.OrdinalIgnoreCase)));
            MainWindow = window; window.Show();
        }
        catch (Exception ex) { Log(ex); MessageBox.Show("Stickel could not start.\n" + ex.Message, "Stickel", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    public static void Log(Exception ex)
    {
        try { var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stickel"); Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "errors.log"), DateTimeOffset.Now + " " + ex + Environment.NewLine); } catch { }
    }
}
