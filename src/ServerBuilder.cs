using System.Diagnostics;

namespace AzarothInstaller;

/// <summary>
/// Orchestrates the whole installation:
///  repack zip -> extract -> find layout -> database -> data files -> playerbots
///  -> config files -> GM account -> launchers/shortcuts -> smoke test.
/// </summary>
public class ServerBuilder
{
    public AppConfig Cfg;
    public string InstallRoot;
    public Action<string> Log = _ => { };
    public IProgress<long> ExtractPct;
    public IProgress<DownloadProgress> DownloadProgress;
    public CancellationToken CancellationToken;

    public ServerBuilder(AppConfig cfg, string installRoot)
    {
        Cfg = cfg;
        InstallRoot = installRoot;
    }

    // ============================================================= repack zip
    public async Task<string> GetRepackZipAsync(string localZipPath, string urlOverride)
    {
        if (!string.IsNullOrWhiteSpace(localZipPath) && File.Exists(localZipPath))
        {
            Log("Using local repack zip: " + localZipPath);
            return localZipPath;
        }

        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(urlOverride)) urls.Add(urlOverride.Trim());
        if (Cfg.Downloads.ServerRepack != null) urls.AddRange(Cfg.Downloads.ServerRepack.Urls);
        urls = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (urls.Count == 0)
            throw new Exception("No repack source available. Paste a direct download URL or pick a local .zip file.");

