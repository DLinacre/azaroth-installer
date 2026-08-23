using System.Diagnostics;

namespace AzarothInstaller;

public static class Misc
{
    public static string TempRoot
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "AzarothInstaller");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DesktopDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    // ------------------------------------------------ shortcuts (COM, no deps)
    public static bool CreateShortcut(string lnkPath, string target, string args, string workDir, string description, string iconExe)
    {
        try
        {
            var shellType = Type.GetTypeFromCLSID(new Guid("72C24DDC-D7A4-11D1-8056-00A0C9110051"));
            if (shellType == null) return false;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic lnk = shell.CreateShortcut(lnkPath);
            lnk.TargetPath = target;
            if (!string.IsNullOrEmpty(args)) lnk.Arguments = args;
            if (!string.IsNullOrEmpty(workDir)) lnk.WorkingDirectory = workDir;
            if (!string.IsNullOrEmpty(description)) lnk.Description = description;
            if (!string.IsNullOrEmpty(iconExe) && File.Exists(iconExe))
            {
                try { lnk.IconLocation = iconExe + ",0"; }
                catch { }
            }
            lnk.Save();
            return File.Exists(lnkPath);
        }
        catch { return false; }
    }

    public static bool ProcessRunning(string processName)
    {
        try { return Process.GetProcessesByName(processName).Any(); }
        catch { return false; }
    }

    public static void KillProcess(string processName)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                try { p.Kill(entireProcessTree: true); p.WaitForExit(8000); } catch { }
                try { p.Dispose(); } catch { }
            }
        }
        catch { }
        try { SqlUtil.RunShell("taskkill", $"/f /im {processName}.exe"); } catch { }
    }

    public static void AddFirewallRules(string serverName, int authPort, int realmPort, Action<string> log)
    {
        string ports = $"{authPort},{realmPort}";
        var (code, outp) = SqlUtil.RunShell("netsh",
            $"advfirewall firewall add rule name=\"{serverName} (WoW LAN server)\" dir=in action=allow protocol=TCP localport={ports} remoteip=LocalSubnet enable=yes");
        log?.Invoke("firewall: " + (code == 0 ? "LAN firewall rule added (TCP " + ports + " scoped to LocalSubnet)" : ("rule add reported: " + outp)));
    }
}
