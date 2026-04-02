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

using System.Diagnostics;
using System.Text.Json;
using OpenAI;
using MeetingMinutes.Shared.DTOs;
using OpenAI.Chat;

namespace MeetingMinutes.Web.Services;

public class SummarizationService : ISummarizationService
{
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly ILogger<SummarizationService> _logger;

    public SummarizationService(OpenAIClient client, ILogger<SummarizationService> logger)
    {
        _client = client;
        _chatClient = _client.GetChatClient("gpt-4o-mini");
        _logger = logger;
    }

    public async Task<SummaryDto> SummarizeAsync(string transcript, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting summarization for transcript of length {Length}", transcript.Length);
        var sw = Stopwatch.StartNew();

        var systemPrompt = """
            You are an expert meeting summarization assistant. Analyze the provided meeting transcript and extract structured information.
            
            Return your response as valid JSON with the following structure:
            {
              "title": "A concise title for the meeting",
              "attendees": ["List of attendee names mentioned"],
              "key_points": ["Main discussion points and topics covered"],
              "action_items": ["Tasks or actions that were assigned or discussed"],
              "decisions": ["Key decisions or conclusions reached"],
              "duration_minutes": estimated_duration_as_integer
            }
            
            Important:
            - Return ONLY the JSON object, no additional text or markdown formatting
            - Use empty arrays [] if no items are found for a category
            - Set duration_minutes to 0 if duration cannot be determined
            - Keep all text concise and professional
            """;

        var userPrompt = $"Please analyze this meeting transcript and provide a structured summary:\n\n{transcript}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        try
        {
            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            var content = response.Value.Content[0].Text;

            _logger.LogInformation("Summarization completed in {ElapsedMs}ms, response length {Length}",
                sw.ElapsedMilliseconds, content.Length);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var summary = JsonSerializer.Deserialize<SummaryDto>(content, options);

            if (summary == null)
                throw new InvalidOperationException("Failed to deserialize summary from OpenAI response");

            return summary;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from OpenAI after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw new InvalidOperationException($"Failed to parse JSON response from OpenAI: {ex.Message}", ex);
        }
    }
}