        var dest = Path.Combine(Misc.TempRoot, "server-repack.zip");
        if (File.Exists(dest)) File.Delete(dest);
        var path = await Downloader.DownloadFirstAsync(urls, dest, DownloadProgress, CancellationToken, Log);
        Log("Downloaded repack (" + (new FileInfo(path).Length / 1048576.0) + " MB)");
        return path;
    }

    // ===================================================== extract + find layout
    public ServerLayout PrepareLayout(string repackZip)
    {
        Directory.CreateDirectory(InstallRoot);
        Log("Extracting to: " + InstallRoot);
        ZipEx.ExtractTo(repackZip, InstallRoot, ExtractPct, CancellationToken);
        Log("Extraction finished.");

        var layout = new ServerLayout
        {
            RepackZipPath = repackZip,
            Root = InstallRoot
        };
        FindLayout(layout);

        // Some repacks nest the actual server inside another zip.
        if (!layout.Found)
        {
            Log("worldserver.exe not found - checking for nested archives...");
            foreach (var inner in FindNestedZips(InstallRoot, 2))
            {
                Log("Extracting nested archive: " + Path.GetFileName(inner));
                try { ZipEx.ExtractTo(inner, InstallRoot, ExtractPct, CancellationToken); }
                catch (Exception ex) { Log("  nested zip failed: " + ex.Message); continue; }
                FindLayout(layout);
                if (layout.Found) break;
            }
        }

        if (!layout.Found)
            throw new Exception("Could not find worldserver.exe in the repack. This zip does not look like a Windows AzerothCore server package.");

        foreach (var n in layout.Notes) Log("layout: " + n);
        return layout;
    }

    List<string> FindNestedZips(string root, int depth)
    {
        var list = new List<string>();
        CollectZips(list, root, depth);
        return list;
    }

    void CollectZips(List<string> list, string root, int depth)
    {
        if (depth < 0 || list.Count > 20) return;
        try
        {
            foreach (var z in Directory.GetFiles(root, "*.zip"))
                list.Add(z);
            foreach (var d in Directory.GetDirectories(root))
                CollectZips(list, d, depth - 1);
        }
        catch { }
    }

    void FindLayout(ServerLayout layout)
    {
        int nodes = 0;
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        void Put(string key, string value) { if (!found.ContainsKey(key)) found[key] = value; }

        void Visit(string dir, int depth)
        {
            if (depth > 8 || nodes > 25000) return;
            nodes++;

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { return; }
            foreach (var f in files)
            {
                var n = Path.GetFileName(f).ToLowerInvariant();
                if (n == "worldserver.exe") Put("ws", f);
                else if (n == "authserver.exe") Put("as", f);
                else if (n == "realmserver.exe" || n == "realmd.exe") Put("rs", f);
                else if (n == "worldserver.conf" || n == "worldserver.conf.dist") Put("wc", f);
                else if (n == "authserver.conf" || n == "authserver.conf.dist") Put("ac", f);
                else if (n == "realmserver.conf" || n == "realmserver.conf.dist") Put("rc", f);
                else if (n == "playerbots.conf" || n == "playerbots.conf.dist") Put("pb", f);
                else if (n == "mysqld.exe") Put("mysqld", f);
                else if (n == "mysql.exe") Put("mysql", f);
                else if (n == "my.ini" || n == "my.cnf" || n == "mysql.ini") Put("myini", f);
                else if (n.EndsWith(".sql") || n.EndsWith(".sqlz") || n.EndsWith(".sql.gz")) PutSql(layout, f, n);
                else if (n.StartsWith("start") && n.EndsWith(".bat")) Put("bat", f);
            }

            // data dir = folder containing dbc + maps (AzerothCore layout)
            if (!found.ContainsKey("data") && found.ContainsKey("ws"))
            {
                try
                {
                    var subs = Directory.GetDirectories(dir);
                    var dbc = subs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), "dbc", StringComparison.OrdinalIgnoreCase));
                    var maps = subs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), "maps", StringComparison.OrdinalIgnoreCase));
                    if (dbc != null && maps != null)
                        Put("data", dir);
                }
                catch { }
            }

            // populated mysql data dir = #innodb_redo subdir or aria_log / ib_logfile0
            if (!found.ContainsKey("datadir"))
            {
                try
                {
                    var subs = Directory.GetDirectories(dir);
                    var redo = subs.FirstOrDefault(d => (Path.GetFileName(d) ?? "") == "#innodb_redo");
                    if (redo == null &&
                        (File.Exists(Path.Combine(dir, "aria_log.000")) || File.Exists(Path.Combine(dir, "ib_logfile0"))))
                        redo = dir;
                    if (redo != null) Put("datadir", dir);
                }
                catch { }
            }

            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (var d in dirs)
                Visit(d, depth + 1);
        }

        void PutSql(ServerLayout l, string file, string name)
        {
            // keep the file, but skip obvious non-dump files
            if (name.Length > 400) return;
            l.SqlFiles.Add(file);
        }

        Visit(InstallRoot, 0);

        if (found.TryGetValue("ws", out var ws))
        {
            layout.WorldserverExe = ws;
            layout.ServerDir = Path.GetDirectoryName(ws);
            layout.AuthserverExe = found.GetValueOrDefault("as") ??
                Path.Combine(layout.ServerDir, "authserver.exe");
            layout.RealmserverExe = found.GetValueOrDefault("rs") ??
                Path.Combine(layout.ServerDir, "realmserver.exe") ??
                Path.Combine(layout.ServerDir, "realmd.exe");
            if (!File.Exists(layout.AuthserverExe)) layout.AuthserverExe = "";
            if (!File.Exists(layout.RealmserverExe)) layout.RealmserverExe = "";

            layout.WorldConf = found.GetValueOrDefault("wc") ?? "";
            layout.AuthConf = found.GetValueOrDefault("ac") ?? "";
            layout.RealmConf = found.GetValueOrDefault("rc") ?? "";
            layout.BundledPlayerbotsConf = found.GetValueOrDefault("pb") ?? "";
            layout.StartBat = found.GetValueOrDefault("bat") ?? "";
            layout.BundledMysqld = found.GetValueOrDefault("mysqld") ?? "";
            layout.BundledMyIni = found.GetValueOrDefault("myini") ?? "";
            if (!string.IsNullOrEmpty(layout.BundledMyIni) && !File.Exists(layout.BundledMyIni)) layout.BundledMyIni = "";

            var dataDir = found.GetValueOrDefault("data");
            if (!string.IsNullOrEmpty(dataDir))
            {
                layout.DataDir = dataDir;
                layout.HasData = true;
            }

            var datadir = found.GetValueOrDefault("datadir");
            if (!string.IsNullOrEmpty(datadir))
            {
                layout.BundledDatadir = datadir;
                layout.BundledDatadirPopulated = IsDatadirPopulated(datadir);
            }

            if (!string.IsNullOrEmpty(layout.BundledMysqld))
                layout.Notes.Add("bundled MySQL found: " + layout.BundledMysqld +
                    (layout.BundledDatadirPopulated ? " (database pre-installed)" : " (empty, will be initialized)"));
            if (layout.HasData) layout.Notes.Add("game data folder present - no data download needed");
            if (layout.SqlFiles.Count > 0) layout.Notes.Add(layout.SqlFiles.Count + " SQL dump file(s) found");
            if (!string.IsNullOrEmpty(layout.BundledPlayerbotsConf)) layout.Notes.Add("playerbots.conf found in repack");
            if (layout.AuthserverExe == "") layout.Notes.Add("authserver.exe NOT found (expected)");
            if (layout.RealmserverExe == "") layout.Notes.Add("realmserver.exe NOT found (expected)");
        }
    }

    static bool IsDatadirPopulated(string datadir)
    {
        try
        {
            var mysqlSys = Path.Combine(datadir, "mysql");
            if (Directory.Exists(mysqlSys) &&
                Directory.EnumerateFiles(mysqlSys, "*", SearchOption.AllDirectories).Any())
                return true;
        }
        catch { }
        return false;
    }

    // ================================================================ database
    public DbServerInfo ResolveDatabase(ServerLayout layout, bool forceFresh)
    {
        var db = new DbServerInfo { Host = "127.0.0.1", Port = 3306 };

        // 1) parse the repack's own worldserver.conf for its DB credentials
        var (repAuth, repChar, repWorld, repLogin, repPass) = ParseRepackDb(layout);

        // 2) find a usable mysql client + server source
        if (!string.IsNullOrEmpty(layout.BundledMysqld))
        {
            db.Source = "MySQL bundled with the repack";
            db.MysqldExe = layout.BundledMysqld;
            db.MyIni = layout.BundledMyIni;
            db.Datadir = layout.BundledDatadir;
            db.MysqlExe = FindMysqlClient(layout) ?? "mysql";
            db.Login = Cfg.Database.RootLogin;
            db.Password = "";
            Log("Starting MySQL bundled with the repack...");
            if (!StartBundledMysqld(layout, db))
                throw new Exception("Could not start the MySQL server bundled with the repack. See log for details.");
            db.ServerRunning = true;
            db.Password = PickBundledPassword(db);
            Log("Bundled MySQL is up (root password: " + (db.Password == "" ? "<none>" : "<set>") + ")");
        }
        else if (TryUseLocalService(db))
        {
            db.Source = "existing database server on this PC (service " + db.ServiceName + ")";
            Log("Found and reusing existing database service: " + db.ServiceName + " - nothing to install.");
        }
        else
        {
            Log("No bundled or existing database server found - installing a fresh one...");
            InstallFreshDbServer(db);
            db.Source = "fresh MySQL 8 installed by this wizard";
        }

        if (!SqlUtil.ServerAlive(db))
            throw new Exception("Database server is not reachable at " + db.Host + ":" + db.Port +
                " after setup. Check the log.");

        // 3) databases: reuse existing ones whenever possible
        bool repDbsExist = SqlUtil.DatabaseExists(db, repAuth) &&
                           SqlUtil.DatabaseExists(db, repChar) &&
                           SqlUtil.DatabaseExists(db, repWorld);

        if (!forceFresh && repDbsExist)
        {
            db.Login = repLogin;
            db.Password = repPass;
            db.HasAzerDbs = true;
            db.Note = "Reusing the databases that ship with the repack (max efficiency - no import needed).";
            Log(db.Note);
            EnsureLoginWorks(db, repAuth);
            return db;
        }

        // otherwise: our databases
        db.Login = Cfg.Database.Login;
        db.Password = Cfg.Database.Password;
        bool anyExists = SqlUtil.DatabaseExists(db, Cfg.Database.AuthDb) ||
                         SqlUtil.DatabaseExists(db, Cfg.Database.CharactersDb) ||
                         SqlUtil.DatabaseExists(db, Cfg.Database.WorldDb);
        db.HasAzerDbs = anyExists;

        SqlUtil.CreateDatabase(db, Cfg.Database.AuthDb, Log);
        SqlUtil.CreateDatabase(db, Cfg.Database.CharactersDb, Log);
        SqlUtil.CreateDatabase(db, Cfg.Database.WorldDb, Log);
        SqlUtil.EnsureUserAndGrants(db, Cfg.Database, Log);

        // verify the dedicated login actually works (root may be locked down)
        var test = new DbServerInfo
        {
            Host = db.Host, Port = db.Port,
            MysqlExe = db.MysqlExe, Login = Cfg.Database.Login, Password = Cfg.Database.Password
        };
        if (!SqlUtil.ServerAlive(test))
        {
            Log("WARNING: dedicated user cannot connect - falling back to a working account in the config.");
            var rootNoPass = new DbServerInfo
            {
                Host = db.Host, Port = db.Port, MysqlExe = db.MysqlExe,
                Login = Cfg.Database.RootLogin, Password = ""
            };
            db.Login = Cfg.Database.RootLogin;
            if (SqlUtil.ServerAlive(rootNoPass))
                db.Password = "";
            else
                db.Password = Cfg.Database.RootPassword ?? "";
        }

        // 4) import SQL dumps if we have any and the world db looks empty
        if (!forceFresh && anyExists)
        {
            Log("Existing Azaroth databases detected - skipping SQL import (data preserved).");
        }
        else
        {
            ImportSqlDumps(db, layout);
        }

        return db;
    }

    (string auth, string chr, string world, string login, string pass) ParseRepackDb(ServerLayout layout)
    {
        string auth = "", chr = "", world = "", login = "", pass = "";
        try
        {
            var conf = layout.WorldConf;
            if (!string.IsNullOrEmpty(conf) && File.Exists(conf))
            {
                foreach (var kv in ParseConf(conf))
                {
                    switch (kv.Key)
                    {
                        case "LoginDatabase": auth = kv.Value; break;
                        case "CharacterDatabase": chr = kv.Value; break;
                        case "WorldDatabase": world = kv.Value; break;
                        case "LoginDatabaseLogin": login = kv.Value; break;
                        case "LoginDatabasePassword": pass = kv.Value; break;
                    }
                }
            }
        }
        catch { }
        return (auth, chr, world, login, pass);
    }

    void EnsureLoginWorks(DbServerInfo db, string sampleDb)
    {
        // make sure the login we recorded can actually read the databases
        if (SqlUtil.ServerAlive(db)) return;
        Log("Repack database user does not work directly - switching to a working account.");
        var test = new DbServerInfo { Host = db.Host, Port = db.Port, MysqlExe = db.MysqlExe, Login = db.Login, Password = "" };
        if (SqlUtil.ServerAlive(test)) { db.Password = ""; return; }
        foreach (var cand in new[] { "test", "root", Cfg.Database.RootPassword })
        {
            test.Password = cand ?? "";
            if (SqlUtil.ServerAlive(test)) { db.Password = cand ?? ""; return; }
        }
    }

    string PickBundledPassword(DbServerInfo db)
    {
        foreach (var cand in new[] { "", "test", "root", Cfg.Database.RootPassword ?? "" })
        {
            db.Password = cand;
            if (SqlUtil.ServerAlive(db)) return cand;
        }
        return "";
    }

    string FindMysqlClient(ServerLayout layout)
    {
        // bundled first
        if (!string.IsNullOrEmpty(layout.BundledMysqld))
        {
            var dir = Path.GetDirectoryName(layout.BundledMysqld);
            var c = Path.Combine(dir, "mysql.exe");
            if (File.Exists(c)) return c;
        }
        var sysdir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var p1 = Path.Combine(sysdir, "mysql.exe");
        if (File.Exists(p1)) return p1;
        foreach (var p in new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
            @"C:\Program Files\MariaDB 10.11\bin\mysql.exe"
        })
            if (File.Exists(p)) return p;
        return "mysql";
    }

    bool StartBundledMysqld(ServerLayout layout, DbServerInfo db)
    {
        if (SqlUtil.TryTcp(db.Host, db.Port, 800))
        {
            Log("Bundled MySQL already running on port " + db.Port);
            return true;
        }

        // build the right start line
        string startArgs;
        if (!string.IsNullOrEmpty(db.MyIni))
            startArgs = "--defaults-file=\"" + db.MyIni + "\"";
        else
        {
            var dd = string.IsNullOrEmpty(layout.BundledDatadir)
                ? Path.Combine(layout.ServerDir, "data")
                : layout.BundledDatadir;
            startArgs = "--datadir=\"" + dd + "\"";
        }

        Log("Starting bundled MySQL: " + Path.GetFileName(db.MysqldExe) + " " + startArgs);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = db.MysqldExe,
                Arguments = startArgs,
                WorkingDirectory = Path.GetDirectoryName(db.MysqldExe),
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Log("  start failed: " + ex.Message);
            return false;
        }

        var sw = Stopwatch.StartNew();
        bool ok = false;
        while (sw.ElapsedMilliseconds < 120000)
        {
            if (SqlUtil.TryTcp(db.Host, db.Port, 500)) { ok = true; break; }
            Thread.Sleep(1000);
        }
        if (!ok)
            Log("  MySQL did not open port " + db.Port + " within 120s (see mysql logs in the repack folder)");
        return ok;
    }

    bool TryUseLocalService(DbServerInfo db)
    {
        var services = SysProbe.FindDbServices(Log);
        foreach (var (name, img) in services)
        {
            Log("Found database service: " + name + (string.IsNullOrEmpty(img) ? "" : " (" + img + ")"));
            // derive the mysql client from the service image path
            string client = null;
            if (!string.IsNullOrEmpty(img))
            {
                var exe = img.Trim().Trim('"');
                try
                {
                    if (File.Exists(exe))
                    {
                        var bin = Path.GetDirectoryName(exe);
                        if (bin != null)
                        {
                            var c = Path.Combine(bin, "mysql.exe");
                            if (File.Exists(c)) client = c;
                        }
                    }
                }
                catch { }
            }
            db.ServiceName = name;
            db.MysqlExe = client ?? FindMysqlClient(null);

            var login = Cfg.Database.RootLogin;
            foreach (var cand in new[] { "", Cfg.Database.RootPassword ?? "", "test" })
            {
                db.Login = login;
                db.Password = cand;
                if (SqlUtil.ServerAlive(db))
                {
                    Log("Connected to " + name + " as " + login + " (password " + (cand == "" ? "<none>" : "<set>") + ")");
                    return true;
                }
            }
        }
        return false;
    }

    void InstallFreshDbServer(DbServerInfo db)
    {
        var urls = Cfg.Downloads.DatabaseServer?.Urls ?? new List<string>();
        if (urls.Count == 0)
            throw new Exception("No database server found on this PC and no download URL is configured (config.json -> downloads.databaseServer).");

        var msi = Path.Combine(Misc.TempRoot, "mysql-setup.msi");
        if (File.Exists(msi)) File.Delete(msi);
        Downloader.DownloadFirstAsync(urls, msi, DownloadProgress, CancellationToken, Log).GetAwaiter().GetResult();

        Log("Installing MySQL silently (this can take a couple of minutes)...");
        var (code, outp) = SqlUtil.RunShell("msiexec",
            $"/i \"{msi}\" /qn /norestart /l*v \"{Path.Combine(Misc.TempRoot, "mysql-msi.log")}\" MYSQL_ROOT_PASSWORD=\"\"");
        if (code != 0)
            Log("msiexec exit code " + code + ": " + outp);

        // service name is usually MySQL80 / MySQL84 / MySQL
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 90000)
        {
            foreach (var cand in new[] { "MySQL80", "MySQL84", "MySQL82", "MySQL" })
            {
                try
                {
                    using var svc = new System.ServiceProcess.ServiceController(cand);
                    if (svc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                        svc.Start();
                    if (svc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        db.ServiceName = cand;
                        db.MysqlExe = FindMysqlClient(null);
                        db.Login = Cfg.Database.RootLogin;
                        db.Password = "";
                        if (SqlUtil.ServerAlive(db))
                        {
                            Log("MySQL service " + cand + " is running.");
                            return;
                        }
                    }
                }
                catch { }
            }
            Task.Delay(3000).GetAwaiter().GetResult();
        }
        throw new Exception("Fresh MySQL installation finished but the server did not start. See " + Path.Combine(Misc.TempRoot, "mysql-msi.log"));
    }

    void ImportSqlDumps(DbServerInfo db, ServerLayout layout)
    {
        if (layout.SqlFiles.Count == 0)
        {
            Log("No SQL dump files found in the repack - skipping import. If the world server refuses to start, the repack may expect its own prebuilt database.");
            return;
        }

        // map each dump to a target database by folder/file name
        var byDb = new Dictionary<string, List<string>>
        {
            { Cfg.Database.AuthDb, new List<string>() },
            { Cfg.Database.CharactersDb, new List<string>() },
            { Cfg.Database.WorldDb, new List<string>() }
        };
        foreach (var f in layout.SqlFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var lower = f.ToLowerInvariant();
            if (lower.Contains("db_auth") || lower.Contains("auth\\base") || lower.Contains("/base/") && lower.Contains("auth"))
                byDb[Cfg.Database.AuthDb].Add(f);
            else if (lower.Contains("character"))
                byDb[Cfg.Database.CharactersDb].Add(f);
            else if (lower.Contains("world"))
                byDb[Cfg.Database.WorldDb].Add(f);
            else
                Log("  no clear target for " + Path.GetFileName(f) + " - skipping");
        }

        int imported = 0, failed = 0;
        foreach (var (dbName, files) in byDb)
        {
            foreach (var f in files)
            {
                var target = new DbServerInfo
                {
                    Host = db.Host, Port = db.Port, MysqlExe = db.MysqlExe,
                    Login = Cfg.Database.Login, Password = Cfg.Database.Password
                };
                if (!SqlUtil.ServerAlive(target))
                {
                    target.Login = db.Login;
                    target.Password = db.Password;
                }
                var code = SqlUtil.ImportSqlFile(target, dbName, f, Log);
                if (code == 0) imported++;
                else failed++;
            }
        }
        Log($"SQL import complete: {imported} file(s) ok, {failed} failed.");
        if (imported == 0 && failed > 0)
            Log("WARNING: all SQL imports failed - the server may not start without a database.");
    }

    // ================================================================== data
    public async Task EnsureGameDataAsync(ServerLayout layout)
    {
        if (layout.HasData)
        {
            Log("Game data (dbc/maps/vmaps/mmaps) already present in the repack - download skipped.");
            return;
        }

        var urls = Cfg.Downloads.AcData?.Urls ?? new List<string>();
        if (urls.Count == 0)
        {
            Log("WARNING: no game data in the repack and no download URL configured. The world server will NOT start without data files (dbc/maps/vmaps/mmaps).");
            return;
        }

        var zip = Path.Combine(Misc.TempRoot, "ac-data.zip");
        if (File.Exists(zip)) File.Delete(zip);
        await Downloader.DownloadFirstAsync(urls, zip, DownloadProgress, CancellationToken, Log);

        Log("Extracting game data...");
        var tmpExtract = Path.Combine(Misc.TempRoot, "ac-data-extract");
        if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true);
        Directory.CreateDirectory(tmpExtract);
        ZipEx.ExtractTo(zip, tmpExtract, ExtractPct, CancellationToken);

        // figure out where "data" ends up
        var dataSrc = tmpExtract;
        var inner = Path.Combine(tmpExtract, "data");
        if (Directory.Exists(inner))
            dataSrc = inner;
        else if (!Directory.Exists(Path.Combine(tmpExtract, "dbc")) && !Directory.Exists(Path.Combine(tmpExtract, "maps")))
        {
            // maybe nested one level deeper
            foreach (var d in Directory.GetDirectories(tmpExtract))
            {
                if (Directory.Exists(Path.Combine(d, "dbc")) || Directory.Exists(Path.Combine(d, "maps")))
                { dataSrc = d; break; }
            }
        }

        var dataDst = Path.Combine(layout.ServerDir, "data");
        MoveTree(dataSrc, dataDst);
        layout.DataDir = dataDst;
        layout.HasData = true;
        Log("Game data ready at " + dataDst);
        try { File.Delete(zip); } catch { }
    }

    static void MoveTree(string src, string dst)
    {
        if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
        {
            var target = Path.Combine(dst, Path.GetFileName(f));
            if (!File.Exists(target)) File.Move(f, target);
        }
        foreach (var d in Directory.GetDirectories(src))
            MoveTree(d, Path.Combine(dst, Path.GetFileName(d)));
        try { Directory.Delete(src, true); } catch { }
    }

    // ============================================================== playerbots
    public async Task EnsurePlayerBotsAsync(ServerLayout layout)
    {
        if (!Cfg.PlayerBots.Enabled)
        {
            Log("PlayerBots disabled in config.json - skipping.");
            return;
        }

        // make sure the module is listed in worldserver.conf
        var confPath = layout.WorldConf;
        if (!string.IsNullOrEmpty(confPath) && File.Exists(confPath))
        {
            var text = File.ReadAllText(confPath);
            if (!text.Contains("Mod_PlayerBots", StringComparison.OrdinalIgnoreCase))
                Log("NOTE: 'Mod_PlayerBots' not found in worldserver.conf. It is added during the configuration step.");
        }

        // make sure playerbots.conf exists next to the server binaries
        var target = Path.Combine(layout.ServerDir, "playerbots.conf");
        if (File.Exists(target))
        {
            Log("playerbots.conf already present.");
            return;
        }

        if (!string.IsNullOrEmpty(layout.BundledPlayerbotsConf))
        {
            File.Copy(layout.BundledPlayerbotsConf, target, true);
            Log("playerbots.conf copied from the repack.");
            return;
        }

        var urls = Cfg.Downloads.PlayerBotsConf?.Urls ?? new List<string>();
        if (urls.Count == 0)
        {
            Log("WARNING: no playerbots.conf found in the repack and no download URL configured.");
            return;
        }
        try
        {
            await Downloader.DownloadFirstAsync(urls, target, DownloadProgress, CancellationToken, Log);
            Log("playerbots.conf downloaded from the mod-playerbots repository.");
        }
        catch (Exception ex)
        {
            Log("playerbots.conf download failed: " + ex.Message +
                " (playerbots will use the module's built-in defaults)");
        }
    }

    // ================================================================= config
    public void WriteConfigs(ServerLayout layout, DbServerInfo db)
    {
        // make sure .conf exists (copy from .dist when needed)
        layout.WorldConf = EnsureConf(layout.WorldConf, layout.ServerDir, "worldserver.conf");
        layout.AuthConf = EnsureConf(layout.AuthConf, layout.ServerDir, "authserver.conf");
        layout.RealmConf = EnsureConf(layout.RealmConf, layout.ServerDir, "realmserver.conf");

        var world = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var auth = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var realm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // --- database credentials. Current AzerothCore uses
        //     LoginDatabaseInfo = "host;port;user;pass;db"
        //     while older repacks use the classic 4-key groups.
        //     We write whichever style the repack's config actually uses.
        string authInfo = $"{db.Host};{db.Port};{db.Login};{db.Password};{Cfg.Database.AuthDb}";
        string worldInfo = $"{db.Host};{db.Port};{db.Login};{db.Password};{Cfg.Database.WorldDb}";
        string charInfo = $"{db.Host};{db.Port};{db.Login};{db.Password};{Cfg.Database.CharactersDb}";

        if (!string.IsNullOrEmpty(layout.WorldConf) && File.Exists(layout.WorldConf) &&
            ParseConf(layout.WorldConf).ContainsKey("LoginDatabaseInfo"))
        {
            world["LoginDatabaseInfo"] = "\"" + authInfo + "\"";
            world["WorldDatabaseInfo"] = "\"" + worldInfo + "\"";
            world["CharacterDatabaseInfo"] = "\"" + charInfo + "\"";
            Log("worldserver.conf: new 'DatabaseInfo' config style detected - credentials written accordingly.");
        }
        else
        {
            world["LoginDatabaseAddress"] = db.Host;
            world["CharacterDatabaseAddress"] = db.Host;
            world["WorldDatabaseAddress"] = db.Host;
            world["LoginDatabase"] = Cfg.Database.AuthDb;
            world["LoginDatabaseLogin"] = db.Login;
            world["LoginDatabasePassword"] = db.Password;
            world["CharacterDatabase"] = Cfg.Database.CharactersDb;
            world["CharacterDatabaseLogin"] = db.Login;
            world["CharacterDatabasePassword"] = db.Password;
            world["WorldDatabase"] = Cfg.Database.WorldDb;
            world["WorldDatabaseLogin"] = db.Login;
            world["WorldDatabasePassword"] = db.Password;
        }
        world["ServerName"] = Cfg.ServerName;
        world["ServerMotd"] = Cfg.ServerName + " - powered by AzerothCore + PlayerBots";
        world["MaxPlayers"] = Cfg.Server.MaxPlayers.ToString();
        world["InstanceID"] = "1";
        string dataPath = (layout.DataDir ?? Path.Combine(layout.ServerDir, "data")).Replace("\\", "/");
        world["DataDir"] = "\"" + dataPath + "\"";

        // playerbots module in the ModuleList
        if (Cfg.PlayerBots.Enabled)
        {
            var existing = world.TryGetValue("ModuleList", out var ml) ? ml : "";
            var modules = existing
                .Replace("\"", "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => m.Trim().Trim('"'))
                .Where(m => m.Length > 0)
                .ToList();
            if (!modules.Any(m => m.Equals("Mod_PlayerBots", StringComparison.OrdinalIgnoreCase)))
                modules.Add("Mod_PlayerBots");
            world["ModuleList"] = string.Join(", ", modules);
            Log("ModuleList = " + world["ModuleList"]);
        }

        auth["Address"] = Cfg.Server.AuthAddress;
        auth["ListenAddress"] = Cfg.Server.ListenAddress;
        auth["Port"] = Cfg.Server.AuthPort.ToString();
        if (!string.IsNullOrEmpty(layout.AuthConf) && File.Exists(layout.AuthConf) &&
            ParseConf(layout.AuthConf).ContainsKey("LoginDatabaseInfo"))
            auth["LoginDatabaseInfo"] = "\"" + authInfo + "\"";
        else
        {
            auth["LoginDatabaseAddress"] = db.Host;
            auth["LoginDatabase"] = Cfg.Database.AuthDb;
            auth["LoginDatabaseLogin"] = db.Login;
            auth["LoginDatabasePassword"] = db.Password;
        }

        realm["Address"] = Cfg.Server.RealmAddress;
        realm["Port"] = Cfg.Server.RealmPort.ToString();
        if (!string.IsNullOrEmpty(layout.RealmConf) && File.Exists(layout.RealmConf) &&
            ParseConf(layout.RealmConf).ContainsKey("LoginDatabaseInfo"))
            realm["LoginDatabaseInfo"] = "\"" + authInfo + "\"";
        else
        {
            realm["LoginDatabaseAddress"] = db.Host;
            realm["LoginDatabase"] = Cfg.Database.AuthDb;
            realm["LoginDatabaseLogin"] = db.Login;
            realm["LoginDatabasePassword"] = db.Password;
        }

        if (!string.IsNullOrEmpty(layout.WorldConf)) { WriteConf(layout.WorldConf, world); Log("worldserver.conf written."); }
        if (!string.IsNullOrEmpty(layout.AuthConf)) { WriteConf(layout.AuthConf, auth); Log("authserver.conf written."); }
        else Log("authserver.conf not found - AzerothCore expects one next to authserver.exe.");
        if (!string.IsNullOrEmpty(layout.RealmConf)) { WriteConf(layout.RealmConf, realm); Log("realmserver.conf written."); }
    }

    string EnsureConf(string confPath, string serverDir, string defaultName)
    {
        if (!string.IsNullOrEmpty(confPath) && File.Exists(confPath))
        {
            if (confPath.EndsWith(".dist", StringComparison.OrdinalIgnoreCase))
            {
                var real = confPath.Substring(0, confPath.Length - 5);
                if (!File.Exists(real)) File.Copy(confPath, real);
                return real;
            }
            return confPath;
        }
        var candidate = Path.Combine(serverDir, defaultName);
        if (File.Exists(candidate)) return candidate;
        var dist = Path.Combine(serverDir, defaultName + ".dist");
        if (File.Exists(dist))
        {
            File.Copy(dist, candidate);
            return candidate;
        }
        return "";
    }

    public static Dictionary<string, string> ParseConf(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#") || line.StartsWith(";") || line.Length == 0) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim();
            var val = line.Substring(eq + 1).Trim().Trim('"');
            dict[key] = val;
        }
        return dict;
    }

    static void WriteConf(string path, Dictionary<string, string> updates)
    {
        var lines = new List<string>();
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#") || line.StartsWith(";") || line.Length == 0 || !line.Contains('='))
            {
                lines.Add(raw);
                continue;
            }
            var eq = line.IndexOf('=');
            var key = line.Substring(0, eq).Trim();
            if (updates.TryGetValue(key, out var val))
            {
                lines.Add(key + " = " + val);
                handled.Add(key);
            }
            else
            {
                lines.Add(raw);
            }
        }
        foreach (var (k, v) in updates)
            if (!handled.Contains(k)) lines.Add(k + " = " + v);

        File.WriteAllLines(path, lines);
    }

    // ======================================================= world options
    public List<ModuleInfo> DetectModules(ServerLayout layout)
    {
        var list = new List<ModuleInfo>();
        var disabledDir = Path.Combine(layout.ServerDir, "modules_disabled");
        try
        {
            foreach (var f in Directory.GetFiles(layout.ServerDir, "*.dll"))
            {
                var n = Path.GetFileNameWithoutExtension(f);
                if (IsCoreDll(n)) continue;
                list.Add(new ModuleInfo { Path = f, Name = n, Friendly = FriendlyName(n), Enabled = true });
            }
        }
        catch { }
        try
        {
            if (Directory.Exists(disabledDir))
                foreach (var f in Directory.GetFiles(disabledDir, "*.dll"))
                {
                    var n = Path.GetFileNameWithoutExtension(f);
                    list.Add(new ModuleInfo { Path = f, Name = n, Friendly = FriendlyName(n), Enabled = false });
                }
        }
        catch { }
        return list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static bool IsCoreDll(string n)
    {
        if (n.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) || n.StartsWith("ext-", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var p in new[]
        {
            "boost", "lib", "zlib", "lzma", "luajit", "lua5", "krb5", "ssleay", "libeay",
            "vcruntime", "msvcp", "ucrt", "comctl32", "wtsapi32", "winmm", "version",
            "dbghelp", "icudt", "icuin", "icuuc", "libcurl", "concrt", "msvcmfc"
        })
            if (n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string FriendlyName(string n)
        => n.StartsWith("Mod_", StringComparison.OrdinalIgnoreCase) ? n.Substring(4) : n;

    /// <summary>
    /// Applies everything the user picked in the World & Options step.
    /// Every change is defensive: a key is only touched when the repack's config
    /// actually uses it, and every step fails soft with a log message.
    /// </summary>
    public void ApplyWorldOptions(ServerLayout layout, DbServerInfo db, WorldOptionsConfig opts,
        List<ModuleInfo> modules, string wowPath, string locale)
    {
        try { SetRealmIdentity(db, opts.RealmName); }
        catch (Exception ex) { Log("realm identity: " + ex.Message); }

        try { ApplyRates(layout, opts); }
        catch (Exception ex) { Log("rates: " + ex.Message); }

        try { ApplyModules(layout, opts, modules); }
        catch (Exception ex) { Log("modules: " + ex.Message); }

        try { ApplyPlayerBotsConf(layout, opts); }
        catch (Exception ex) { Log("playerbots.conf: " + ex.Message); }

        try
        {
            if (opts.GmGenieAddon && !string.IsNullOrEmpty(wowPath) && Directory.Exists(wowPath))
                InstallGmGenie(wowPath);
        }
        catch (Exception ex) { Log("GM Genie: " + ex.Message); }

        _ = locale; // realmlist locale is handled by WriteLaunchers
    }

    void SetRealmIdentity(DbServerInfo db, string name)
    {
        name = (name ?? "Azaroth").Trim();
        if (name.Length > 15) name = name.Substring(0, 15);
        var n = name.Replace("'", "''");
        var a = Cfg.Server.AuthAddress.Replace("'", "''");
        SqlUtil.Query(db, Cfg.Database.AuthDb,
            "UPDATE realm SET name='" + n + "', address='" + a + "' WHERE id=(SELECT MIN(id) FROM realm);");
        var (code, outp) = SqlUtil.Query(db, Cfg.Database.AuthDb, "SELECT name FROM realm ORDER BY id LIMIT 1;");
        if (code == 0 && outp.Trim() == name)
            Log("Realm '" + name + "' registered in the database (shown on the character-select screen).");
        else
            Log("NOTE: could not verify the realm name update - the character screen may keep the repack's default name. (" +
                (string.IsNullOrWhiteSpace(outp) ? "no realm rows" : outp.Trim()) + ")");
    }

    void ApplyRates(ServerLayout layout, WorldOptionsConfig opts)
    {
        if (string.IsNullOrEmpty(layout.WorldConf) || !File.Exists(layout.WorldConf)) return;
        var parsed = ParseConf(layout.WorldConf);
        var upd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string num(double d) => d == Math.Floor(d) ? ((long)d).ToString() : d.ToString("0.##");

        if (parsed.ContainsKey("Rate.XP.Kill"))
        {
            foreach (var k in new[] { "Rate.XP.Kill", "Rate.XP.Quest", "Rate.XP.Quest.DF", "Rate.XP.Explore", "Rate.XP.Pet" })
                if (parsed.ContainsKey(k)) upd[k] = num(opts.XpRate);
        }
        else if (parsed.ContainsKey("Rate.XP"))
        {
            upd["Rate.XP"] = num(opts.XpRate);
        }
        if (parsed.ContainsKey("Rate.Honor")) upd["Rate.Honor"] = num(opts.HonorRate);
        if (parsed.ContainsKey("Rate.Gold")) upd["Rate.Gold"] = num(opts.GoldRate);
        if (parsed.ContainsKey("MaxPlayerLevel")) upd["MaxPlayerLevel"] = opts.LevelCap.ToString();

        if (upd.Count == 0)
        {
            Log("No rate / level-cap keys found in this repack's worldserver.conf - progression options skipped.");
            return;
        }
        WriteConf(layout.WorldConf, upd);
        Log("Applied world options: " + string.Join(", ", upd.Select(kv => kv.Key + "=" + kv.Value)));
    }

    void ApplyModules(ServerLayout layout, WorldOptionsConfig opts, List<ModuleInfo> selected)
    {
        var disabledDir = Path.Combine(layout.ServerDir, "modules_disabled");
        var serverDlls = Directory.Exists(layout.ServerDir) ? Directory.GetFiles(layout.ServerDir, "*.dll") : Array.Empty<string>();

        foreach (var m in selected ?? new List<ModuleInfo>())
        {
            if (!File.Exists(m.Path)) continue;
            var dir = Path.GetDirectoryName(m.Path);
            try
            {
                if (m.Enabled && string.Equals(dir, disabledDir, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(m.Path, Path.Combine(layout.ServerDir, Path.GetFileName(m.Path)));
                    Log("module enabled: " + m.Friendly);
                }
                else if (!m.Enabled && !string.Equals(dir, disabledDir, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(disabledDir);
                    File.Move(m.Path, Path.Combine(disabledDir, Path.GetFileName(m.Path)));
                    Log("module disabled: " + m.Friendly);
                }
            }
            catch (Exception ex)
            {
                Log("module " + m.Friendly + " could not be moved: " + ex.Message);
            }
        }

        // Old-style repacks: add missing Mod_* entries to ModuleList (auto-load repacks have no such key).
        if (!string.IsNullOrEmpty(layout.WorldConf) && File.Exists(layout.WorldConf))
        {
            var parsed = ParseConf(layout.WorldConf);
            if (parsed.TryGetValue("ModuleList", out var ml))
            {
                var mods = ml.Replace("\"", "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.Trim().Trim('"'))
                    .Where(x => x.Length > 0)
                    .ToList();
                var added = new List<string>();
                foreach (var m in (selected ?? new List<ModuleInfo>())
                             .Where(x => x.Enabled && x.Name.StartsWith("Mod_", StringComparison.OrdinalIgnoreCase) &&
                                          Directory.Exists(Path.GetDirectoryName(x.Path)) &&
                                          Path.GetFileName(x.Path) != null &&
                                          serverDlls.Contains(x.Path)))
                {
                    if (!mods.Any(x => x.Equals(m.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        mods.Add(m.Name);
                        added.Add(m.Name);
                    }
                }
                if (added.Count > 0)
                {
                    WriteConf(layout.WorldConf, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ModuleList"] = string.Join(", ", mods)
                    });
                    Log("ModuleList now includes: " + string.Join(", ", added));
                }
            }
        }

        // extra prebuilt modules from direct links
        foreach (var em in opts?.ExtraModules ?? new List<ExtraModule>())
        {
            if (string.IsNullOrWhiteSpace(em.Url)) continue;
            try
            {
                InstallExtraModule(layout, em);
            }
            catch (Exception ex)
            {
                Log("extra module '" + (string.IsNullOrEmpty(em.Name) ? em.Url : em.Name) + "' failed: " + ex.Message);
            }
        }
    }

    void InstallExtraModule(ServerLayout layout, ExtraModule em)
    {
        string urlFileName = "";
        try
        {
            urlFileName = Path.GetFileName(new Uri(em.Url).Segments.Last(s => !s.EndsWith("/")).Trim());
        }
        catch { }
        var isZip = urlFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var file = Path.Combine(Misc.TempRoot, "module_" + Guid.NewGuid().ToString("N") + (isZip ? ".zip" : ".file"));
        Downloader.DownloadFirstAsync(new List<string> { em.Url }, file, DownloadProgress, CancellationToken, Log)
            .GetAwaiter().GetResult();
        if (isZip)
        {
            ZipEx.ExtractTo(file, layout.ServerDir, ExtractPct, CancellationToken);
        }
        else
        {
            if (string.IsNullOrEmpty(urlFileName) || urlFileName.Length > 200) urlFileName = "module.dll";
            File.Copy(file, Path.Combine(layout.ServerDir, urlFileName), true);
        }
        Log("extra module installed into the server folder: " + (string.IsNullOrEmpty(em.Name) ? em.Url : em.Name));
    }

    void ApplyPlayerBotsConf(ServerLayout layout, WorldOptionsConfig opts)
    {
        var conf = Path.Combine(layout.ServerDir, "playerbots.conf");
        if (!File.Exists(conf) && !string.IsNullOrEmpty(layout.BundledPlayerbotsConf) && File.Exists(layout.BundledPlayerbotsConf))
            conf = layout.BundledPlayerbotsConf;
        if (!File.Exists(conf))
        {
            Log("playerbots.conf not present - bot behaviour options skipped (module will use its built-in defaults).");
            return;
        }

        var want = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AiPlayerbot.MinRandomBots"] = opts.RandomBots.ToString(),
            ["AiPlayerbot.MaxRandomBots"] = opts.RandomBots.ToString(),
            ["AiPlayerbot.RandomBotAutologin"] = opts.BotsAutologin ? "1" : "0",
            ["AiPlayerbot.AddClassAccountPoolSize"] = opts.AddClassPool.ToString(),
            ["AiPlayerbot.MaxAddedBots"] = opts.MaxAddedBots.ToString(),
            ["AiPlayerbot.RandomBotGuildCount"] = opts.BotGuilds.ToString(),
            ["AiPlayerbot.DisabledWithoutRealPlayer"] = opts.BotsOnlyWhenPlayerOnline ? "1" : "0"
        };
        var parsed = ParseConf(conf);
        var upd = want.Where(kv => parsed.ContainsKey(kv.Key))
                      .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        if (upd.Count == 0)
        {
            Log("playerbots.conf has no recognizable AiPlayerbot.* keys - bot tuning skipped.");
            return;
        }
        WriteConf(conf, upd);
        Log("PlayerBots tuned: " + string.Join(", ", upd.Select(kv => kv.Key.Split('.').Last() + "=" + kv.Value)));
    }

    void InstallGmGenie(string wowPath)
    {
        var addons = Path.Combine(wowPath, "Interface", "AddOns");
        Directory.CreateDirectory(addons);
        if (Directory.Exists(Path.Combine(addons, "GMGenie")))
        {
            Log("GM Genie addon already installed in the client.");
            return;
        }
        var zip = Path.Combine(Misc.TempRoot, "gmgenie.zip");
        if (File.Exists(zip)) File.Delete(zip);
        Downloader.DownloadFirstAsync(
            new List<string> { "https://github.com/azerothcore/GMGenie/archive/refs/heads/master.zip" },
            zip, DownloadProgress, CancellationToken, Log).GetAwaiter().GetResult();
        var tmp = Path.Combine(Misc.TempRoot, "gmgenie-extract");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        Directory.CreateDirectory(tmp);
        ZipEx.ExtractTo(zip, tmp, ExtractPct, CancellationToken);

        string tocFolder = null;
        foreach (var f in Directory.EnumerateFiles(tmp, "*.toc", SearchOption.AllDirectories))
        { tocFolder = Path.GetDirectoryName(f); break; }
        if (tocFolder == null)
        {
            Log("GM Genie archive had no .toc file - addon skipped.");
            return;
        }
        MoveTree(tocFolder, Path.Combine(addons, "GMGenie"));
        Log("GM Genie addon installed: " + Path.Combine(addons, "GMGenie") + " (enable it in your client's addon menu).");
    }

    // =============================================================== gm account
    public void CreateGmAccount(DbServerInfo db)
    {
        var user = Cfg.Server.GmUsername;
        var pass = Cfg.Server.GmPassword;
        var sha = SqlUtil.WoWPassSha1(user, pass);

        // account (try modern AzerothCore sha_pass_hash first, then legacy sha)
        var (code, outp) = SqlUtil.Query(db, Cfg.Database.AuthDb,
            $"INSERT INTO account (username, sha_pass_hash, email, joindate, last_ip) VALUES ('{user.ToUpperInvariant()}', '{sha}', '{user}@azaroth.local', NOW(), '127.0.0.1') " +
            $"ON DUPLICATE KEY UPDATE sha_pass_hash='{sha}';");
        if (code != 0)
        {
            SqlUtil.Query(db, Cfg.Database.AuthDb,
                $"INSERT INTO account (username, sha, email, reg_time, last_ip) VALUES ('{user.ToUpperInvariant()}', '{sha}', '{user}@azaroth.local', UNIX_TIMESTAMP(), '127.0.0.1') " +
                $"ON DUPLICATE KEY UPDATE sha='{sha}';");
        }

        var acctId = SqlUtil.GetIntValue(db, Cfg.Database.AuthDb, $"SELECT id FROM account WHERE username='{user}' OR username='{user.ToUpperInvariant()}'");
        if (acctId != null)
        {
            // Set GM permissions in account_access (AzerothCore default)
            SqlUtil.Query(db, Cfg.Database.AuthDb,
                $"INSERT INTO account_access (id, SecurityLevel, RealmID) VALUES ({acctId}, 3, -1) ON DUPLICATE KEY UPDATE SecurityLevel=3;");
            // Legacy fallbacks if columns exist
            SqlUtil.Query(db, Cfg.Database.AuthDb, $"UPDATE account SET gmsec=3 WHERE id={acctId};");
            SqlUtil.Query(db, Cfg.Database.AuthDb, $"UPDATE account SET gmlevel=3 WHERE id={acctId};");
            Log("GM account ready: " + user + " / " + pass + " (GM level 3)");
        }
        else
        {
            Log("Could not resolve GM account id: " + outp);
            return;
        }

        // character (Human Mage starting at Northshire Abbey / Stormwind, level 1)
        var name = (Cfg.Server.GmCharacterName ?? "Azaroth").Replace("'", "''").Replace("\\", "");
        if (name.Length > 12) name = name.Substring(0, 12);

        SqlUtil.Query(db, Cfg.Database.CharactersDb, $"DELETE FROM characters WHERE account={acctId} AND name='{name}';");

        var (code2, outp2) = SqlUtil.Query(db, Cfg.Database.CharactersDb,
            $"INSERT INTO characters (account, name, race, class, gender, skin, face, hairStyle, hairColor, facialStyle, level, xp, money, position_x, position_y, position_z, orientation, map, zone) " +
            $"VALUES ({acctId}, '{name}', 1, 8, 0, 0, 0, 0, 0, 0, 1, 0, 0, -8913.5, 555.8, 93.9, 0.7, 0, 1519);");
        if (code2 == 0)
            Log("GM character created: '" + name + "' (Human Mage, Stormwind)");
        else
            Log("GM character insert note: " + outp2.Replace("\n", " "));
    }

    // ================================================================ launchers
    public void WriteLaunchers(ServerLayout layout, DbServerInfo db, string wowPath, string locale)
    {
        var sd = layout.ServerDir;
        var serverName = Cfg.ServerName;

        var startLines = new List<string>
        {
            "@echo off",
            "title " + serverName + " Core - Server",
            "cd /d \"%~dp0\"",
            "echo ================================================",
            "echo    " + serverName + " Core - starting server",
            "echo    Keep this window open while playing.",
            "echo ================================================",
            ""
        };

        if (!string.IsNullOrEmpty(db.MysqldExe))
        {
            var mysqldDir = Path.GetDirectoryName(db.MysqldExe) ?? sd;
            var startArgs = !string.IsNullOrEmpty(db.MyIni)
                ? $"--defaults-file=\"{db.MyIni}\""
                : $"--datadir=\"{Path.GetRelativePath(mysqldDir, string.IsNullOrEmpty(db.Datadir) ? Path.Combine(sd, "data") : db.Datadir)}\"";
            startLines.AddRange(new[]
            {
                "echo Starting MySQL...",
                "set /a aztries=0",
                ":mysqlcheck",
                "netstat -an | findstr /r /c:\":3306 .*LISTENING\" >nul 2>&1",
                "if %errorlevel%==0 goto mysqlrunning",
                "set /a aztries+=1",
                "if %aztries% geq 30 goto mysqlgiveup",
                "cd /d \"" + mysqldDir + "\"",
                "start \"" + serverName + " MySQL\" /MIN \"" + db.MysqldExe + "\" " + startArgs,
                "timeout /t 5 >nul",
                "goto mysqlcheck",
                ":mysqlgiveup",
                "echo MySQL did not open port 3306 - check the mysql logs in the server folder.",
                ":mysqlrunning",
                "cd /d \"%~dp0\"",
                ""
            });
        }

        if (!string.IsNullOrEmpty(layout.AuthserverExe))
            startLines.Add("start \"" + serverName + " Auth\" authserver.exe");
        if (!string.IsNullOrEmpty(layout.RealmserverExe))
            startLines.Add("start \"" + serverName + " Realm\" " + Path.GetFileName(layout.RealmserverExe));
        startLines.AddRange(new[]
        {
            "timeout /t 3 >nul",
            "worldserver.exe",
            ""
        });

        File.WriteAllText(Path.Combine(sd, "Start_Azaroth.bat"), string.Join("\r\n", startLines));
        Log("Start_Azaroth.bat written.");

        var stopLines = new List<string>
        {
            "@echo off",
            "echo Stopping " + serverName + " Core...",
            "taskkill /f /im worldserver.exe >nul 2>&1",
            "taskkill /f /im realmserver.exe >nul 2>&1",
            "taskkill /f /im realmd.exe >nul 2>&1",
            "taskkill /f /im authserver.exe >nul 2>&1",
            "echo Done. (MySQL keeps running in the background.)",
            "timeout /t 3 >nul"
        };
        File.WriteAllText(Path.Combine(sd, "Stop_Azaroth.bat"), string.Join("\r\n", stopLines));

        bool playMade = false;
        if (!string.IsNullOrWhiteSpace(wowPath) && Directory.Exists(wowPath))
        {
            var wowExe = Path.Combine(wowPath, "wow.exe");
            if (File.Exists(wowExe))
            {
                File.WriteAllText(Path.Combine(sd, "Play_Azaroth.bat"),
                    "@echo off\r\nstart \"\" /D \"" + wowPath + "\" wow.exe " + Cfg.Server.AuthAddress + "\r\n");
                playMade = true;
                Log("Play_Azaroth.bat written.");
            }
        }

        // desktop shortcuts
        var iconExe = File.Exists(layout.WorldserverExe) ? layout.WorldserverExe : null;
        var wowIcon = !string.IsNullOrWhiteSpace(wowPath) && File.Exists(Path.Combine(wowPath, "wow.exe"))
            ? Path.Combine(wowPath, "wow.exe") : null;

        var desk = Misc.DesktopDir;
        try { Directory.CreateDirectory(desk); } catch { }

        Misc.CreateShortcut(Path.Combine(desk, "Start " + serverName + " Server.lnk"),
            Path.Combine(sd, "Start_Azaroth.bat"), "", sd, "Start the " + serverName + " Core world server", iconExe);
        Misc.CreateShortcut(Path.Combine(desk, "Stop " + serverName + " Server.lnk"),
            Path.Combine(sd, "Stop_Azaroth.bat"), "", sd, "Stop the " + serverName + " Core world server", iconExe);
        if (playMade)
            Misc.CreateShortcut(Path.Combine(desk, "Play " + serverName + ".lnk"),
                Path.Combine(sd, "Play_Azaroth.bat"), "", sd, "Launch World of Warcraft on " + serverName, wowIcon);

        // realmlist override in the client (so the login screen works without the IP argument)
        if (playMade) WriteRealmlist(wowPath, locale);

        if (Cfg.Server.FirewallRules)
            Misc.AddFirewallRules(serverName, Cfg.Server.AuthPort, Cfg.Server.RealmPort, Log);
    }

    void WriteRealmlist(string wowPath, string locale)
    {
        var locales = new List<string>();
        if (!string.IsNullOrWhiteSpace(locale) && locale.ToLowerInvariant() != "auto")
            locales.Add(locale);
        locales.AddRange(new[] { "enUS", "enGB", "frFR", "deDE", "esES", "esMX", "zhCN", "zhTW", "koKR", "ptBR", "ruRU" });
        string dir = null;
        foreach (var l in locales)
        {
            var d = Path.Combine(wowPath, "Data", l);
            if (Directory.Exists(d)) { dir = d; break; }
        }
        if (dir == null) dir = Path.Combine(wowPath, "Data");
        if (!Directory.Exists(dir)) return;

        var file = Path.Combine(dir, "realmlist.wtf");
        if (File.Exists(file) && File.ReadAllText(file).Contains("127.0.0.1", StringComparison.Ordinal))
            return;
        if (File.Exists(file))
        {
            try { File.Copy(file, file + ".orig", true); } catch { }
        }
        File.WriteAllText(file, "SET realmlist " + Cfg.Server.AuthAddress + "\n");
        Log("realmlist.wtf pointed at " + Cfg.Server.AuthAddress);
    }

    // ============================================================== smoke test
    public async Task<bool> SmokeTestAsync(ServerLayout layout, DbServerInfo db)
    {
        Log("Smoke test: starting the server stack...");
        var sd = layout.ServerDir;
        var started = new List<string>();
        bool ok = false;

        try
        {
            if (!string.IsNullOrEmpty(layout.AuthserverExe) && File.Exists(layout.AuthserverExe))
            {
                StartHidden(layout.AuthserverExe, sd);
                started.Add("authserver");
                if (!await SqlUtil.WaitTcpAsync(Cfg.Server.AuthAddress, Cfg.Server.AuthPort, 45000))
                {
                    Log("authserver did not open port " + Cfg.Server.AuthPort + " within 45s.");
                    DumpTailLogs(sd);
                    return false;
                }
                Log("authserver OK (port " + Cfg.Server.AuthPort + " open).");
            }

            if (!string.IsNullOrEmpty(layout.RealmserverExe) && File.Exists(layout.RealmserverExe))
            {
                StartHidden(layout.RealmserverExe, sd);
                started.Add("realmserver");
                if (!await SqlUtil.WaitTcpAsync(Cfg.Server.RealmAddress, Cfg.Server.RealmPort, 20000))
                    Log("realmserver port " + Cfg.Server.RealmPort + " not open (optional - continuing).");
                else
                    Log("realmserver OK (port " + Cfg.Server.RealmPort + " open).");
            }

            if (File.Exists(layout.WorldserverExe))
            {
                StartHidden(layout.WorldserverExe, sd);
                started.Add("worldserver");
                Log("worldserver starting (first boot loads the database - this can take a while)...");
                await Task.Delay(45000);
                if (Misc.ProcessRunning("worldserver"))
                {
                    Log("worldserver is running and stable. All services verified.");
                    ok = true;
                }
                else
                {
                    Log("worldserver process exited - configuration or database problem.");
                    DumpTailLogs(sd);
                    return false;
                }
            }
        }
        finally
        {
            // leave everything stopped except the database (it stays up for normal play)
            foreach (var p in new[] { "worldserver", "realmserver", "realmd", "authserver" })
                Misc.KillProcess(p);
        }

        if (db != null && db.MysqldExe != null)
            db.ServerRunning = SqlUtil.TryTcp(db.Host, db.Port, 800);

        return ok;
    }

    void StartHidden(string exe, string workDir)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            });
        }
        catch (Exception ex)
        {
            Log("Could not start " + Path.GetFileName(exe) + ": " + ex.Message);
        }
    }

    void DumpTailLogs(string serverDir)
    {
        try
        {
            var logsDir = Path.Combine(serverDir, "logs");
            if (!Directory.Exists(logsDir)) return;
            foreach (var f in Directory.GetFiles(logsDir, "*.log").OrderBy(x => x).TakeLast(3))
            {
                Log("--- tail of " + Path.GetFileName(f) + " ---");
                var lines = File.ReadAllLines(f).TakeLast(12).ToList();
                foreach (var l in lines) Log("    " + (l.Length > 220 ? l.Substring(0, 220) : l));
            }
        }
        catch { }
    }

    // ================================================================== marker
    public void WriteMarker(InstallSummary summary)
    {
        try
        {
            var marker = new Dictionary<string, string>
            {
                ["installedAt"] = DateTime.Now.ToString("s"),
                ["installRoot"] = InstallRoot,
                ["serverDir"] = summary.ServerDir,
                ["serverName"] = Cfg.ServerName,
                ["dbSource"] = summary.DbSource,
                ["dbLogin"] = summary.DbLogin,
                ["dbPassword"] = summary.DbPassword,
                ["gmUser"] = summary.GmUser,
                ["gmPassword"] = summary.GmPassword,
                ["wowPath"] = summary.WowPath
            };
            File.WriteAllText(Path.Combine(InstallRoot, "azaroth-installer.json"),
                System.Text.Json.JsonSerializer.Serialize(marker, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log("marker write failed: " + ex.Message); }
    }

    public static InstallSummary FindExistingInstall()
    {
        var candidates = new List<string>
        {
            @"C:\Program Files\Azaroth Core",
            @"C:\Azaroth Core",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Azaroth Core"
        };
        foreach (var c in candidates.Distinct())
        {
            var marker = Path.Combine(c, "azaroth-installer.json");
            if (File.Exists(marker))
            {
                try
                {
                    var j = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(marker));
                    var s = new InstallSummary
                    {
                        InstallRoot = c,
                        ServerDir = j.GetValueOrDefault("serverDir", ""),
                        DbSource = j.GetValueOrDefault("dbSource", ""),
                        GmUser = j.GetValueOrDefault("gmUser", ""),
                        GmPassword = j.GetValueOrDefault("gmPassword", ""),
                        WowPath = j.GetValueOrDefault("wowPath", "")
                    };
                    s.Lines.Add("Existing install: " + c);
                    return s;
                }
                catch { }
            }
        }
        return null;
    }
}
