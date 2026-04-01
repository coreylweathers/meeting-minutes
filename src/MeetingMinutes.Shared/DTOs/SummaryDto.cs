namespace MeetingMinutes.Shared.DTOs;

public record SummaryDto(
    string Title,
    string[] Attendees,
    string[] KeyPoints,
    string[] ActionItems,
    string[] Decisions,
    int DurationMinutes
);
