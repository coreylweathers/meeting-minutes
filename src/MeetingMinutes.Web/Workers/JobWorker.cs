using Azure.Storage.Blobs;
using MeetingMinutes.Web.Services;
using MeetingMinutes.Shared.Enums;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace MeetingMinutes.Web.Workers;

public class JobWorker : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("MeetingMinutes.JobWorker");

    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BlobServiceClient _blobServiceClient;

    private readonly Meter _meter = new("MeetingMinutes.JobWorker");
    private readonly Counter<long> _jobsStarted;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;

    public JobWorker(
        ILogger<JobWorker> logger,
        IServiceScopeFactory scopeFactory,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _blobServiceClient = blobServiceClient;

        _jobsStarted = _meter.CreateCounter<long>("jobs_started", description: "Number of jobs started");
        _jobsCompleted = _meter.CreateCounter<long>("jobs_completed", description: "Number of jobs completed");
        _jobsFailed = _meter.CreateCounter<long>("jobs_failed", description: "Number of jobs failed");
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
        using var activity = ActivitySource.StartActivity("ProcessJob");
        activity?.SetTag("job.id", jobId);

        var sw = Stopwatch.StartNew();
        string? videoTempPath = null;
        string? audioTempPath = null;

        _jobsStarted.Add(1);

        try
        {
            _logger.LogInformation("Processing job {JobId}", jobId);

            var job = await jobService.GetJobAsync(jobId, ct);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found", jobId);
                return;
            }

            activity?.SetTag("job.filename", job.FileName);

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

            // 6. Store transcript in blob storage
            var transcriptBlobUri = await blobService.UploadTextAsync(transcript, $"{jobId}.txt", ct);

            job.TranscriptBlobUri = transcriptBlobUri;
            await jobService.UpdateJobAsync(job, ct);

            // 7. Update status → Summarizing
            _logger.LogInformation("Job {JobId}: Summarizing transcript", jobId);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Summarizing, ct: ct);

            // 8. Summarize transcript via SummarizationService
            var summaryDto = await summarizer.SummarizeAsync(transcript, ct);

            // 9. Serialize SummaryDto to JSON, store in blob
            var summaryJson = JsonSerializer.Serialize(summaryDto, new JsonSerializerOptions { WriteIndented = true });
            var summaryBlobUri = await UploadToContainerAsync("summaries", $"{jobId}.json", summaryJson, ct);

            job.SummaryBlobUri = summaryBlobUri;
            await jobService.UpdateJobAsync(job, ct);

            // 10. Update status → Completed
            await jobService.UpdateStatusAsync(jobId, JobStatus.Completed, ct: ct);

            sw.Stop();
            activity?.SetTag("job.status", "completed");
            _jobsCompleted.Add(1);
            _logger.LogInformation("Job {JobId} completed successfully in {ElapsedMs}ms", jobId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetTag("job.status", "failed");
            _jobsFailed.Add(1);
            _logger.LogError(ex, "Job {JobId} failed after {ElapsedMs}ms", jobId, sw.ElapsedMilliseconds);
            await jobService.UpdateStatusAsync(jobId, JobStatus.Failed, ex.Message, ct);
        }
        finally
        {
            if (videoTempPath != null && File.Exists(videoTempPath))
            {
                try { File.Delete(videoTempPath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp video file: {Path}", videoTempPath); }
            }

            if (audioTempPath != null && File.Exists(audioTempPath))
            {
                try { File.Delete(audioTempPath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp audio file: {Path}", audioTempPath); }
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
