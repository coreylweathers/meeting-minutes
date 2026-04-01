using Azure;
using Azure.Data.Tables;

namespace MeetingMinutes.Shared.Entities;

public class ProcessingJob : ITableEntity
{
    public string PartitionKey { get; set; } = "jobs";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string JobId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? BlobUri { get; set; }
    public string? AudioBlobUri { get; set; }
    public string? TranscriptBlobUri { get; set; }
    public string? SummaryBlobUri { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
