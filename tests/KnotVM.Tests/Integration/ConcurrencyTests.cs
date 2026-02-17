using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Integration;

/// <summary>
/// Tests for concurrency and locking mechanisms
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly LockManager _sut;
    private readonly string _tempLocksPath;

    public ConcurrencyTests()
    {
        _pathServiceMock = new Mock<IPathService>();
        
        // Use real temp directory for lock files (LockManager requires real FileStream)
        _tempLocksPath = Path.Combine(Path.GetTempPath(), "knotvm-test-locks-" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempLocksPath);
        
        _pathServiceMock.Setup(x => x.GetLocksPath()).Returns(_tempLocksPath);
        
        // Use real FileSystemService since locks require real files
        var platformMock = new Mock<IPlatformService>();
        platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);
        var fileSystemService = new KnotVM.Infrastructure.Services.FileSystemService(platformMock.Object);
        _sut = new LockManager(_pathServiceMock.Object, fileSystemService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempLocksPath))
            {
                Directory.Delete(_tempLocksPath, recursive: true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public void AcquireLock_WhenNotLocked_AcquiresSuccessfully()
    {
        // Arrange
        var lockName = "test-lock";

        // Act
        using var lockHandle = _sut.AcquireLock(lockName, timeoutSeconds: 1);

        // Assert
        lockHandle.Should().NotBeNull();
    }

    [Fact]
    public void AcquireLock_WithDispose_ReleasesLock()
    {
        // Arrange
        var lockName = "test-dispose";

        // Act
        using (var lockHandle = _sut.AcquireLock(lockName, timeoutSeconds: 1))
        {
            lockHandle.Should().NotBeNull();
        }

        // After dispose, lock should be released (can acquire again)
        using var lockHandle2 = _sut.AcquireLock(lockName, timeoutSeconds: 1);
        lockHandle2.Should().NotBeNull();
    }

    [Fact]
    public void ConcurrentOperations_OnDifferentLocks_NoConflict()
    {
        // Arrange
        var lock1 = "lock-1";
        var lock2 = "lock-2";

        // Act - Acquire two different locks simultaneously
        using var handle1 = _sut.AcquireLock(lock1, timeoutSeconds: 1);
        using var handle2 = _sut.AcquireLock(lock2, timeoutSeconds: 1);

        // Assert - Both locks acquired without conflict
        handle1.Should().NotBeNull();
        handle2.Should().NotBeNull();
    }

    [Fact]
    public void SequentialOperations_OnSameLock_WorkCorrectly()
    {
        // Arrange
        var lockName = "sequential";
        var operations = new List<string>();

        // Act - Sequential operations with same lock
        using (var handle1 = _sut.AcquireLock(lockName, timeoutSeconds: 1))
        {
            operations.Add("op1-start");
            operations.Add("op1-end");
        }

        using (var handle2 = _sut.AcquireLock(lockName, timeoutSeconds: 1))
        {
            operations.Add("op2-start");
            operations.Add("op2-end");
        }

        // Assert
        operations.Should().ContainInOrder("op1-start", "op1-end", "op2-start", "op2-end");
    }

    [Fact]
    public void AcquireLock_WithZeroTimeout_ThrowsOnConflict()
    {
        // Arrange
        var lockName = "zero-timeout";

        using var handle1 = _sut.AcquireLock(lockName, timeoutSeconds: 5);

        // Act - Try to acquire with zero timeout
        var act = () => _sut.AcquireLock(lockName, timeoutSeconds: 0);

        // Assert
        act.Should().Throw<KnotVMException>();
    }
}
