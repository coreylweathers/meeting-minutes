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

using MeetingMinutes.Shared.Enums;
using MeetingMinutes.Shared.Models;
using MeetingMinutes.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace MeetingMinutes.Tests.Services;

public class RoutingTranscriptionServiceTests
{
    private readonly Mock<ITranscriptionSettingsService> _mockSettings;
    private readonly Mock<ILogger<RoutingTranscriptionService>> _mockLogger;

    public RoutingTranscriptionServiceTests()
    {
        _mockSettings = new Mock<ITranscriptionSettingsService>();
        _mockLogger = new Mock<ILogger<RoutingTranscriptionService>>();
    }

    /// <summary>
    /// Minimal IServiceProvider + IKeyedServiceProvider implementation for testing keyed service resolution.
    /// Moq's As&lt;IKeyedServiceProvider&gt;() approach is unreliable with GetRequiredKeyedService extension methods.
    /// </summary>
    private sealed class FakeKeyedServiceProvider : IServiceProvider, IKeyedServiceProvider
    {
        private readonly Dictionary<string, object> _keyedServices = new(StringComparer.Ordinal);
        public string? LastRequestedKey { get; private set; }

        public void Register(string key, object service) => _keyedServices[key] = service;

        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            LastRequestedKey = serviceKey as string;
            return serviceKey is string key && _keyedServices.TryGetValue(key, out var svc) ? svc : null;
        }

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
            => GetKeyedService(serviceType, serviceKey)
               ?? throw new InvalidOperationException($"No keyed service registered for key '{serviceKey}'.");

        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task TranscribeAsync_ShouldRouteToAzure_WhenProviderIsAzureSpeech()
    {
        // Arrange
        _mockSettings.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeechProvider.AzureSpeech);

        var mockAzureService = new Mock<ISpeechTranscriptionService>();
        mockAzureService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptResult("azure transcript"));

        var fakeProvider = new FakeKeyedServiceProvider();
        fakeProvider.Register("azure", mockAzureService.Object);

        var service = new RoutingTranscriptionService(_mockSettings.Object, fakeProvider, _mockLogger.Object);

        // Act
        await service.TranscribeAsync("test-audio.wav", CancellationToken.None);

        // Assert
        fakeProvider.LastRequestedKey.Should().Be("azure");
    }

    [Fact]
    public async Task TranscribeAsync_ShouldRouteToDeepgram_WhenProviderIsDeepgram()
    {
        // Arrange
        _mockSettings.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeechProvider.Deepgram);

        var mockDeepgramService = new Mock<ISpeechTranscriptionService>();
        mockDeepgramService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptResult("deepgram transcript"));

        var fakeProvider = new FakeKeyedServiceProvider();
        fakeProvider.Register("deepgram", mockDeepgramService.Object);

        var service = new RoutingTranscriptionService(_mockSettings.Object, fakeProvider, _mockLogger.Object);

        // Act
        await service.TranscribeAsync("test-audio.wav", CancellationToken.None);

        // Assert
        fakeProvider.LastRequestedKey.Should().Be("deepgram");
    }

    [Fact]
    public async Task TranscribeAsync_ShouldReturnResultFromProvider()
    {
        // Arrange
        var expected = new TranscriptResult("hello world", [new SpeakerSegment(0, "hello world", 0.0, 2.5)]);

        _mockSettings.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeechProvider.AzureSpeech);

        var mockAzureService = new Mock<ISpeechTranscriptionService>();
        mockAzureService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var fakeProvider = new FakeKeyedServiceProvider();
        fakeProvider.Register("azure", mockAzureService.Object);

        var service = new RoutingTranscriptionService(_mockSettings.Object, fakeProvider, _mockLogger.Object);

        // Act
        var result = await service.TranscribeAsync("test-audio.wav", CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        result.Text.Should().Be("hello world");
        result.Segments.Should().HaveCount(1);
    }
}
