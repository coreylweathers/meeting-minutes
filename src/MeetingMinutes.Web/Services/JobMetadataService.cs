using Azure;
using Azure.Data.Tables;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;

namespace MeetingMinutes.Web.Services;

public class JobMetadataService : IJobMetadataService
{
    private const string TableName = "jobs";
    private const string PartitionKey = "jobs";

    private readonly TableClient _tableClient;
    private readonly ILogger<JobMetadataService> _logger;
    private bool _tableInitialized;

    public JobMetadataService(TableServiceClient tableServiceClient, ILogger<JobMetadataService> logger)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _logger = logger;
    }

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        if (_tableInitialized) return;
        await _tableClient.CreateIfNotExistsAsync(ct);
        _tableInitialized = true;
    }

    public async Task<ProcessingJob> CreateJobAsync(string fileName, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        var jobId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var job = new ProcessingJob
        {
            PartitionKey = PartitionKey,
            RowKey = jobId,
            JobId = jobId,
            FileName = fileName,
            Status = JobStatus.Pending.ToString(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _tableClient.UpsertEntityAsync(job, TableUpdateMode.Replace, ct);
        _logger.LogInformation("Job {JobId} created for file {FileName}", jobId, fileName);
        return job;
    }

    public async Task<ProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        try
        {
            var response = await _tableClient.GetEntityAsync<ProcessingJob>(PartitionKey, jobId, cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListJobsAsync(CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        var results = new List<ProcessingJob>();
        await foreach (var job in _tableClient.QueryAsync<ProcessingJob>(
            filter: $"PartitionKey eq '{PartitionKey}'",
            cancellationToken: ct))
        {
            results.Add(job);
        }

        return results;
    }

    public async Task UpdateJobAsync(ProcessingJob job, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _tableClient.UpsertEntityAsync(job, TableUpdateMode.Replace, ct);
    }

    public async Task UpdateStatusAsync(string jobId, JobStatus status, string? errorMessage = null, CancellationToken ct = default)
    {
        var job = await GetJobAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

        job.Status = status.ToString();
        job.ErrorMessage = errorMessage;

        _logger.LogInformation("Job {JobId} status updated to {Status}", jobId, status);
        await UpdateJobAsync(job, ct);
    }
}
