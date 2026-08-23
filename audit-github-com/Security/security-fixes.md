# Security Fix Snippets

## 1. Random default credentials (AppConfig / first-run)

```csharp
// src/PasswordGen.cs
using System.Security.Cryptography;

public static class PasswordGen
{
    static readonly char[] Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*_-".ToCharArray();

    public static string Generate(int length = 24)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
```

Apply on first run in `AppConfig.Load`/wizard:
```csharp
if (string.IsNullOrEmpty(Cfg.Database.Password) || Cfg.Database.Password == "Azar0th!DB")
    Cfg.Database.Password = PasswordGen.Generate();
if (string.IsNullOrEmpty(Cfg.Server.GmPassword) || Cfg.Server.GmPassword == "gm1234")
    Cfg.Server.GmPassword = PasswordGen.Generate(16);
```

**Never** keep a working default password in `config.json` in the repo.

## 2. SHA-256 verification after download

```csharp
// In Downloader.cs, after File.Move(tmp, destFile):
public static async Task VerifySha256(string path, string expectedHash, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(expectedHash)) return; // optional
    using var sha = SHA256.Create();
    await using var fs = File.OpenRead(path);
    var actual = BitConverter.ToString(await sha.ComputeHashAsync(fs, ct)).Replace("-", "");
    if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
        throw new Exception(
            $"Checksum mismatch for {Path.GetFileName(path)}.\n" +
            $"  expected: {expectedHash}\n  actual:   {actual}\n" +
            "Refusing to run a possibly tampered/corrupted download.");
}
```

Add `"sha256"` to `UrlDownload` in `AppConfig.cs` and document it in CONFIG.md.

## 3. MySQL via `--defaults-extra-file` (no password on command line)

```csharp
// SqlUtil.cs
static string WriteDefaultsFile(DbServerInfo db, string database)
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
    File.WriteAllText(path, string.Join("\n", lines));

    // Restrict ACLs to the current user only (Windows)
    var acl = new FileSecurity();
    acl.SetOwner(WindowsIdentity.GetCurrent().User);
    acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
    acl.AddAccessRule(new FileSystemAccessRule(
        WindowsIdentity.GetCurrent().User,
        FileSystemRights.FullControl, AccessControlType.Allow));
    File.SetAccessControl(path, acl);
    return path;
}

static int RunMysql(DbServerInfo db, string database, string extraArgs, string stdinFile)
{
    var cfg = WriteDefaultsFile(db, database);
    try
    {
        var psi = new ProcessStartInfo(db.MysqlExe,
            $"--defaults-extra-file=\"{cfg}\" {extraArgs}")
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (!string.IsNullOrEmpty(stdinFile))
        {
            using var fs = File.OpenRead(stdinFile);
            fs.CopyTo(p.StandardInput.BaseStream);
            p.StandardInput.Close();
        }
        // ... read output, wait, return exit code
    }
    finally { try { File.Delete(cfg); } catch { } }
}
```

## 4. SQL identifier/value quoting helpers

```csharp
static string QuoteIdent(string name)
{
    // MySQL backtick-quoted identifier: double any embedded backticks.
    // Also reject NUL bytes (MySQL does not allow them in identifiers).
    if (string.IsNullOrEmpty(name)) throw new ArgumentException("empty identifier");
    if (name.Contains('\0')) throw new ArgumentException("NUL in identifier");
    if (name.Length > 64) throw new ArgumentException("identifier too long");
    return "`" + name.Replace("`", "``") + "`";
}

static string QuoteString(string value)
{
    if (value == null) return "NULL";
    return "'" + value.Replace("\\", "\\\\").Replace("'", "''") + "'";
}
```

For account/grants queries, prefer `MySqlConnector` with parameters:
```csharp
using var cmd = new MySqlCommand(
    "CREATE USER IF NOT EXISTS @u@'localhost' IDENTIFIED BY @p;", conn);
cmd.Parameters.AddWithValue("@u", cfg.Login);
cmd.Parameters.AddWithValue("@p", cfg.Password);
```

## 5. Localhost-by-default + opt-in LAN firewall rule

```csharp
// config defaults
"listenAddress": "127.0.0.1",
"firewallRules": false,
"lanPlay": false
```

```csharp
// Misc.AddFirewallRules — only when LAN play enabled; scope to private subnets
var remoteIp = "LocalSubnet"; // netsh supports the 'LocalSubnet' keyword
SqlUtil.RunShell("netsh",
    $"advfirewall firewall add rule name=\"{serverName} (WoW LAN)\" " +
    $"dir=in action=allow protocol=TCP localport={ports} remoteip={remoteIp} enable=yes");
```

## 6. Release verification (publish for users)

```powershell
# User verifies the downloaded installer:
Get-FileHash .\setup.exe -Algorithm SHA256
# Compare to SHA256SUMS.txt on the release page.

Get-AuthenticodeSignature .\setup.exe | Format-List Status, SignerCertificate
```
