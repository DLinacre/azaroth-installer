using System.Diagnostics;
using System.IO.Compression;

namespace AzarothInstaller;

public static class SqlUtil
{
    public static string QuoteIdent(string name)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Identifier cannot be empty.");
        if (name.Contains('\0')) throw new ArgumentException("Identifier cannot contain NUL bytes.");
        return "`" + name.Replace("`", "``") + "`";
    }

    public static string QuoteString(string value)
    {
        if (value == null) return "NULL";
        return "'" + value.Replace("\\", "\\\\").Replace("'", "''") + "'";
    }

    public static string WriteDefaultsFile(DbServerInfo db, string database = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "azmy_" + Guid.NewGuid().ToString("N") + ".cnf");
        var lines = new List<string>
        {
            "[client]",
            $"host={db.Host}",
            $"port={db.Port}",
            $"user={db.Login}",
            $"password={db.Password}",
            "protocol=TCP",
            "default-character-set=utf8mb4"
        };
        if (!string.IsNullOrEmpty(database)) lines.Add($"database={database}");
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");

        try
        {
            var acl = new System.Security.AccessControl.FileSecurity();
            var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().User;
            acl.SetOwner(currentUser);
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                currentUser,
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));
            var fi = new FileInfo(path);
            fi.SetAccessControl(acl);
        }
        catch { }

        return path;
    }

    /// <summary>Run an SQL file through the mysql/mariadb client. Returns process exit code.</summary>
    public static int ImportSqlFile(DbServerInfo db, string database, string sqlFile, Action<string> log)
    {
        string realSql = null;
        bool isTempSql = false;
        string defaultsFile = null;
        try
        {
            realSql = EnsurePlainSql(sqlFile, out isTempSql);
            defaultsFile = WriteDefaultsFile(db, database);
            var bat = WriteTempBat($"\"{db.MysqlExe}\" --defaults-extra-file=\"{defaultsFile}\" < \"{realSql}\"");
            var (code, outp) = RunBat(bat);
            log?.Invoke($"SQL import: {Path.GetFileName(sqlFile)} -> {database} (exit {code})");
            if (!string.IsNullOrWhiteSpace(outp))
                log?.Invoke("    " + Truncate(outp.Replace("\r", ""), 500).Replace("\n", "\n    "));
            return code;
        }
        catch (Exception ex)
        {
            log?.Invoke("SQL import error for " + Path.GetFileName(sqlFile) + ": " + ex.Message);
            return -1;
        }
        finally
        {
            if (!string.IsNullOrEmpty(defaultsFile) && File.Exists(defaultsFile))
            {
                try { File.Delete(defaultsFile); } catch { }
            }
            if (isTempSql && !string.IsNullOrEmpty(realSql) && File.Exists(realSql))
            {
                try { File.Delete(realSql); } catch { }
            }
        }
    }

    public static (int code, string outp) Query(DbServerInfo db, string database, string sql)
    {
        string defaultsFile = null;
        try
        {
            defaultsFile = WriteDefaultsFile(db, database);
            var bat = WriteTempBat($"\"{db.MysqlExe}\" --defaults-extra-file=\"{defaultsFile}\" -N -B -e \"{sql.Replace("\"", "\\\"")}\"");
            return RunBat(bat);
        }
        finally
        {
            if (!string.IsNullOrEmpty(defaultsFile) && File.Exists(defaultsFile))
            {
                try { File.Delete(defaultsFile); } catch { }
            }
        }
    }

    public static bool ServerAlive(DbServerInfo db)
    {
        try
        {
            var (code, outp) = Query(db, null, "SELECT 1");
            if (code == 0 && outp.Contains("1")) return true;
        }
        catch { }
        return TryTcp(db.Host, db.Port, 1200);
    }

    public static bool DatabaseExists(DbServerInfo db, string name)
    {
        try
        {
            var (code, outp) = Query(db, null,
                "SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=" + QuoteString(name));
            return code == 0 && outp.Contains(name);
        }
        catch { return false; }
    }

    public static int CreateDatabase(DbServerInfo db, string name, Action<string> log)
    {
        if (DatabaseExists(db, name))
        {
            log?.Invoke($"database '{name}' already exists - reusing it");
            return 0;
        }
        var (code, outp) = Query(db, null,
            $"CREATE DATABASE IF NOT EXISTS {QuoteIdent(name)} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        if (code != 0) log?.Invoke("create database failed: " + Truncate(outp, 300));
        else log?.Invoke($"created database '{name}'");
        return code;
    }

    public static int EnsureUserAndGrants(DbServerInfo db, DbConfig cfg, Action<string> log)
    {
        string escUser = QuoteString(cfg.Login);
        string escPass = QuoteString(cfg.Password);
        string authDb = QuoteIdent(cfg.AuthDb);
        string charDb = QuoteIdent(cfg.CharactersDb);
        string worldDb = QuoteIdent(cfg.WorldDb);

        var sql =
            $"CREATE USER IF NOT EXISTS {escUser}@'localhost' IDENTIFIED BY {escPass};" +
            $"CREATE USER IF NOT EXISTS {escUser}@'127.0.0.1' IDENTIFIED BY {escPass};" +
            $"GRANT ALL PRIVILEGES ON {authDb}.* TO {escUser}@'localhost';" +
            $"GRANT ALL PRIVILEGES ON {charDb}.* TO {escUser}@'localhost';" +
            $"GRANT ALL PRIVILEGES ON {worldDb}.* TO {escUser}@'localhost';" +
            $"GRANT ALL PRIVILEGES ON {authDb}.* TO {escUser}@'127.0.0.1';" +
            $"GRANT ALL PRIVILEGES ON {charDb}.* TO {escUser}@'127.0.0.1';" +
            $"GRANT ALL PRIVILEGES ON {worldDb}.* TO {escUser}@'127.0.0.1';" +
            "FLUSH PRIVILEGES;";
        var (code, outp) = Query(db, null, sql);
        if (code != 0) log?.Invoke("user/grants setup problem: " + Truncate(outp, 300));
        else log?.Invoke($"database user '{cfg.Login}' ready");
        return code;
    }

    public static string GetIntValue(DbServerInfo db, string database, string sql)
    {
        var (code, outp) = Query(db, database, sql);
        if (code == 0)
        {
            foreach (var line in outp.Split('\n'))
            {
                var t = line.Trim();
                if (long.TryParse(t, out var v)) return t;
            }
        }
        return null;
    }

    public static string WoWPassSha1(string username, string password)
    {
        string combo = (username ?? "").ToUpperInvariant() + ":" + (password ?? "").ToUpperInvariant();
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var shaBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combo));
        return Convert.ToHexString(shaBytes); // uppercase hex
    }

    public static string WoWPassSha1(string password) => WoWPassSha1("gm", password);

    // ------------------------------------------------------------ internals

    static string MysqlPassArg(string password)
        => string.IsNullOrEmpty(password) ? "" : $" --password={password.Replace("\"", "\\\"")}";

    static string DbArg(string database)
        => string.IsNullOrEmpty(database) ? "" : " " + database;

    static string EnsurePlainSql(string sqlFile, out bool isTemp)
    {
        var ext = Path.GetExtension(sqlFile).ToLowerInvariant();
        if (ext == ".sql")
        {
            isTemp = false;
            return sqlFile;
        }
        isTemp = true;
        var tmpFile = Path.Combine(Path.GetTempPath(), "azsql_" + Guid.NewGuid().ToString("N") + ".sql");
        if (ext == ".gz")
        {
            using var gz = File.OpenRead(sqlFile);
            using var zs = new GZipStream(gz, CompressionMode.Decompress);
            using var outp = File.Create(tmpFile);
            zs.CopyTo(outp);
        }
        else if (ext == ".z" || ext == ".sqlz")
        {
            using var fs = File.OpenRead(sqlFile);
            using var zs = new ZLibStream(fs, CompressionMode.Decompress);
            using var outp = File.Create(tmpFile);
            zs.CopyTo(outp);
        }
        else
        {
            File.Copy(sqlFile, tmpFile, true);
        }
        return tmpFile;
    }

    static string WriteTempBat(string line)
    {
        var bat = Path.Combine(Path.GetTempPath(), "azbat_" + Guid.NewGuid().ToString("N") + ".bat");
        File.WriteAllText(bat, "@echo off\r\n" + line + "\r\n");
        return bat;
    }

    public static (int code, string outp) RunBat(string batPath)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/d /c \"\"" + batPath + "\"\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            var so = soTask.GetAwaiter().GetResult();
            var se = seTask.GetAwaiter().GetResult();
            File.Delete(batPath);
            return (p.ExitCode, (so + "\n" + se).Trim());
        }
        catch (Exception ex)
        {
            try { File.Delete(batPath); } catch { }
            return (-1, ex.Message);
        }
    }

    public static (int code, string outp) RunShell(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();
            p.WaitForExit(120000);
            var so = soTask.GetAwaiter().GetResult();
            var se = seTask.GetAwaiter().GetResult();
            return (p.ExitCode, (so + "\n" + se).Trim());
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    public static bool TryTcp(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var task = client.ConnectAsync(host, port);
            return task.Wait(timeoutMs) && client.Connected;
        }
        catch { return false; }
    }

    public static async Task<bool> WaitTcpAsync(string host, int port, int timeoutMs, int pollMs = 500)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (TryTcp(host, port, 400)) return true;
            await Task.Delay(pollMs);
        }
        return TryTcp(host, port, 400);
    }

    static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", "");
        return s.Length <= max ? s : s.Substring(0, max) + " ...";
    }
}
