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

using System.Text;
using Deepgram;
using Deepgram.Models.Listen.v1.REST;
using MeetingMinutes.Shared.Models;
using MeetingMinutes.Web.Options;
using Microsoft.Extensions.Options;

namespace MeetingMinutes.Web.Services;

public class DeepgramTranscriptionService : ISpeechTranscriptionService
{
    private readonly string _apiKey;
    private readonly ILogger<DeepgramTranscriptionService> _logger;

    public DeepgramTranscriptionService(IOptions<DeepgramOptions> options, ILogger<DeepgramTranscriptionService> logger)
    {
        _apiKey = options.Value.ApiKey;
        _logger = logger;
    }

    public async Task<TranscriptResult> TranscribeAsync(string audioFilePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Deepgram API key not configured");

        _logger.LogInformation("Starting Deepgram transcription of {AudioFilePath}", audioFilePath);

        var audioBytes = await File.ReadAllBytesAsync(audioFilePath, ct);

        var client = ClientFactory.CreateListenRESTClient(_apiKey);

        var schema = new PreRecordedSchema
        {
            Model = "nova-3",
            Punctuate = true,
            Diarize = true,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var response = await client.TranscribeFile(audioBytes, schema, cts);

        var channels = response?.Results?.Channels;
        if (channels == null || channels.Count == 0)
        {
            _logger.LogWarning("Deepgram returned no channels for {AudioFilePath}", audioFilePath);
            return new TranscriptResult(string.Empty);
        }

        var words = channels[0].Alternatives?[0].Words;
        if (words == null || words.Count == 0)
        {
            var rawTranscript = channels[0].Alternatives?[0].Transcript ?? string.Empty;
            _logger.LogInformation("Deepgram transcription completed (no word-level data), length={Length}", rawTranscript.Length);
            return new TranscriptResult(rawTranscript);
        }

        var (formattedText, segments) = BuildDiarizedTranscript(words);

        _logger.LogInformation("Deepgram transcription completed for {AudioFilePath}, length={Length}, segments={Segments}",
            audioFilePath, formattedText.Length, segments.Count);

        return new TranscriptResult(formattedText, segments);
    }

    private static (string Text, List<SpeakerSegment> Segments) BuildDiarizedTranscript(
        IReadOnlyList<Word> words)
    {
        var segments = new List<SpeakerSegment>();
        var fullText = new StringBuilder();

        var currentSpeaker = words[0].Speaker ?? 0;
        var segmentWords = new StringBuilder();
        var segmentStart = (double)(words[0].Start ?? 0m);
        var segmentEnd = (double)(words[0].End ?? 0m);

        void FlushSegment()
        {
            var text = segmentWords.ToString().TrimEnd();
            segments.Add(new SpeakerSegment(currentSpeaker, text, segmentStart, segmentEnd));
            fullText.AppendLine($"[Speaker {currentSpeaker + 1}]: {text}");
        }

        foreach (var word in words)
        {
            var speaker = word.Speaker ?? currentSpeaker;

            if (speaker != currentSpeaker)
            {
                FlushSegment();
                currentSpeaker = speaker;
                segmentWords.Clear();
                segmentStart = (double)(word.Start ?? 0m);
            }

            segmentWords.Append(word.PunctuatedWord ?? word.HeardWord ?? string.Empty).Append(' ');
            segmentEnd = (double)(word.End ?? 0m);
        }

        FlushSegment();

        return (fullText.ToString().TrimEnd(), segments);
    }
}
