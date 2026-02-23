using FluentAssertions;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class ProcessRunnerTests
{
    private readonly ProcessRunner _sut;

    public ProcessRunnerTests()
    {
        _sut = new ProcessRunner();
    }

    [Fact]
    public void IsExecutableAccessible_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");

        try
        {
            // Act
            var result = _sut.IsExecutableAccessible(tempFile);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsExecutableAccessible_WithNonExistingFile_ReturnsFalse()
    {
        // Act
        var result = _sut.IsExecutableAccessible("/non/existing/file");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExecutableAccessible_WithEmptyFile_ReturnsFalse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "");

        try
        {
            // Act
            var result = _sut.IsExecutableAccessible(tempFile);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Note: GetNodeVersion requires actual node executable, so we test error handling
    [Fact]
    public void GetNodeVersion_WithNonExistingFile_ReturnsNull()
    {
        // Act
        var result = _sut.GetNodeVersion("/non/existing/node");

        // Assert
        result.Should().BeNull();
    }
}
