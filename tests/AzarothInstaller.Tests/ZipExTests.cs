using System;
using System.IO;
using Xunit;
using AzarothInstaller;

namespace AzarothInstaller.Tests;

public class ZipExTests
{
    [Fact]
    public void SafePath_ValidRelativePath_ReturnsResolvedPathInsideRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipTestRoot");
        var resolved = ZipEx.SafePath(tempDir, @"worldserver/worldserver.exe");
        Assert.StartsWith(tempDir, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafePath_DirectoryTraversalZipSlip_ThrowsInvalidOperationException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipTestRoot");
        Assert.Throws<InvalidOperationException>(() =>
            ZipEx.SafePath(tempDir, @"../../windows/system32/cmd.exe"));
    }
}
