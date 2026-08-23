using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Win32;

namespace AzarothInstaller;

public static class SysProbe
{
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static SystemInfo GetSystemInfo(Action<string> log)
    {
        var si = new SystemInfo();
        si.OsVersion = Environment.OSVersion.VersionString;
        si.Is64Bit = Environment.Is64BitOperatingSystem;
        si.LogicalCores = Environment.ProcessorCount;
        if (!si.Is64Bit)
            si.Warnings.Add("This PC runs 32-bit Windows. Azaroth Core needs 64-bit Windows 10/11.");

        // CPU
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject o in s.Get())
            {
                var name = o["Name"] as string;
                if (!string.IsNullOrEmpty(name)) si.CpuName = name.Trim();
                if (o["NumberOfCores"] != null) si.PhysicalCores = Math.Max(si.PhysicalCores, Convert.ToInt32(o["NumberOfCores"]));
                if (o["NumberOfLogicalProcessors"] != null) si.LogicalCores = Math.Max(si.LogicalCores, Convert.ToInt32(o["NumberOfLogicalProcessors"]));
            }
        }
        catch (Exception ex) { log?.Invoke("CPU probe (WMI) failed: " + ex.Message); }
        if (string.IsNullOrEmpty(si.CpuName))
            si.CpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

        // RAM
        try
        {
            using var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject o in s.Get())
                if (o["TotalPhysicalMemory"] != null) si.RamBytes = Convert.ToInt64(o["TotalPhysicalMemory"]);
        }
        catch (Exception ex) { log?.Invoke("RAM probe (WMI) failed: " + ex.Message); }
        if (si.RamBytes <= 0)
        {
            try
            {
                var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref ms)) si.RamBytes = (long)ms.ullTotalPhys;
            }
            catch { }
        }
        if (si.RamBytes > 0 && si.RamBytes < 4L * 1073741824)
            si.Warnings.Add("Less than 4 GB of RAM detected - the server will run slowly.");
        else if (si.RamBytes > 0 && si.RamBytes < 8L * 1073741824)
            si.Warnings.Add("8 GB RAM or more is recommended for a smooth experience (detected: " + si.RamGb + ").");

        // GPU
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                var g = new GpuInfo { Name = (o["Name"] as string) ?? "", DriverVersion = (o["DriverVersion"] as string) ?? "" };
                if (o["AdapterRAM"] != null) { try { g.VideoBytes = Convert.ToInt64(o["AdapterRAM"]); } catch { } }
                si.Gpus.Add(g);
            }
        }
        catch (Exception ex) { log?.Invoke("GPU probe (WMI) failed: " + ex.Message); }
        if (si.Gpus.Count == 0) si.Gpus.Add(new GpuInfo { Name = "Unknown (WMI query failed)" });

        // Drives
        try
        {
            var sysDrive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").ToUpperInvariant();
            using var s = new ManagementObjectSearcher("SELECT DeviceID, VolumeName, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (ManagementObject o in s.Get())
            {
                var dev = (o["DeviceID"] as string) ?? "";
                si.Drives.Add(new DriveStat
                {
                    Root = dev,
                    Label = (o["VolumeName"] as string) ?? "",
                    FileSystem = (o["FileSystem"] as string) ?? "",
                    TotalBytes = Convert.ToInt64(o["Size"]),
                    FreeBytes = Convert.ToInt64(o["FreeSpace"]),
                    IsSystem = dev.ToUpperInvariant() == sysDrive
                });
            }
        }
        catch (Exception ex) { log?.Invoke("Drive probe (WMI) failed: " + ex.Message); }
        if (si.Drives.Count == 0)
        {
            try
            {
                foreach (var di in DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed))
                {
                    var d = new DriveStat
                    {
                        Root = di.Name,
                        Label = di.VolumeLabel,
                        FileSystem = di.DriveFormat,
                        TotalBytes = di.TotalSize,
                        FreeBytes = di.TotalFreeSpace
                    };
                    d.IsSystem = di.Name.TrimEnd('\\', ':').ToUpperInvariant() + ":" == (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").ToUpperInvariant();
                    si.Drives.Add(d);
                }
            }
            catch { }
        }
        if (si.Drives.Count == 0) si.Warnings.Add("No fixed drives could be detected.");

        return si;
    }

    // ------------------------------------------------------------------ WoW
    static readonly HashSet<string> WowNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "world of warcraft", "wow", "wotlk", "wrath of the lich king", "3.3.5", "3.3.5a",
        "cataclysmclassic", "cataclysm classic", "wow classic", "world_of_warcraft", "warcraft"
    };

    static readonly HashSet<string> DescendNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "games", "game", "program files", "program files (x86)", "program files (x64)",
        "software", "apps", "application", "battle.net", "battle net", "gaming",
        "wow", "world of warcraft", "wotlk", "gameplay", "my games"
    };

    public static List<WowCandidate> ScanForWoW(Action<string> log, List<string> extraDirs)
    {
        var found = new List<WowCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
                var key = Path.GetFullPath(dir).TrimEnd('\\', '/');
                if (!seen.Add(key)) return;
                var c = ScoreWow(dir);
                if (c != null) found.Add(c);
            }
            catch { }
        }

        // 1) registry (classic WoW installs register here)
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = baseKey.OpenSubKey(@"SOFTWARE\Blizzard\World of Warcraft");
                if (k != null)
                {
                    var p = k.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(p)) AddCandidate(p);
                }
            }
            catch (Exception ex) { log?.Invoke("WoW registry scan: " + ex.Message); }
        }

        // 2) extra dirs from config / user
        foreach (var d in extraDirs) AddCandidate(d);

        // 3) file system scan (pruned: only descend into game-ish folders)
        int budget = 9000;
        void Walk(string dir, int depth)
        {
            if (budget <= 0 || depth > 6) return;
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(dir)) ?? "";
            if (WowNames.Contains(name) || name.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("warcraft", StringComparison.OrdinalIgnoreCase) >= 0)
                AddCandidate(dir);

            bool shouldDescend = depth < 2 || DescendNames.Contains(name) || WowNames.Contains(name);
            if (!shouldDescend) return;

            string[] sub;
            try { sub = Directory.GetDirectories(dir); }
            catch { return; }

            foreach (var s in sub)
            {
                if (budget <= 0) return;
                budget--;
                var sl = (Path.GetFileName(s) ?? "").ToLowerInvariant();
                if (sl.StartsWith(".") || sl.StartsWith("$") || sl == "system volume information" ||
                    sl == "perflogs" || sl == "windows" || sl == "recovery" || sl == "users")
                    continue;
                if (depth >= 2 && !(DescendNames.Contains(sl) || WowNames.Contains(sl) ||
                    sl.Contains("wow") || sl.Contains("warcraft")))
                    continue;
                Walk(s, depth + 1);
            }
        }

        foreach (var d in SafeDrives())
            Walk(d.RootDirectory.FullName, 0);

        return found.OrderByDescending(x => x.Score).ToList();
    }

    public static List<DriveInfo> SafeDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(x => { try { return x.IsReady && x.DriveType == DriveType.Fixed; } catch { return false; } })
                .ToList();
        }
        catch { return new List<DriveInfo>(); }
    }

    static WowCandidate ScoreWow(string dir)
    {
        try
        {
            var c = new WowCandidate { Path = dir };
            foreach (var f in Directory.EnumerateFiles(dir, "wow.exe", SearchOption.TopDirectoryOnly))
            { c.HasWowExe = true; break; }
            if (c.HasWowExe) c.Score += 4;

            if (File.Exists(Path.Combine(dir, "version.txt")))
            {
                try
                {
                    var ver = File.ReadAllText(Path.Combine(dir, "version.txt")).Trim();
                    c.Hint = ver.Length > 40 ? ver.Substring(0, 40) : ver;
                    if (c.Hint.StartsWith("3.3.5", StringComparison.OrdinalIgnoreCase)) c.Score += 3;
                }
                catch { }
            }

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var n = (Path.GetFileName(sub) ?? "").ToLowerInvariant();
                if (n == "data") { c.HasData = true; c.Score += 2; }
                else if (n == "interface") { c.HasInterface = true; c.Score += 2; }
                else if (n == "wtf") { c.HasWtf = true; c.Score += 1; }
                else if (n == "_retail_" || n == "_classic_") c.LooksModern = true;
            }
            if (c.LooksModern) c.Score -= 5;
            if (c.Score <= 0) return null;
            return c;
        }
        catch { return null; }
    }

    // ------------------------------------------------- existing DB services
    public static List<(string serviceName, string imagePath)> FindDbServices(Action<string> log)
    {
        var list = new List<(string, string)>();
        try
        {
            foreach (System.ServiceProcess.ServiceController svc in System.ServiceProcess.ServiceController.GetServices())
            {
                try
                {
                    var name = svc.ServiceName ?? "";
                    var display = svc.DisplayName ?? "";
                    if (!name.IsMatchDbName() && !display.IsMatchDbName()) continue;
                    string img = "";
                    try
                    {
                        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                            .OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name);
                        img = key?.GetValue("ImagePath") as string ?? "";
                    }
                    catch { }
                    list.Add((name, img));
                }
                catch { }
            }
        }
        catch (Exception ex) { log?.Invoke("Service scan failed: " + ex.Message); }
        return list;
    }

    static bool IsMatchDbName(this string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var l = s.ToLowerInvariant();
        return l.Contains("mysql") || l.Contains("mariadb");
    }
}
