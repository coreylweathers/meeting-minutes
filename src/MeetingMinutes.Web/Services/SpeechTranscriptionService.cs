using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace MeetingMinutes.Web.Services;

public class SpeechTranscriptionService : ISpeechTranscriptionService
{
    private readonly string _key;
    private readonly string _region;
    private readonly ILogger<SpeechTranscriptionService> _logger;

    public SpeechTranscriptionService(IConfiguration configuration, ILogger<SpeechTranscriptionService> logger)
    {
        _key = configuration["AzureSpeech:Key"] ?? string.Empty;
        _region = configuration["AzureSpeech:Region"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(string audioFilePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_region))
            throw new InvalidOperationException("Azure Speech credentials not configured");

        _logger.LogInformation("Starting transcription of {AudioFilePath}", audioFilePath);

        var speechConfig = SpeechConfig.FromSubscription(_key, _region);
        speechConfig.OutputFormat = OutputFormat.Detailed;
        speechConfig.SpeechRecognitionLanguage = "en-US";

        using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var transcript = new StringBuilder();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        recognizer.Recognized += (_, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                transcript.Append(e.Result.Text).Append(' ');
            }
        };

        recognizer.SessionStopped += (_, _) =>
        {
            tcs.TrySetResult(transcript.ToString().TrimEnd());
        };

        recognizer.Canceled += (_, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                _logger.LogError("Transcription canceled with error: {ErrorCode} — {ErrorDetails}", e.ErrorCode, e.ErrorDetails);
                tcs.TrySetException(new InvalidOperationException($"Speech recognition canceled: {e.ErrorDetails}"));
            }
            else
            {
                tcs.TrySetResult(transcript.ToString().TrimEnd());
            }
        };

        await using var ctRegistration = ct.Register(async () =>
        {
            _logger.LogWarning("Transcription canceled via CancellationToken for {AudioFilePath}", audioFilePath);
            await recognizer.StopContinuousRecognitionAsync();
            tcs.TrySetCanceled(ct);
        });

        await recognizer.StartContinuousRecognitionAsync();

        try
        {
            var result = await tcs.Task;
            _logger.LogInformation("Transcription completed for {AudioFilePath}, length={Length}", audioFilePath, result.Length);
            return result;
        }
        finally
        {
            await recognizer.StopContinuousRecognitionAsync();
        }
    }
}
