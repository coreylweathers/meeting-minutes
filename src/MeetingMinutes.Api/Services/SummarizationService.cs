using System.Text.Json;
using OpenAI;
using MeetingMinutes.Shared.DTOs;
using OpenAI.Chat;

namespace MeetingMinutes.Api.Services;

public class SummarizationService : ISummarizationService
{
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;

    public SummarizationService(OpenAIClient client)
    {
        _client = client;
        _chatClient = _client.GetChatClient("gpt-4o-mini");
    }

    public async Task<SummaryDto> SummarizeAsync(string transcript, CancellationToken ct = default)
    {
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

            // Parse the JSON response into SummaryDto
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var summary = JsonSerializer.Deserialize<SummaryDto>(content, options);

            if (summary == null)
            {
                throw new InvalidOperationException("Failed to deserialize summary from OpenAI response");
            }

            return summary;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON response from OpenAI: {ex.Message}", ex);
        }
    }
}
