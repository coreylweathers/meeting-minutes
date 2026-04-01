namespace MeetingMinutes.Api.Services;

public interface ISpeechTranscriptionService
{
    Task<string> TranscribeAsync(string audioFilePath, CancellationToken ct = default);
}
