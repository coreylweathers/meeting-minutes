using MeetingMinutes.Shared.Enums;

namespace MeetingMinutes.Shared.DTOs;

public record JobDto(
    string JobId,
    string FileName,
    JobStatus Status,
    string? BlobUri,
    string? TranscriptBlobUri,
    string? SummaryBlobUri,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
