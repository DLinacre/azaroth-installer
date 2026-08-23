namespace AzarothInstaller;

public class DownloadProgress
{
    public long Received;
    public long Total = -1;
    public string Url = "";
    public string File = "";

    public string PercentText
    {
        get
        {
            if (Total <= 0) return $"{Received / 1048576.0:0.#} MB";
            return $"{Received / 1048576.0:0.#} / {Total / 1048576.0:0.#} MB ({Received * 100 / Total}%)" +
                   $"  @ {SpeedMbs:0.#} MB/s";
        }
    }

    long _lastBytes;
    long _lastTicks;
    double _lastSpeed;

    public void Tick()
    {
        var now = Environment.TickCount64;
        if (_lastTicks == 0)
        {
            _lastTicks = now;
            _lastBytes = Received;
            return;
        }
        long deltaMs = now - _lastTicks;
        if (deltaMs >= 400)
        {
            double secs = deltaMs / 1000.0;
            double mb = (Received - _lastBytes) / 1048576.0;
            if (secs > 0 && mb >= 0)
            {
                double inst = mb / secs;
                _lastSpeed = _lastSpeed == 0 ? inst : _lastSpeed * 0.6 + inst * 0.4;
            }
            _lastBytes = Received;
            _lastTicks = now;
        }
    }

    public double SpeedMbs => _lastSpeed;
}

public static class Downloader
{
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        var c = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        c.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        return c;
    }

    /// <summary>Try each URL (3 attempts each) until one succeeds. Returns the destination path.</summary>
    public static async Task<string> DownloadFirstAsync(List<string> urls, string destFile,
        IProgress<DownloadProgress> progress, CancellationToken ct, Action<string> log)
    {
        var usable = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (usable.Count == 0)
            throw new Exception("No download URL configured. Edit config.json or pick a local file in the wizard.");

        Exception last = null;
        foreach (var url in usable)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    log?.Invoke($"Downloading: {url} (attempt {attempt}/3)");
                    await DownloadOneAsync(url, destFile, progress, ct);
                    return destFile;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    log?.Invoke("  attempt failed: " + ex.Message);
                }
            }
        }
        throw new Exception("All download sources failed." + (last == null ? "" : " Last error: " + last.Message));
    }

    static async Task DownloadOneAsync(string url, string destFile, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode} {resp.StatusCode} for {url}");

        var total = resp.Content.Headers.ContentLength ?? -1;
        var dir = Path.GetDirectoryName(Path.GetFullPath(destFile));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = destFile + ".part";

        long received = 0;
        var p = new DownloadProgress { Total = total, Url = url, File = Path.GetFileName(destFile) };
        using (var stream = await resp.Content.ReadAsStreamAsync(ct))
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 262144, true))
        {
            var buf = new byte[131072];
            long lastReport = 0;
            int n;
            while ((n = await stream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                await fs.WriteAsync(buf, 0, n, ct);
                received += n;
                long now = Environment.TickCount64;
                if (now - lastReport > 250)
                {
                    lastReport = now;
                    p.Received = received;
                    p.Tick();
                    progress?.Report(p);
                }
            }
        }
        p.Received = total >= 0 ? total : received;
        progress?.Report(p);
        if (File.Exists(destFile)) File.Delete(destFile);
        File.Move(tmp, destFile);
    }
}
