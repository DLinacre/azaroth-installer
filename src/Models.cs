namespace AzarothInstaller;

public class GpuInfo
{
    public string Name = "";
    public long VideoBytes = 0;
    public string DriverVersion = "";
}

public class DriveStat
{
    public string Root = "";
    public string Label = "";
    public string FileSystem = "";
    public long FreeBytes = 0;
    public long TotalBytes = 0;
    public bool IsSystem = false;

    public string FreeText => $"{FreeBytes / 1073741824.0:0.#} GB free of {TotalBytes / 1073741824.0:0.#} GB";
}

public class SystemInfo
{
    public string CpuName = "";
    public int PhysicalCores;
    public int LogicalCores;
    public long RamBytes;
    public string OsVersion = "";
    public bool Is64Bit;
    public List<GpuInfo> Gpus = new();
    public List<DriveStat> Drives = new();
    public List<string> Warnings = new();

    public string RamGb => $"{RamBytes / 1073741824.0:0.#} GB";
}

public class WowCandidate
{
    public string Path = "";
    public int Score;
    public string Hint = "";
    public bool HasWowExe;
    public bool HasData;
    public bool HasInterface;
    public bool HasWtf;
    public bool LooksModern; // has _retail_ / _classic_ folders -> not a 3.3.5 client

    public override string ToString() =>
        $"[{Score} pts] {Path}{(LooksModern ? "  (looks like a MODERN client - not 3.3.5)" : "")}";
}

public class DbServerInfo
{
    public string Source = "";            // "bundled with repack" | "existing local server (service)" | "fresh install"
    public string MysqlExe = "";          // client executable
    public string MysqldExe = "";         // server executable (bundled) or ""
    public string MyIni = "";             // bundled config file (if any)
    public string Datadir = "";           // bundled data dir (if any)
    public string ServiceName = "";       // local service (if used)
    public string Host = "127.0.0.1";
    public int Port = 3306;
    public string Login = "root";
    public string Password = "";
    public bool ServerRunning;            // true if we left the DB server running
    public bool HasAzerDbs;               // azaroth_* databases already existed
    public string Note = "";
}

public class ServerLayout
{
    public string RepackZipPath = "";
    public string RepackSourceDesc = "";
    public string Root = "";              // extraction root
    public string ServerDir = "";         // folder containing worldserver.exe
    public string WorldserverExe = "";
    public string AuthserverExe = "";
    public string RealmserverExe = "";
    public string WorldConf = "";         // worldserver.conf (or .dist)
    public string AuthConf = "";
    public string RealmConf = "";
    public string DataDir = "";           // existing data/ folder (dbc/maps/...)
    public bool HasData;
    public List<string> SqlFiles = new(); // .sql/.sqlz/.gz dump files found
    public string BundledMysqld = "";
    public string BundledDatadir = "";
    public string BundledMyIni = "";
    public bool BundledDatadirPopulated;
    public string BundledPlayerbotsConf = "";
    public string StartBat = "";
    public List<string> Notes = new();

    public bool Found => !string.IsNullOrEmpty(WorldserverExe);
}

public class ModuleInfo
{
    public string Path = "";
    public string Name = "";
    public string Friendly = "";
    public bool Enabled = true;

    public override string ToString() => (Enabled ? "✓ " : "✗ ") + Friendly;
}

// Result of the whole run, passed to the summary screen.
public class InstallSummary
{
    public string InstallRoot = "";
    public string ServerDir = "";
    public string WowPath = "";
    public string DbSource = "";
    public string DbLogin = "";
    public string DbPassword = "";
    public string GmUser = "";
    public string GmPassword = "";
    public bool ServerVerified;
    public bool ClientFound;
    public List<string> Lines = new();
}
