using Azure.Storage.Blobs;
using MeetingMinutes.Api.Services;
using MeetingMinutes.Shared.Enums;
using System.Text.Json;

namespace MeetingMinutes.Api.Workers;

public class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BlobServiceClient _blobServiceClient;

    public JobWorker(
        ILogger<JobWorker> logger,
        IServiceScopeFactory scopeFactory,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _blobServiceClient = blobServiceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processing loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("JobWorker stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IJobMetadataService>();

        var allJobs = await jobService.ListJobsAsync(ct);
        var pendingJobs = allJobs.Where(j => j.Status == JobStatus.Pending.ToString()).ToList();

        foreach (var job in pendingJobs)
        {
            var blobService = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
            var ffmpeg = scope.ServiceProvider.GetRequiredService<IFFmpegHelper>();
            var speech = scope.ServiceProvider.GetRequiredService<ISpeechTranscriptionService>();
            var summarizer = scope.ServiceProvider.GetRequiredService<ISummarizationService>();

            await ProcessJobAsync(job.JobId, jobService, blobService, ffmpeg, speech, summarizer, ct);
        }
    }

    private async Task ProcessJobAsync(
        string jobId,
        IJobMetadataService jobService,
        IBlobStorageService blobService,
        IFFmpegHelper ffmpeg,
        ISpeechTranscriptionService speech,
        ISummarizationService summarizer,
        CancellationToken ct)
    {
        string? videoTempPath = null;
        string? audioTempPath = null;

        try
        {
            _logger.LogInformation("Processing job {JobId}", jobId);

            // Get job details
            var job = await jobService.GetJobAsync(jobId, ct);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found", jobId);
                return;
            }

            if (string.IsNullOrEmpty(job.BlobUri))
            {
                _logger.LogError("Job {JobId} has no video blob URI", jobId);
                await jobService.UpdateStatusAsync(jobId, JobStatus.Failed, "No video blob URI", ct);
                return;
            }

            // 1. Update status → ExtractingAudio
            _logger.LogInformation("Job {JobId}: Extracting audio", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.ExtractingAudio, ct: ct);

            // 2. Download video blob → temp file
            videoTempPath = Path.GetTempFileName();
            await DownloadBlobToFileAsync(job.BlobUri, videoTempPath, ct);

            // 3. Extract audio (WAV) via FFmpegHelper → temp file
            audioTempPath = await ffmpeg.ExtractAudioAsync(videoTempPath, ct);

            // 4. Update status → Transcribing
            _logger.LogInformation("Job {JobId}: Transcribing audio", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Transcribing, ct: ct);

            // 5. Transcribe audio via SpeechTranscriptionService
            var transcript = await speech.TranscribeAsync(audioTempPath, ct);

            // 6. Store transcript in blob storage (container: "transcripts", blob: "{jobId}.txt")
            var transcriptBlobUri = await blobService.UploadTextAsync(transcript, $"{jobId}.txt", ct);
            
            // Update job with transcript URI
            job.TranscriptBlobUri = transcriptBlobUri;
            await jobService.UpdateJobAsync(job, ct);

            // 7. Update status → Summarizing
            _logger.LogInformation("Job {JobId}: Summarizing transcript", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Summarizing, ct: ct);

            // 8. Summarize transcript via SummarizationService
            var summaryDto = await summarizer.SummarizeAsync(transcript, ct);

            // 9. Serialize SummaryDto to JSON, store in blob ("summaries", "{jobId}.json")
            var summaryJson = JsonSerializer.Serialize(summaryDto, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var summaryBlobUri = await UploadToContainerAsync("summaries", $"{jobId}.json", summaryJson, ct);

            // Update job with summary URI
            job.SummaryBlobUri = summaryBlobUri;
            await jobService.UpdateJobAsync(job, ct);

            // 10. Update status → Completed
            _logger.LogInformation("Job {JobId}: Completed successfully", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Completed, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Failed, ex.Message, ct);
        }
        finally
        {
            // Delete temp files
            if (videoTempPath != null && File.Exists(videoTempPath))
            {
                try
                {
                    File.Delete(videoTempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp video file: {Path}", videoTempPath);
                }
            }

            if (audioTempPath != null && File.Exists(audioTempPath))
            {
                try
                {
                    File.Delete(audioTempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp audio file: {Path}", audioTempPath);
                }
            }
        }
    }

    private async Task DownloadBlobToFileAsync(string blobUri, string destinationPath, CancellationToken ct)
    {
        var uri = new Uri(blobUri);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        if (segments.Length < 2)
            throw new InvalidOperationException($"Invalid blob URI: {blobUri}");

        var containerName = segments[0];
        var blobName = segments[1];

        var blob = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        await blob.DownloadToAsync(destinationPath, ct);
    }

    private async Task<string> UploadToContainerAsync(string containerName, string blobName, string content, CancellationToken ct)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        return blob.Uri.ToString();
    }
}
