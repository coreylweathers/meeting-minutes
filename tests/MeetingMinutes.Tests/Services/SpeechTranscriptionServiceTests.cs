using MeetingMinutes.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace MeetingMinutes.Tests.Services;

public class SpeechTranscriptionServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<SpeechTranscriptionService>> _mockLogger;

    public SpeechTranscriptionServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SpeechTranscriptionService>>();
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithValidConfiguration()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:Key"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");

        // Act
        var service = new SpeechTranscriptionService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeAsync_ShouldThrow_WhenCredentialsNotConfigured()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:Key"]).Returns(string.Empty);
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns(string.Empty);

        var service = new SpeechTranscriptionService(_mockConfiguration.Object, _mockLogger.Object);
        var audioFilePath = "test-audio.wav";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.TranscribeAsync(audioFilePath, CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_ShouldThrow_WhenOnlyKeyIsMissing()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:Key"]).Returns(string.Empty);
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");

        var service = new SpeechTranscriptionService(_mockConfiguration.Object, _mockLogger.Object);
        var audioFilePath = "test-audio.wav";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.TranscribeAsync(audioFilePath, CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_ShouldThrow_WhenOnlyRegionIsMissing()
    {
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:Key"]).Returns("test-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns(string.Empty);

        var service = new SpeechTranscriptionService(_mockConfiguration.Object, _mockLogger.Object);
        var audioFilePath = "test-audio.wav";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.TranscribeAsync(audioFilePath, CancellationToken.None));
    }

    [Trait("Category", "Integration")]
    [Fact(Skip = "Requires Azure Speech SDK and valid credentials")]
    public async Task TranscribeAsync_ShouldReturnTranscript_WithValidAudioFile()
    {
        // This test requires:
        // 1. Valid Azure Speech credentials
        // 2. A real .wav audio file
        // 3. Azure Speech SDK to be fully functional
        // 
        // Mark as Integration test and skip by default
        // Run with: dotnet test --filter Category=Integration
        
        // Arrange
        _mockConfiguration.Setup(x => x["AzureSpeech:Key"]).Returns("real-key");
        _mockConfiguration.Setup(x => x["AzureSpeech:Region"]).Returns("eastus");

        var service = new SpeechTranscriptionService(_mockConfiguration.Object, _mockLogger.Object);
        var audioFilePath = "path/to/real/audio.wav";

        // Act
        var result = await service.TranscribeAsync(audioFilePath, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
}
