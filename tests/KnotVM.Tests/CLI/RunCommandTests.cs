using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class RunCommandTests : IDisposable
{
    private readonly string _tempInstallationPath;

    public RunCommandTests()
    {
        _tempInstallationPath = Path.Combine(Path.GetTempPath(), $"knotvm-run-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempInstallationPath);
    }

    [Fact]
    public void RunCommand_ShouldPassTokenizedArguments_ToProcessRunner()
    {
        var executablePath = Path.Combine(_tempInstallationPath, "npm.cmd");
        File.WriteAllText(executablePath, "@echo off");

        var repositoryMock = new Mock<IInstallationsRepository>();
        repositoryMock
            .Setup(x => x.GetByAlias("v20"))
            .Returns(new Installation("v20", "20.11.0", _tempInstallationPath, Use: false));

        var processRunnerMock = new Mock<IProcessRunner>();
        var capturedArguments = Array.Empty<string>();

        processRunnerMock
            .Setup(x => x.RunAndPropagateExitCode(
                executablePath,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .Callback<string, IReadOnlyList<string>, string?, Dictionary<string, string>?>((_, args, _, _) =>
            {
                capturedArguments = args.ToArray();
            })
            .Returns(0);

        var platformServiceMock = new Mock<IPlatformService>();
        platformServiceMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);

        var detectorMock = new Mock<IVersionFileDetector>();
        var installServiceMock = new Mock<IInstallationService>();

        var runCommand = new RunCommand(repositoryMock.Object, processRunnerMock.Object, platformServiceMock.Object, detectorMock.Object, installServiceMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(runCommand);

        var exitCode = rootCommand.Parse([
            "run",
            "npm --name \"a b\" --flag",
            "--with-version",
            "v20"
        ]).Invoke();

        exitCode.Should().Be(0);
        capturedArguments.Should().Equal("--name", "a b", "--flag");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempInstallationPath))
        {
            Directory.Delete(_tempInstallationPath, recursive: true);
        }
    }
}
