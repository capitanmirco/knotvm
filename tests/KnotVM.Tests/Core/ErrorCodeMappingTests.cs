using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using Xunit;

namespace KnotVM.Tests.Core;

/// <summary>
/// Test mappatura KNOT-* error codes -> exit codes
/// Requisito: Prompt 11/12 - verifica mappa deterministica
/// </summary>
public class ErrorCodeMappingTests
{
    [Theory]
    [InlineData(KnotErrorCode.UnsupportedOs, 10)]
    [InlineData(KnotErrorCode.UnsupportedArch, 11)]
    [InlineData(KnotErrorCode.PathCreationFailed, 20)]
    [InlineData(KnotErrorCode.InsufficientPermissions, 21)]
    [InlineData(KnotErrorCode.CorruptedSettingsFile, 23)]
    [InlineData(KnotErrorCode.PathNotFound, 24)]
    [InlineData(KnotErrorCode.RemoteApiFailed, 30)]
    [InlineData(KnotErrorCode.ArtifactNotAvailable, 31)]
    [InlineData(KnotErrorCode.DownloadFailed, 32)]
    [InlineData(KnotErrorCode.ChecksumMismatch, 33)]
    [InlineData(KnotErrorCode.CorruptedArchive, 34)]
    [InlineData(KnotErrorCode.InstallationNotFound, 40)]
    [InlineData(KnotErrorCode.InvalidAlias, 41)]
    [InlineData(KnotErrorCode.CommandNotFound, 42)]
    [InlineData(KnotErrorCode.InstallationFailed, 43)]
    [InlineData(KnotErrorCode.ProxyGenerationFailed, 50)]
    [InlineData(KnotErrorCode.SyncFailed, 51)]
    [InlineData(KnotErrorCode.LockFailed, 60)]
    [InlineData(KnotErrorCode.UnexpectedError, 99)]
    public void ErrorCode_ShouldMapToCorrectExitCode(KnotErrorCode errorCode, int expectedExitCode)
    {
        // Arrange & Act
        int actualExitCode = ErrorExitCodeMap.GetExitCode(errorCode);
        
        // Assert
        actualExitCode.Should().Be(expectedExitCode, 
            $"KNOT-{errorCode} deve mappare a exit code {expectedExitCode}");
    }
    
    [Fact]
    public void ErrorCodes_ShouldBeUnique()
    {
        // Arrange
        var allCodes = Enum.GetValues<KnotErrorCode>().Cast<int>().ToList();
        
        // Act
        var uniqueCodes = allCodes.Distinct().Count();
        
        // Assert
        uniqueCodes.Should().Be(allCodes.Count, "Tutti i codici errore devono essere univoci");
    }
    
    [Fact]
    public void ErrorCodes_ShouldNotConflict()
    {
        // Arrange
        var codes = Enum.GetValues<KnotErrorCode>().Cast<int>().ToList();
        var duplicates = codes.GroupBy(x => x)
                             .Where(g => g.Count() > 1)
                             .Select(g => g.Key)
                             .ToList();
        
        // Assert
        duplicates.Should().BeEmpty("Non devono esistere codici exit duplicati");
    }
    
    [Theory]
    [InlineData("KNOT-OS-001", KnotErrorCode.UnsupportedOs)]
    [InlineData("KNOT-OS-002", KnotErrorCode.UnsupportedArch)]
    [InlineData("KNOT-PATH-001", KnotErrorCode.PathCreationFailed)]
    [InlineData("KNOT-PERM-001", KnotErrorCode.InsufficientPermissions)]
    [InlineData("KNOT-DL-001", KnotErrorCode.DownloadFailed)]
    [InlineData("KNOT-SEC-001", KnotErrorCode.ChecksumMismatch)]
    [InlineData("KNOT-INS-001", KnotErrorCode.InstallationNotFound)]
    [InlineData("KNOT-RUN-001", KnotErrorCode.CommandNotFound)]
    [InlineData("KNOT-LOCK-001", KnotErrorCode.LockFailed)]
    public void KnotVMException_ShouldHaveCorrectCodeFormat(string expectedCode, KnotErrorCode errorCode)
    {
        // Arrange & Act
        var exception = new KnotVMException(errorCode, "Test message");
        
        // Assert
        exception.ErrorCode.Should().Be(errorCode);
        exception.CodeString.Should().Be(expectedCode, $"CodeString deve essere {expectedCode}");
    }
    
    [Fact]
    public void ErrorExitCodeMap_ShouldBeValid()
    {
        // Act & Assert
        ErrorExitCodeMap.ValidateMapping().Should().BeTrue(
            "Il mapping deve essere completo e senza collisioni di exit code");
    }
}
