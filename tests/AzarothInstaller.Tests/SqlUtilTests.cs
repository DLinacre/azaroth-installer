using System;
using Xunit;
using AzarothInstaller;

namespace AzarothInstaller.Tests;

public class SqlUtilTests
{
    [Fact]
    public void QuoteIdent_StandardName_WrapsInBackticks()
    {
        var quoted = SqlUtil.QuoteIdent("azaroth_auth");
        Assert.Equal("`azaroth_auth`", quoted);
    }

    [Fact]
    public void QuoteIdent_EmbeddedBacktick_DoublesBacktick()
    {
        var quoted = SqlUtil.QuoteIdent("db`name");
        Assert.Equal("`db``name`", quoted);
    }

    [Fact]
    public void QuoteIdent_EmptyOrNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SqlUtil.QuoteIdent(""));
        Assert.Throws<ArgumentException>(() => SqlUtil.QuoteIdent(null));
    }

    [Fact]
    public void QuoteString_Null_ReturnsNULL()
    {
        var quoted = SqlUtil.QuoteString(null);
        Assert.Equal("NULL", quoted);
    }

    [Fact]
    public void QuoteString_StandardValue_WrapsInSingleQuotes()
    {
        var quoted = SqlUtil.QuoteString("admin");
        Assert.Equal("'admin'", quoted);
    }

    [Fact]
    public void QuoteString_EscapesQuotesAndBackslashes()
    {
        var quoted = SqlUtil.QuoteString(@"O'Connor\Test");
        Assert.Equal(@"'O''Connor\\Test'", quoted);
    }
}
