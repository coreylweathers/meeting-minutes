using MeetingMinutes.Shared.DTOs;

namespace MeetingMinutes.Web.Services;

public interface ISummarizationService
{
    Task<SummaryDto> SummarizeAsync(string transcript, CancellationToken ct = default);
}
