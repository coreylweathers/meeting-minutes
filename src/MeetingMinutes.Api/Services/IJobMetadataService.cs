using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;

namespace MeetingMinutes.Api.Services;

public interface IJobMetadataService
{
    Task<ProcessingJob> CreateJobAsync(string fileName, CancellationToken ct = default);
    Task<ProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessingJob>> ListJobsAsync(CancellationToken ct = default);
    Task UpdateJobAsync(ProcessingJob job, CancellationToken ct = default);
    Task UpdateStatusAsync(string jobId, JobStatus status, string? errorMessage = null, CancellationToken ct = default);
}
