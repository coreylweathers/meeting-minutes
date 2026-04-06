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

namespace MeetingMinutes.Web.Services;

public sealed class RoutingTranscriptionService : ISpeechTranscriptionService
{
    private readonly ITranscriptionSettingsService _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoutingTranscriptionService> _logger;

    public RoutingTranscriptionService(
        ITranscriptionSettingsService settings,
        IServiceProvider serviceProvider,
        ILogger<RoutingTranscriptionService> logger)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TranscriptResult> TranscribeAsync(string audioFilePath, CancellationToken ct = default)
    {
        var provider = await _settings.GetProviderAsync(ct);
        _logger.LogInformation("Routing transcription to provider: {Provider}", provider);

        var key = provider == SpeechProvider.Deepgram ? "deepgram" : "azure";
        var service = _serviceProvider.GetRequiredKeyedService<ISpeechTranscriptionService>(key);
        return await service.TranscribeAsync(audioFilePath, ct);
    }
}
