using System.IO.Compression;
using System.Text.Json;

namespace Stickel.Core;

public static class ProjectArchive
{
    public static async Task Save(ProjectDocument document, string path, CancellationToken ct = default)
    {
        ProjectRules.Validate(document);
        var manifest = document.Clone();
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var asset in manifest.Assets)
                {
                    var source = document.Assets.First(a => a.Id == asset.Id).SourcePath;
                    asset.SourcePath = "media/" + asset.Id + Path.GetExtension(source).ToLowerInvariant();
                    await Add(zip, asset.SourcePath, source, ct);
                }
                if (document.Watermark.SourcePath.Length > 0)
                {
                    manifest.Watermark.SourcePath = "media/watermark" + Path.GetExtension(document.Watermark.SourcePath).ToLowerInvariant();
                    await Add(zip, manifest.Watermark.SourcePath, document.Watermark.SourcePath, ct);
                }
                var entry = zip.CreateEntry("project.json", CompressionLevel.Optimal);
                await using var jsonStream = entry.Open();
                await JsonSerializer.SerializeAsync(jsonStream, manifest, ProjectDocument.JsonOptions, ct);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task Add(ZipArchive zip, string name, string source, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        await using var output = entry.Open();
        await input.CopyToAsync(output, ct);
    }

    public static async Task<ProjectDocument> Load(string path, string workspace, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 20000 || zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
            throw new InvalidDataException("Invalid or excessively large project directory.");
        var json = zip.GetEntry("project.json") ?? throw new InvalidDataException("This is not a .stickel project.");
        if (json.Length > 16 * 1024 * 1024) throw new InvalidDataException("Project manifest is too large.");
        ProjectDocument document;
        await using (var input = json.Open()) document = await JsonSerializer.DeserializeAsync<ProjectDocument>(input, ProjectDocument.JsonOptions, ct) ?? throw new InvalidDataException("Empty project manifest.");
        ProjectRules.Validate(document);
        var names = document.Assets.Select(a => a.SourcePath).Append(document.Watermark.SourcePath).Where(p => p.Length > 0).Distinct().ToArray();
        var root = Path.GetFullPath(workspace) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        var mapping = new Dictionary<string, string>();
        long total = 0;
        foreach (var name in names)
        {
            var destination = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!name.StartsWith("media/", StringComparison.Ordinal) || !destination.StartsWith(root, StringComparison.OrdinalIgnoreCase) || name.Contains(':') || name.Contains('\\'))
                throw new InvalidDataException("Unsafe media path in project archive.");
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Missing media in archive: " + name);
            total = checked(total + entry.Length);
            mapping[name] = destination;
        }
        var free = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;
        if (total > free - 64L * 1024 * 1024) throw new IOException("Not enough disk space to open this project.");
        foreach (var (name, destination) in mapping)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var entry = zip.GetEntry(name)!;
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
            var buffer = new byte[131072];
            long written = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                written += count;
                if (written > entry.Length) throw new InvalidDataException("Invalid archive entry size.");
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            if (written != entry.Length) throw new InvalidDataException("Truncated archive entry.");
        }
        foreach (var asset in document.Assets) asset.SourcePath = mapping[asset.SourcePath];
        if (document.Watermark.SourcePath.Length > 0) document.Watermark.SourcePath = mapping[document.Watermark.SourcePath];
        return document;
    }
}
