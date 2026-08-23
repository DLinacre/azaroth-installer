using Xunit;
using AzarothInstaller;

namespace AzarothInstaller.Tests;

public class PasswordGenTests
{
    [Fact]
    public void Generate_DefaultLength_Returns24Chars()
    {
        var pass = PasswordGen.Generate();
        Assert.NotNull(pass);
        Assert.Equal(24, pass.Length);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void Generate_CustomLength_ReturnsRequestedLength(int length)
    {
        var pass = PasswordGen.Generate(length);
        Assert.Equal(length, pass.Length);
    }

    [Fact]
    public void Generate_MultipleCalls_ProduceUniqueValues()
    {
        var pass1 = PasswordGen.Generate(20);
        var pass2 = PasswordGen.Generate(20);
        Assert.NotEqual(pass1, pass2);
    }
}
