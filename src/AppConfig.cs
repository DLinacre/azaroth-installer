using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzarothInstaller;

public class AppConfig
{
    public string ServerName { get; set; } = "Azaroth";
    public string InstallFolderName { get; set; } = "Azaroth Core";
    public double MinFreeSpaceGB { get; set; } = 30;
    public bool AutoModeDefault { get; set; } = true;

    public DownloadsConfig Downloads { get; set; } = new();
    public DbConfig Database { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
    public WorldOptionsConfig World { get; set; } = new();
    public PlayerBotsConfig PlayerBots { get; set; } = new();
    public WowClientConfig WowClient { get; set; } = new();

    public static JsonSerializerOptions JsonOpts => new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load(out string loadError)
    {
        loadError = null;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null)
                {
                    cfg.Downloads ??= new DownloadsConfig();
                    cfg.Database ??= new DbConfig();
                    cfg.Server ??= new ServerConfig();
                    cfg.World ??= new WorldOptionsConfig();
                    cfg.PlayerBots ??= new PlayerBotsConfig();
                    cfg.WowClient ??= new WowClientConfig();
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            loadError = "config.json could not be read (" + ex.Message + ") - using built-in defaults.";
        }

        var def = new AppConfig();
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(def, JsonOpts)); } catch { }
        return def;
    }
}

public class DownloadsConfig
{
    public UrlDownload ServerRepack { get; set; } = new();
    public UrlDownload AcData { get; set; } = new()
    {
        Urls = new List<string> { "https://github.com/wowgaming/client-data/releases/download/v20.0/Data.zip" },
        OnlyIfMissing = true
    };
    public UrlDownload DatabaseServer { get; set; } = new()
    {
        Urls = new List<string>
        {
            "https://downloads.mysql.com/archives/get/p/23/file/mysql-8.0.42-winx64.msi",
            "https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-8.0.42-winx64.msi"
        },
        OnlyIfMissing = true
    };
    public UrlDownload PlayerBotsConf { get; set; } = new()
    {
        Urls = new List<string>
        {
            "https://raw.githubusercontent.com/mod-playerbots/mod-playerbots/master/conf/playerbots.conf.dist"
        }
    };
}

public class UrlDownload
{
    public List<string> Urls { get; set; } = new();
    public bool OnlyIfMissing { get; set; }
    public string Hint { get; set; } = "";
}

public class DbConfig
{
    public string Login { get; set; } = "azaroth";
    public string Password { get; set; } = "Azar0th!DB";
    public string RootLogin { get; set; } = "root";
    public string RootPassword { get; set; } = "";
    public string AuthDb { get; set; } = "azaroth_auth";
    public string CharactersDb { get; set; } = "azaroth_characters";
    public string WorldDb { get; set; } = "azaroth_world";
}

public class ServerConfig
{
    public string AuthAddress { get; set; } = "127.0.0.1";
    public int AuthPort { get; set; } = 3724;
    public string RealmAddress { get; set; } = "127.0.0.1";
    public int RealmPort { get; set; } = 8085;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int MaxPlayers { get; set; } = 500;
    public string GmUsername { get; set; } = "gm";
    public string GmPassword { get; set; } = "gm1234";
    public string GmCharacterName { get; set; } = "Azaroth";
    public bool FirewallRules { get; set; } = true;
}

public class PlayerBotsConfig
{
    public bool Enabled { get; set; } = true;
}

public class WorldOptionsConfig
{
    // Realm identity (written to the auth database 'realm' table)
    public string RealmName { get; set; } = "Azaroth";
    public string ClientLocale { get; set; } = "auto"; // auto | enUS | enGB | frFR | ...

    // Progression & economy (worldserver.conf, only keys that exist in the repack)
    public double XpRate { get; set; } = 1;
    public double HonorRate { get; set; } = 1;
    public double GoldRate { get; set; } = 1;
    public int LevelCap { get; set; } = 80;

    // PlayerBots behaviour (playerbots.conf, verified AiPlayerbot.* keys)
    public int RandomBots { get; set; } = 100;
    public bool BotsAutologin { get; set; } = true;
    public int AddClassPool { get; set; } = 25;
    public int MaxAddedBots { get; set; } = 40;
    public int BotGuilds { get; set; } = 0;
    public bool BotsOnlyWhenPlayerOnline { get; set; } = false;

    // Extra prebuilt modules (direct links to a .dll or .zip dropped into the server dir)
    public List<ExtraModule> ExtraModules { get; set; } = new();

    // Client addon (GM tools UI for your GM account)
    public bool GmGenieAddon { get; set; } = true;
}

public class ExtraModule
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class WowClientConfig
{
    public bool AutoScan { get; set; } = true;
    public List<string> ExtraScanDirs { get; set; } = new();
    public List<string> DownloadUrls { get; set; } = new();
}
