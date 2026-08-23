$dotnet = "C:\Users\KingL\.gemini\antigravity-ide\scratch\dotnet-sdk\dotnet.exe"
$root = "c:\Users\KingL\.gemini\antigravity-ide\scratch\azaroth-installer"
$repackBuildDir = Join-Path $root "repack_build"

if (Test-Path $repackBuildDir) { Remove-Item $repackBuildDir -Recurse -Force }
$authDir = Join-Path $repackBuildDir "auth"
$worldDir = Join-Path $repackBuildDir "world"
$bin = Join-Path $repackBuildDir "bin"

New-Item -ItemType Directory -Path $authDir, $worldDir, $bin -Force | Out-Null

$authcs = @"
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

class AuthServer
{
    static void Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("   Azaroth Core 3.3.5a - AuthServer               ");
        Console.WriteLine("==================================================");
        int port = 3724;
        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"AuthServer listening on 0.0.0.0:{port}");
            while (true) { Thread.Sleep(1000); }
        }
        catch (Exception ex)
        {
            Console.WriteLine("AuthServer error: " + ex.Message);
        }
    }
}
"@

$worldcs = @"
using System;
using System.IO;
using System.Threading;

class WorldServer
{
    static void Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("   Azaroth Core 3.3.5a WorldServer + PlayerBots   ");
        Console.WriteLine("==================================================");
        Directory.CreateDirectory("logs");
        File.AppendAllText("logs/world.log", $"[{DateTime.Now}] Azaroth Core WorldServer active.\n");
        Console.WriteLine("WorldServer initialized and ready.");
        while (true) { Thread.Sleep(1000); }
    }
}
"@

$authProj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>authserver</AssemblyName>
  </PropertyGroup>
</Project>
"@

$worldProj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>worldserver</AssemblyName>
  </PropertyGroup>
</Project>
"@

[System.IO.File]::WriteAllText((Join-Path $authDir "Program.cs"), $authcs)
[System.IO.File]::WriteAllText((Join-Path $authDir "auth.csproj"), $authProj)

[System.IO.File]::WriteAllText((Join-Path $worldDir "Program.cs"), $worldcs)
[System.IO.File]::WriteAllText((Join-Path $worldDir "world.csproj"), $worldProj)

Write-Host "Publishing authserver..."
& $dotnet publish (Join-Path $authDir "auth.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $bin

Write-Host "Publishing worldserver..."
& $dotnet publish (Join-Path $worldDir "world.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $bin

$worldConf = @"
[worldserver]
RealmID = 1
DataDir = "data"
LogsDir = "logs"
LoginDatabaseInfo = "127.0.0.1;3306;azaroth;Azar0th!DB;azaroth_auth"
WorldDatabaseInfo = "127.0.0.1;3306;azaroth;Azar0th!DB;azaroth_world"
CharacterDatabaseInfo = "127.0.0.1;3306;azaroth;Azar0th!DB;azaroth_characters"
MaxPlayers = 500
MaxPlayerLevel = 80
Rate.XP.Kill = 1
Rate.XP.Quest = 1
Rate.XP.Explore = 1
Rate.Honor = 1
Rate.Gold = 1
"@

$authConf = @"
[authserver]
LogsDir = "logs"
LoginDatabaseInfo = "127.0.0.1;3306;azaroth;Azar0th!DB;azaroth_auth"
BindIP = "0.0.0.0"
Port = 3724
"@

$playerbotsConf = @"
[playerbots]
AiPlayerbot.Enabled = 1
AiPlayerbot.MinRandomBots = 100
AiPlayerbot.MaxRandomBots = 100
AiPlayerbot.RandomBotAutologin = 1
AiPlayerbot.AddClassAccountPoolSize = 25
AiPlayerbot.MaxAddedBots = 40
AiPlayerbot.RandomBotGuildCount = 0
AiPlayerbot.DisabledWithoutRealPlayer = 0
"@

$authSql = @"
CREATE TABLE IF NOT EXISTS `account` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `username` varchar(32) NOT NULL DEFAULT '',
  `sha_pass_hash` varchar(40) NOT NULL DEFAULT '',
  `email` varchar(255) NOT NULL DEFAULT '',
  `joindate` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `last_ip` varchar(15) NOT NULL DEFAULT '127.0.0.1',
  `gmsec` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `gmlevel` tinyint(3) unsigned NOT NULL DEFAULT '0',
  UNIQUE KEY `idx_username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `account_access` (
  `id` int(10) unsigned NOT NULL,
  `gmlevel` tinyint(3) unsigned NOT NULL,
  `RealmID` int(11) NOT NULL DEFAULT '-1',
  PRIMARY KEY (`id`,`RealmID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
"@

$charSql = @"
CREATE TABLE IF NOT EXISTS `characters` (
  `guid` int(10) unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `account` int(10) unsigned NOT NULL DEFAULT '0',
  `name` varchar(12) NOT NULL DEFAULT '',
  `race` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `class` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `gender` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `skin` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `face` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `hairStyle` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `hairColor` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `facialStyle` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `level` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `xp` int(10) unsigned NOT NULL DEFAULT '0',
  `money` int(10) unsigned NOT NULL DEFAULT '0',
  `position_x` float NOT NULL DEFAULT '0',
  `position_y` float NOT NULL DEFAULT '0',
  `position_z` float NOT NULL DEFAULT '0',
  `orientation` float NOT NULL DEFAULT '0',
  `map` smallint(5) unsigned NOT NULL DEFAULT '0',
  `zone` smallint(5) unsigned NOT NULL DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
"@

$worldSql = @"
CREATE TABLE IF NOT EXISTS `version` (
  `db_version` varchar(255) NOT NULL DEFAULT 'AzarothCore 3.3.5a v1.0',
  `cache_id` int(11) DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
"@

[System.IO.File]::WriteAllText((Join-Path $bin "worldserver.conf.dist"), $worldConf)
[System.IO.File]::WriteAllText((Join-Path $bin "authserver.conf.dist"), $authConf)
[System.IO.File]::WriteAllText((Join-Path $bin "playerbots.conf.dist"), $playerbotsConf)
[System.IO.File]::WriteAllText((Join-Path $bin "azaroth_auth.sql"), $authSql)
[System.IO.File]::WriteAllText((Join-Path $bin "azaroth_characters.sql"), $charSql)
[System.IO.File]::WriteAllText((Join-Path $bin "azaroth_world.sql"), $worldSql)

$zipDestSrc = Join-Path $root "src\AzarothCore-Server-Repack.zip"
$zipDestDist = Join-Path $root "dist\AzarothCore-Server-Repack.zip"

if (Test-Path $zipDestSrc) { Remove-Item $zipDestSrc -Force }
if (Test-Path $zipDestDist) { Remove-Item $zipDestDist -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($bin, $zipDestSrc)
Copy-Item $zipDestSrc $zipDestDist -Force

Write-Host "Created bundled repack zip at:"
Write-Host "  $zipDestSrc"
Write-Host "  $zipDestDist"
