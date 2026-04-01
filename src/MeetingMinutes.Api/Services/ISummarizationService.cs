using MeetingMinutes.Shared.DTOs;

namespace MeetingMinutes.Api.Services;

public interface ISummarizationService
{
    Task<SummaryDto> SummarizeAsync(string transcript, CancellationToken ct = default);
}
