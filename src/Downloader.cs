namespace AzarothInstaller;

public class DownloadProgress
{
    public long Received;
    public long Total = -1;
    public string Url = "";
    public string File = "";

    public string EtaText
    {
        get
        {
            if (Total <= 0 || SpeedMbs <= 0.05) return "";
            var remainingBytes = Total - Received;
            if (remainingBytes <= 0) return "0s";
            var secs = remainingBytes / (SpeedMbs * 1048576.0);
            if (secs < 60) return $"{secs:0}s left";
            var mins = (int)(secs / 60);
            var remSecs = (int)(secs % 60);
            return $"{mins}m {remSecs}s left";
        }
    }

    public string PercentText
    {
        get
        {
            var eta = EtaText;
            var etaSuffix = string.IsNullOrEmpty(eta) ? "" : $"  ·  {eta}";
            if (Total <= 0) return $"{Received / 1048576.0:0.#} MB";
            return $"{Received / 1048576.0:0.#} / {Total / 1048576.0:0.#} MB ({Received * 100 / Total}%)" +
                   $"  @ {SpeedMbs:0.#} MB/s{etaSuffix}";
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
        var dir = Path.GetDirectoryName(Path.GetFullPath(destFile));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = destFile + ".part";

        long existingLength = 0;
        if (File.Exists(tmp))
        {
            try { existingLength = new FileInfo(tmp).Length; } catch { existingLength = 0; }
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0)
        {
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
        }

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        bool isPartial = resp.StatusCode == System.Net.HttpStatusCode.PartialContent;
        if (!resp.IsSuccessStatusCode && !isPartial)
        {
            if (existingLength > 0)
            {
                try { File.Delete(tmp); } catch { }
                existingLength = 0;
                using var freshResp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!freshResp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)freshResp.StatusCode} {freshResp.StatusCode} for {url}");
                await SaveResponseStream(freshResp, tmp, destFile, 0, url, progress, ct);
                return;
            }
            throw new Exception($"HTTP {(int)resp.StatusCode} {resp.StatusCode} for {url}");
        }

        await SaveResponseStream(resp, tmp, destFile, existingLength, url, progress, ct);
    }

    static async Task SaveResponseStream(HttpResponseMessage resp, string tmp, string destFile, long existingLength, string url, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        bool isPartial = resp.StatusCode == System.Net.HttpStatusCode.PartialContent;
        long total = -1;
        if (isPartial && resp.Content.Headers.ContentRange?.Length != null)
        {
            total = resp.Content.Headers.ContentRange.Length.Value;
        }
        else if (resp.Content.Headers.ContentLength.HasValue)
        {
            total = isPartial ? (existingLength + resp.Content.Headers.ContentLength.Value) : resp.Content.Headers.ContentLength.Value;
        }

        var fileMode = isPartial && existingLength > 0 ? FileMode.Append : FileMode.Create;
        long received = isPartial ? existingLength : 0;
        var p = new DownloadProgress { Total = total, Url = url, File = Path.GetFileName(destFile), Received = received };

        using (var stream = await resp.Content.ReadAsStreamAsync(ct))
        using (var fs = new FileStream(tmp, fileMode, FileAccess.Write, FileShare.None, 262144, true))
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

    /// <summary>Verifies the SHA-256 checksum of a file against an expected hex string.</summary>
    public static async Task VerifySha256Async(string filePath, string expectedSha256, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return;
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144, true);
        var hashBytes = await sha.ComputeHashAsync(fs, ct);
        var actualHash = Convert.ToHexString(hashBytes);
        if (!string.Equals(actualHash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(filePath); } catch { }
            throw new Exception($"SHA-256 checksum mismatch for {Path.GetFileName(filePath)}.\n  Expected: {expectedSha256}\n  Actual  : {actualHash}\nFile deleted due to checksum failure.");
        }
    }
}
