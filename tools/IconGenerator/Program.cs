using ImageMagick;
var assets = Path.GetFullPath("src/Stickel.Desktop/Assets");
using var vector = new MagickImage(Path.Combine(assets, "stickel.svg"));
using var icons = new MagickImageCollection();
foreach (var size in new uint[] { 16, 24, 32, 48, 64, 128, 256 }) { var icon = vector.Clone(); icon.Resize(size, size); icons.Add(icon); }
icons.Write(Path.Combine(assets, "stickel.ico"));
