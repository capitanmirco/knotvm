using System.Diagnostics;
using FluentAssertions;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;

namespace KnotVM.Tests.Infrastructure;

public class LockManagerTests : IDisposable
{
    private readonly string _locksPath;
    private readonly LockManager _lockManager;

    public LockManagerTests()
    {
        _locksPath = Path.Combine(Path.GetTempPath(), $"knotvm-lock-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_locksPath);

        var platformService = new PlatformService();
        var fileSystem = new FileSystemService(platformService);

        var pathServiceMock = new Mock<IPathService>();
        pathServiceMock.Setup(x => x.GetLocksPath()).Returns(_locksPath);

        _lockManager = new LockManager(pathServiceMock.Object, fileSystem);
    }

    [Fact]
    public void TryAcquireLock_ShouldReturnTrue_WhenLockIsFree()
    {
        var acquired = _lockManager.TryAcquireLock("state", out var lockHandle);

        acquired.Should().BeTrue();
        lockHandle.Should().NotBeNull();
        lockHandle!.Dispose();
    }

    [Fact]
    public void TryAcquireLock_ShouldReturnQuickly_WhenLockIsBusy()
    {
        using var firstHandle = _lockManager.AcquireLock("state");
        var stopwatch = Stopwatch.StartNew();

        var acquired = _lockManager.TryAcquireLock("state", out var secondHandle);

        stopwatch.Stop();
        acquired.Should().BeFalse();
        secondHandle.Should().BeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void AcquireLock_ShouldThrow_WhenTimeoutIsNegative()
    {
        var action = () => _lockManager.AcquireLock("state", timeoutSeconds: -1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_locksPath))
        {
            Directory.Delete(_locksPath, recursive: true);
        }
    }
}

