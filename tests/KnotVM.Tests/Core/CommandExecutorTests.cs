using FluentAssertions;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using Xunit;

namespace KnotVM.Tests.Core;

public class CommandExecutorTests
{
    [Fact]
    public void ExecuteWithExitCode_ShouldReturnActionExitCode_WhenNoException()
    {
        var exitCode = CommandExecutor.ExecuteWithExitCode(() => 7);

        exitCode.Should().Be(7);
    }

    [Fact]
    public void ExecuteWithExitCode_ShouldReturnMappedExitCode_ForKnotException()
    {
        Func<int> action = () => throw new KnotVMException(KnotErrorCode.InstallationNotFound, "not found");
        var exitCode = CommandExecutor.ExecuteWithExitCode(action);

        exitCode.Should().Be(40);
    }

    [Fact]
    public void ExecuteWithExitCode_ShouldReturnMappedExitCode_ForKnotHintException()
    {
        Func<int> action = () => throw new KnotVMHintException(KnotErrorCode.InvalidAlias, "invalid", "hint");
        var exitCode = CommandExecutor.ExecuteWithExitCode(action);

        exitCode.Should().Be(41);
    }

    [Fact]
    public void ExecuteWithExitCode_ShouldReturnGenericExitCode_ForUnexpectedException()
    {
        Func<int> action = () => throw new InvalidOperationException("boom");
        var exitCode = CommandExecutor.ExecuteWithExitCode(action);

        exitCode.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteWithExitCodeAsync_ShouldReturnZero_WhenActionCompletes()
    {
        var exitCode = await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            await Task.Delay(1);
        });

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteWithExitCodeAsync_ShouldReturn130_WhenCancelled()
    {
        var exitCode = await CommandExecutor.ExecuteWithExitCodeAsync(() =>
        {
            throw new OperationCanceledException();
        });

        exitCode.Should().Be(130);
    }
}
