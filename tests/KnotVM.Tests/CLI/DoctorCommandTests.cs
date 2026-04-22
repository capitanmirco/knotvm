using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace KnotVM.Tests.CLI;

[Collection("Sequential")]
public class DoctorCommandTests
{
    private static TestConsole SetupTestConsole()
    {
        var testConsole = new TestConsole();
        testConsole.Profile.Capabilities.Interactive = false;
        AnsiConsole.Console = testConsole;
        return testConsole;
    }

    private static DoctorCheck PassedCheck(string name) =>
        new(name, Passed: true, IsWarning: false, Detail: null, Suggestion: null, CanAutoFix: false);

    private static DoctorCheck FailedCheck(string name, bool isWarning = false) =>
        new(name, Passed: false, IsWarning: isWarning, Detail: "problema", Suggestion: "fix", CanAutoFix: false);

    private static DoctorCheck FixableCheck(string name) =>
        new(name, Passed: false, IsWarning: false, Detail: "problema", Suggestion: "fix", CanAutoFix: true);

    [Fact]
    public async Task Doctor_AllChecksPassed_ReturnsExitCode0()
    {
        // Arrange
        SetupTestConsole();
        var doctorMock = new Mock<IDoctorService>();
        var allPassed = new List<DoctorCheck>
        {
            PassedCheck("KNOT_HOME"),
            PassedCheck("Versione attiva"),
            PassedCheck("Proxy sincronizzati"),
        };
        doctorMock.Setup(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPassed);

        var command = new DoctorCommand(doctorMock.Object);
        var root = new RootCommand();
        root.Subcommands.Add(command);

        // Act
        var exitCode = root.Parse(["doctor"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Doctor_CriticalCheckFailed_ReturnsExitCode1()
    {
        // Arrange
        SetupTestConsole();
        var doctorMock = new Mock<IDoctorService>();
        var checks = new List<DoctorCheck>
        {
            PassedCheck("KNOT_HOME"),
            FailedCheck("Versione attiva", isWarning: false),  // critical
        };
        doctorMock.Setup(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

        var command = new DoctorCommand(doctorMock.Object);
        var root = new RootCommand();
        root.Subcommands.Add(command);

        // Act
        var exitCode = root.Parse(["doctor"]).Invoke();

        // Assert
        exitCode.Should().Be(1);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Doctor_OnlyWarningsFailed_ReturnsExitCode0()
    {
        // Arrange
        SetupTestConsole();
        var doctorMock = new Mock<IDoctorService>();
        var checks = new List<DoctorCheck>
        {
            PassedCheck("KNOT_HOME"),
            FailedCheck("Connettività nodejs.org", isWarning: true),  // warning only
        };
        doctorMock.Setup(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

        var command = new DoctorCommand(doctorMock.Object);
        var root = new RootCommand();
        root.Subcommands.Add(command);

        // Act
        var exitCode = root.Parse(["doctor"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Doctor_WithFix_CallsTryAutoFixForFixableChecks()
    {
        // Arrange
        SetupTestConsole();
        var doctorMock = new Mock<IDoctorService>();
        var fixableCheck = FixableCheck("Proxy sincronizzati");
        var checksBeforeFix = new List<DoctorCheck> { fixableCheck };
        var checksAfterFix  = new List<DoctorCheck> { PassedCheck("Proxy sincronizzati") };

        doctorMock.SetupSequence(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(checksBeforeFix)
            .ReturnsAsync(checksAfterFix);

        doctorMock.Setup(x => x.TryAutoFixAsync(fixableCheck, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new DoctorCommand(doctorMock.Object);
        var root = new RootCommand();
        root.Subcommands.Add(command);

        // Act
        var exitCode = root.Parse(["doctor", "--fix"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        doctorMock.Verify(x => x.TryAutoFixAsync(fixableCheck, It.IsAny<CancellationToken>()), Times.Once);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Doctor_WithFixAndNoFixableChecks_DoesNotCallTryAutoFix()
    {
        // Arrange
        SetupTestConsole();
        var doctorMock = new Mock<IDoctorService>();
        var checks = new List<DoctorCheck> { PassedCheck("KNOT_HOME") };
        doctorMock.Setup(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

        var command = new DoctorCommand(doctorMock.Object);
        var root = new RootCommand();
        root.Subcommands.Add(command);

        // Act
        root.Parse(["doctor", "--fix"]).Invoke();

        // Assert
        doctorMock.Verify(x => x.TryAutoFixAsync(It.IsAny<DoctorCheck>(), It.IsAny<CancellationToken>()), Times.Never);
        await Task.CompletedTask;
    }
}
