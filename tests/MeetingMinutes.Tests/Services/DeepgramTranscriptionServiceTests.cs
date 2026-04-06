// Meeting Minutes - AI-powered meeting transcription and summarization.
// Copyright (C) 2026 Corey Weathers
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using MeetingMinutes.Web.Options;
using MeetingMinutes.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace MeetingMinutes.Tests.Services;

public class DeepgramTranscriptionServiceTests
{
    private readonly Mock<ILogger<DeepgramTranscriptionService>> _mockLogger;

    public DeepgramTranscriptionServiceTests()
    {
        _mockLogger = new Mock<ILogger<DeepgramTranscriptionService>>();
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithValidApiKey()
    {
        // Arrange
        var options = Options.Create(new DeepgramOptions { ApiKey = "test-api-key" });

        // Act
        var service = new DeepgramTranscriptionService(options, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task TranscribeAsync_ShouldThrow_WhenApiKeyIsEmpty()
    {
        // Arrange
        var options = Options.Create(new DeepgramOptions { ApiKey = "" });
        var service = new DeepgramTranscriptionService(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.TranscribeAsync("test-audio.wav", CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_ShouldThrow_WhenApiKeyIsNull()
    {
        // Arrange
        var options = Options.Create(new DeepgramOptions { ApiKey = null! });
        var service = new DeepgramTranscriptionService(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.TranscribeAsync("test-audio.wav", CancellationToken.None));
    }

    [Trait("Category", "Integration")]
    [Fact(Skip = "Requires Deepgram API key and a real audio file")]
    public async Task TranscribeAsync_ShouldReturnTranscript_WithValidAudioFile()
    {
        // This test requires:
        // 1. Valid Deepgram API key
        // 2. A real audio file
        //
        // Mark as Integration test and skip by default
        // Run with: dotnet test --filter Category=Integration

        // Arrange
        var options = Options.Create(new DeepgramOptions { ApiKey = "real-api-key" });
        var service = new DeepgramTranscriptionService(options, _mockLogger.Object);

        // Act
        var result = await service.TranscribeAsync("path/to/real/audio.wav", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().NotBeNullOrEmpty();
    }
}
