using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MeetingMinutes.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Text;

namespace MeetingMinutes.Tests.Services;

public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockVideosContainer;
    private readonly Mock<BlobContainerClient> _mockTranscriptsContainer;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly Mock<ILogger<BlobStorageService>> _mockLogger;
    private readonly BlobStorageService _service;

    public BlobStorageServiceTests()
    {
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockVideosContainer = new Mock<BlobContainerClient>();
        _mockTranscriptsContainer = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();
        _mockLogger = new Mock<ILogger<BlobStorageService>>();

        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("videos"))
            .Returns(_mockVideosContainer.Object);

        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("transcripts"))
            .Returns(_mockTranscriptsContainer.Object);

        _service = new BlobStorageService(_mockBlobServiceClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UploadVideoAsync_ShouldCreateContainerAndUploadBlob()
    {
        // Arrange
        var fileName = "test-video.mp4";
        var expectedUri = new Uri($"https://storage.example.com/videos/{fileName}");
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake video content"));

        _mockVideosContainer
            .Setup(x => x.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());

        _mockVideosContainer
            .Setup(x => x.GetBlobClient(fileName))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.Uri)
            .Returns(expectedUri);

        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        // Act
        var result = await _service.UploadVideoAsync(stream, fileName, CancellationToken.None);

        // Assert
        result.Should().Be(expectedUri.ToString());
        _mockVideosContainer.Verify(x => x.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockBlobClient.Verify(x => x.UploadAsync(stream, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadTextAsync_ShouldCreateContainerAndUploadText()
    {
        // Arrange
        var blobName = "transcript-123.txt";
        var content = "This is a test transcript with multiple words.";
        var expectedUri = new Uri($"https://storage.example.com/transcripts/{blobName}");

        _mockTranscriptsContainer
            .Setup(x => x.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContainerInfo>>());

        _mockTranscriptsContainer
            .Setup(x => x.GetBlobClient(blobName))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.Uri)
            .Returns(expectedUri);

        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        // Act
        var result = await _service.UploadTextAsync(content, blobName, CancellationToken.None);

        // Assert
        result.Should().Be(expectedUri.ToString());
        _mockTranscriptsContainer.Verify(x => x.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockBlobClient.Verify(x => x.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadTextAsync_ShouldReturnContent_WhenBlobExists()
    {
        // Arrange
        var blobUri = "https://storage.example.com/transcripts/transcript-123.txt";
        var expectedContent = "Downloaded transcript content";
        
        var mockContainer = new Mock<BlobContainerClient>();
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("transcripts"))
            .Returns(mockContainer.Object);

        mockContainer
            .Setup(x => x.GetBlobClient("transcript-123.txt"))
            .Returns(_mockBlobClient.Object);

        var binaryData = BinaryData.FromString(expectedContent);
        var downloadResult = BlobsModelFactory.BlobDownloadResult(content: binaryData);
        var response = Response.FromValue(downloadResult, Mock.Of<Response>());

        _mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.DownloadTextAsync(blobUri, CancellationToken.None);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task DownloadTextAsync_ShouldReturnNull_WhenBlobNotFound()
    {
        // Arrange
        var blobUri = "https://storage.example.com/transcripts/nonexistent.txt";
        
        var mockContainer = new Mock<BlobContainerClient>();
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("transcripts"))
            .Returns(mockContainer.Object);

        mockContainer
            .Setup(x => x.GetBlobClient("nonexistent.txt"))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Blob not found"));

        // Act
        var result = await _service.DownloadTextAsync(blobUri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadTextAsync_ShouldReturnNull_WhenUriIsInvalid()
    {
        // Arrange
        var invalidUri = "https://storage.example.com/invalidpath";

        // Act
        var result = await _service.DownloadTextAsync(invalidUri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSasUrlAsync_ShouldReturnSasUri()
    {
        // Arrange
        var blobUri = "https://storage.example.com/videos/video-123.mp4";
        var expectedSasUri = new Uri("https://storage.example.com/videos/video-123.mp4?sv=2021-06-08&sig=test");
        var expiry = TimeSpan.FromHours(1);

        var mockContainer = new Mock<BlobContainerClient>();
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient("videos"))
            .Returns(mockContainer.Object);

        mockContainer
            .Setup(x => x.GetBlobClient("video-123.mp4"))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.GenerateSasUri(It.IsAny<Azure.Storage.Sas.BlobSasBuilder>()))
            .Returns(expectedSasUri);

        // Act
        var result = await _service.GetSasUrlAsync(blobUri, expiry, CancellationToken.None);

        // Assert
        result.Should().Be(expectedSasUri.ToString());
        _mockBlobClient.Verify(x => x.GenerateSasUri(It.Is<Azure.Storage.Sas.BlobSasBuilder>(
            builder => builder.ExpiresOn > DateTimeOffset.UtcNow && 
                       builder.ExpiresOn < DateTimeOffset.UtcNow.Add(expiry).AddMinutes(1))), 
            Times.Once);
    }
}
