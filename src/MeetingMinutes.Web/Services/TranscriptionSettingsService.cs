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

using Azure;
using Azure.Data.Tables;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;

namespace MeetingMinutes.Web.Services;

public class TranscriptionSettingsService : ITranscriptionSettingsService
{
    private const string TableName = "appsettings";
    private const string PartitionKey = "settings";
    private const string RowKey = "transcription";

    private readonly TableClient _tableClient;
    private readonly ILogger<TranscriptionSettingsService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _tableInitialized;
    private SpeechProvider? _cached;

    public TranscriptionSettingsService(TableServiceClient tableServiceClient, ILogger<TranscriptionSettingsService> logger)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _logger = logger;
    }

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        if (_tableInitialized) return;
        await _tableClient.CreateIfNotExistsAsync(ct);
        _tableInitialized = true;
    }

    public async Task<SpeechProvider> GetProviderAsync(CancellationToken ct = default)
    {
        if (_cached.HasValue)
            return _cached.Value;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached.HasValue)
                return _cached.Value;

            await EnsureTableExistsAsync(ct);

            try
            {
                var response = await _tableClient.GetEntityAsync<AppSettings>(PartitionKey, RowKey, cancellationToken: ct);
                var entity = response.Value;

                if (Enum.TryParse<SpeechProvider>(entity.TranscriptionProvider, out var provider))
                {
                    _cached = provider;
                    return provider;
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("No transcription provider setting found; defaulting to AzureSpeech");
            }

            _cached = SpeechProvider.AzureSpeech;
            return SpeechProvider.AzureSpeech;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetProviderAsync(SpeechProvider provider, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureTableExistsAsync(ct);

            var entity = new AppSettings
            {
                PartitionKey = PartitionKey,
                RowKey = RowKey,
                TranscriptionProvider = provider.ToString()
            };

            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
            _cached = provider;

            _logger.LogInformation("Transcription provider updated to {Provider}", provider);
        }
        finally
        {
            _lock.Release();
        }
    }
}
