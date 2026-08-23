# Performance Optimisation Notes

## 1. Smaller setup.exe

```xml
<!-- src/AzarothInstaller.csproj -->
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>   <!-- add -->
  <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  <DebugType>embedded</DebugType>
  <!-- Trimming: only enable after testing — WinForms/reflection often breaks.
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode>
  -->
</PropertyGroup>
```

Expected: single-file compression typically reduces the self-contained .NET 8
installer by 20–35% (e.g. ~140 MB → ~100 MB).

## 2. Resumable, ranged downloads

```csharp
// Add to Downloader.DownloadOneAsync: if a .part file exists, issue a Range request.
var existingLen = File.Exists(tmp) ? new FileInfo(tmp).Length : 0;
var req = new HttpRequestMessage(HttpMethod.Get, url);
if (existingLen > 0) req.Headers.Range = new RangeHeaderValue(existingLen, null);
using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
if (resp.StatusCode == HttpStatusCode.PartialContent) { /* append */ }
else if (resp.IsSuccessStatusCode) { existingLen = 0; /* truncate */ }
```

## 3. Poll, don't sleep (smoke test)

```csharp
// Replace fixed Task.Delay(45000) with log polling:
var deadline = DateTime.UtcNow.AddMinutes(5);
while (DateTime.UtcNow < deadline)
{
    if (!Misc.ProcessRunning("worldserver")) { DumpTailLogs(sd); return false; }
    var log = Path.Combine(sd, "logs", "worldserver.log");
    if (File.Exists(log) && File.ReadAllText(log).Contains("world server is running", StringComparison.OrdinalIgnoreCase))
        return true;
    await Task.Delay(2000, ct);
}
```

## 4. Accurate VRAM (>4 GB)

`Win32_VideoController.AdapterRAM` is a **uint32** and wraps above 4 GB. Prefer:

- `Win32_VideoController.AdapterRAM` is unreliable; read
  `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}\0000\HardwareInformation.qwMemorySize`
  (a **qword / 64-bit** value), or
- DXGI `IDXGIAdapter::GetDesc().DedicatedVideoMemory` via P/Invoke, or
- CIM `MSFT_VideoController` on modern Windows.

## 5. Optimize web assets

- `assets/banner.png` (1.09 MB) → export a WebP/AVIF (~150–300 KB) with PNG
  fallback. The two `*-art.png` source files (~3.4 MB combined) shouldn't be in
  every clone — move to a separate `assets-source` branch or a release asset.

```html
<picture>
  <source srcset="banner.avif" type="image/avif">
  <source srcset="banner.webp" type="image/webp">
  <img src="banner.png" alt="Azaroth Core — One-Click Installer" width="1280" height="640" loading="eager">
</picture>
```

## 6. Responsive/async WMI probes

Run WMI queries on a background task with a timeout and a "Probing hardware…"
status; WMI can block for seconds on systems with many devices.

## 7. HttpClient hygiene

- Keep the static singleton (good — avoids socket exhaustion).
- Add a per-request timeout fallback (e.g. 10 min for large files) in addition to
  the UI `CancellationToken`.
- Set a contactable `User-Agent` in addition to the Chrome-compatibility UA.
