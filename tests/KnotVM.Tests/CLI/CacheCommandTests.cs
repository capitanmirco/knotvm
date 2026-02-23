using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class CacheCommandTests
{
    [Fact]
    public void Cache_List_DisplaysCacheFiles()
    {
        // Arrange
        var cacheServiceMock = new Mock<ICacheService>();
        var cacheEntries = new[]
        {
            ("node-v20.11.0-win-x64.zip", 50_000_000L, DateTime.Now.AddDays(-5))
        };
        cacheServiceMock.Setup(x => x.ListCacheFiles()).Returns(cacheEntries);
        cacheServiceMock.Setup(x => x.GetCacheSizeBytes()).Returns(50_000_000L);

        var command = new CacheCommand(cacheServiceMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["cache", "--list"]).Invoke();

        // Assert  
        exitCode.Should().Be(0);
        cacheServiceMock.Verify(x => x.ListCacheFiles(), Times.Once);
    }
}
