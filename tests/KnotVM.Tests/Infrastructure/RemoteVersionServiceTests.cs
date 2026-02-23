using FluentAssertions;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class RemoteVersionServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly RemoteVersionService _sut;
    private readonly string _basePath = Path.Combine("/test", "knotvm");
    private readonly string _indexFilePath;

    public RemoteVersionServiceTests()
    {
        _fileSystemMock = new Mock<IFileSystemService>();
        _pathServiceMock = new Mock<IPathService>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _indexFilePath = Path.Combine(_basePath, "versions-index.json");
        _pathServiceMock.Setup(x => x.GetBasePath()).Returns(_basePath);
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        _sut = new RemoteVersionService(_fileSystemMock.Object, _pathServiceMock.Object, _httpClient);
    }

    #region GetAvailableVersionsAsync Tests

    [Fact]
    public async Task GetAvailableVersionsAsync_WithValidRemoteData_ReturnsVersions()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64"",""linux-x64""]},
            {""version"":""v18.19.0"",""lts"":""Hydrogen"",""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.GetAvailableVersionsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Version.Should().Be("20.11.0");
        result[0].Lts.Should().Be("Iron");
        result[1].Version.Should().Be("18.19.0");
        result[1].Lts.Should().Be("Hydrogen");
        _fileSystemMock.Verify(x => x.WriteAllTextSafe(_indexFilePath, json), Times.Once);
    }

    [Fact]
    public async Task GetAvailableVersionsAsync_WithNetworkError_ThrowsKnotVMException()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var act = async () => await _sut.GetAvailableVersionsAsync();

        // Assert
        await act.Should().ThrowAsync<KnotVMException>();
    }

    [Fact]
    public async Task GetAvailableVersionsAsync_WithExpiredCacheAndForceRefresh_FetchesFromRemote()
    {
        // Arrange
        var oldJson = @"[{""version"":""v18.0.0"",""lts"":false,""date"":""2023-01-01"",""files"":[""win-x64""]}]";
        var newJson = @"[{""version"":""v20.0.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64""]}]";

        _fileSystemMock.Setup(x => x.FileExists(_indexFilePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(_indexFilePath)).Returns(oldJson);
        _fileSystemMock.Setup(x => x.GetFileLastWriteTime(_indexFilePath)).Returns(DateTime.UtcNow.AddHours(-2));

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(newJson)
            });

        // Act
        var result = await _sut.GetAvailableVersionsAsync(forceRefresh: true);

        // Assert
        result.Should().HaveCount(1);
        result[0].Version.Should().Be("20.0.0");
    }

    #endregion

    #region GetLtsVersionsAsync Tests

    [Fact]
    public async Task GetLtsVersionsAsync_FiltersLtsVersionsOnly()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v19.0.0"",""lts"":false,""date"":""2023-12-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":true,""date"":""2023-11-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.GetLtsVersionsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Version.Should().Be("20.11.0");
        result[0].Lts.Should().Be("Iron");
    }

    #endregion

    #region GetLatestLtsVersionAsync Tests

    [Fact]
    public async Task GetLatestLtsVersionAsync_ReturnsFirstLtsVersion()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":""Hydrogen"",""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.GetLatestLtsVersionAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("20.11.0");
        result.Lts.Should().Be("Iron");
    }

    #endregion

    #region ResolveVersionAsync Tests

    [Fact]
    public async Task ResolveVersionAsync_WithLatestPattern_ReturnsLatestVersion()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":false,""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":false,""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("latest");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_WithLtsPattern_ReturnsLatestLts()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v19.0.0"",""lts"":false,""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("lts");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_WithExactVersion_ReturnsMatch()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":false,""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":false,""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("18.19.0");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("18.19.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_WithPartialVersion_ReturnsFirstMatch()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.1"",""lts"":false,""date"":""2024-01-02"",""files"":[""win-x64""]},
            {""version"":""v20.11.0"",""lts"":false,""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":false,""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("20");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("20.11.1");
    }

    [Fact]
    public async Task ResolveVersionAsync_WithLtsCodename_ReturnsCodenameVersion()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":""Iron"",""date"":""2024-01-01"",""files"":[""win-x64""]},
            {""version"":""v18.19.0"",""lts"":""Hydrogen"",""date"":""2023-12-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("hydrogen");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("18.19.0");
        result.Lts.Should().Be("Hydrogen");
    }

    [Fact]
    public async Task ResolveVersionAsync_WithNonExistingVersion_ReturnsNull()
    {
        // Arrange
        var json = @"[
            {""version"":""v20.11.0"",""lts"":false,""date"":""2024-01-01"",""files"":[""win-x64""]}
        ]";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _sut.ResolveVersionAsync("99.99.99");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_RemovesCacheFile()
    {
        // Act
        _sut.ClearCache();

        // Assert
        _fileSystemMock.Verify(x => x.DeleteFileIfExists(_indexFilePath), Times.Once);
    }

    #endregion
}
