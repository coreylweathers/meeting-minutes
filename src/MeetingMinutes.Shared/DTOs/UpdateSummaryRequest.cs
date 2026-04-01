namespace MeetingMinutes.Shared.DTOs;

public record UpdateSummaryRequest(
    string Title,
    string[] Attendees,
    string[] KeyPoints,
    string[] ActionItems,
    string[] Decisions,
    int DurationMinutes
);
