using Xunit;
using AzarothInstaller;

namespace AzarothInstaller.Tests;

public class AppConfigTests
{
    [Fact]
    public void AppConfig_Defaults_HasValidRandomCredentials()
    {
        var cfg = new AppConfig();
        Assert.NotNull(cfg.Database.Password);
        Assert.NotEmpty(cfg.Database.Password);
        Assert.NotEqual("Azar0th!DB", cfg.Database.Password);

        Assert.NotNull(cfg.Server.GmPassword);
        Assert.NotEmpty(cfg.Server.GmPassword);
        Assert.NotEqual("gm1234", cfg.Server.GmPassword);
    }

    [Fact]
    public void AppConfig_DefaultListenAddress_IsLocalhost()
    {
        var cfg = new AppConfig();
        Assert.Equal("127.0.0.1", cfg.Server.ListenAddress);
        Assert.False(cfg.Server.FirewallRules);
        Assert.False(cfg.Server.LanPlay);
    }
}
