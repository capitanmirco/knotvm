using FluentAssertions;
using KnotVM.Core.Common;
using Xunit;

namespace KnotVM.Tests.Core;

public class ProxyNamingTests
{
    [Fact]
    public void BuildIsolatedProxyName_ShouldPrefixCommand()
    {
        // Act
        var proxyName = ProxyNaming.BuildIsolatedProxyName("node");

        // Assert
        proxyName.Should().Be("nlocal-node");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildIsolatedProxyName_ShouldThrow_WhenCommandNameIsInvalid(string commandName)
    {
        // Act
        Action act = () => ProxyNaming.BuildIsolatedProxyName(commandName);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsolatedPrefix_ShouldBeStable()
    {
        ProxyNaming.IsolatedPrefix.Should().Be("nlocal-");
    }
}
