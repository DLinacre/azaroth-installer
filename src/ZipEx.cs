using System.IO.Compression;

namespace AzarothInstaller;

public static class ZipEx
{
    public static void ExtractTo(string zipPath, string destDir, IProgress<long> progress, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        long total = 0;
        foreach (var e in zip.Entries) total += e.Length;

        long done = 0;
        long lastReported = 0;
        var buf = new byte[262144];

        foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var target = SafePath(destDir, rel);

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || string.IsNullOrEmpty(entry.Name) || string.IsNullOrWhiteSpace(Path.GetFileName(target)))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var src = entry.Open())
            using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 262144, true))
            {
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    dst.Write(buf, 0, n);
                    done += n;
                    if (done - lastReported > 16 * 1024 * 1024)
                    {
                        lastReported = done;
                        progress?.Report(total > 0 ? done * 100 / total : 100);
                    }
                }
            }
        }
        progress?.Report(100);
    }

    /// <summary>Prevents zip-slip: every entry must stay inside destDir.</summary>
    public static string SafePath(string root, string entryName)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar)) fullRoot += Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, entryName));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), fullRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refused unsafe zip entry path: " + entryName);
        return target;
    }

    public static List<string> PeekEntries(string zipPath, int maxEntries = 30000)
    {
        var list = new List<string>();
        using var zip = ZipFile.OpenRead(zipPath);
        int i = 0;
        foreach (var e in zip.Entries)
        {
            if (i++ >= maxEntries) { list.Add("...(entry list truncated)"); break; }
            list.Add(e.FullName.Replace('\\', '/'));
        }
        return list;
    }
}
