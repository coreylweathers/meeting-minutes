// Meeting Minutes - AI-powered meeting transcription and summarization.
// Copyright (C) 2026 Corey Weathers
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using FFMpegCore;
using MeetingMinutes.Web;
using MeetingMinutes.Web.Components;
using MeetingMinutes.Web.Options;
using MeetingMinutes.Web.Services;
using MeetingMinutes.Web.Workers;
using OpenAI;
using System.ClientModel;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure FFMpegCore binary path — handles winget installs that don't add to PATH
var ffmpegBinDir = FFmpegPathResolver.Resolve();
if (!string.IsNullOrEmpty(ffmpegBinDir))
    GlobalFFOptions.Configure(opts => opts.BinaryFolder = ffmpegBinDir);

builder.AddServiceDefaults();

// Azure Storage (Aspire-managed in Aspire mode; real URLs in standalone)
builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureTableServiceClient("tables");

// OpenAI client — key from ConnectionStrings:openai
var openAiApiKey = builder.Configuration.GetConnectionString("openai")
    ?? throw new InvalidOperationException("OpenAI API key not configured. Set 'ConnectionStrings:openai'.");
builder.Services.AddSingleton(new OpenAIClient(new ApiKeyCredential(openAiApiKey)));

// Razor components with Interactive Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Antiforgery (required by Blazor Server)
builder.Services.AddAntiforgery();

// Business services
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IJobMetadataService, JobMetadataService>();
builder.Services.AddSingleton<ISummarizationService, SummarizationService>();
builder.Services.AddSingleton<IFFmpegHelper, FFmpegHelper>();

// Options binding
builder.Services.Configure<AzureSpeechOptions>(builder.Configuration.GetSection("AzureSpeech"));
builder.Services.Configure<DeepgramOptions>(builder.Configuration.GetSection("Deepgram"));

// Aspire credential wiring — PostConfigure overrides appsettings.json with ConnectionStrings values
var deepgramKey = builder.Configuration.GetConnectionString("deepgram");
if (!string.IsNullOrEmpty(deepgramKey))
{
    builder.Services.PostConfigure<DeepgramOptions>(opts =>
    {
        opts.ApiKey = deepgramKey;
    });
}

var speechConnStr = builder.Configuration.GetConnectionString("speech");
if (!string.IsNullOrEmpty(speechConnStr))
{
    var parts = speechConnStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2))
        .Where(p => p.Length == 2)
        .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

    var speechKey = parts.GetValueOrDefault("Key");
    var endpoint = parts.GetValueOrDefault("Endpoint");
    var speechRegion = string.Empty;
    if (!string.IsNullOrEmpty(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        speechRegion = endpointUri.Host.Split('.')[0];

    if (!string.IsNullOrEmpty(speechKey) && !string.IsNullOrEmpty(speechRegion))
    {
        builder.Services.PostConfigure<AzureSpeechOptions>(opts =>
        {
            opts.Key = speechKey;
            opts.Region = speechRegion;
        });
    }
}

// Keyed service registrations (concrete providers)
builder.Services.AddKeyedSingleton<ISpeechTranscriptionService, AzureSpeechTranscriptionService>("azure");
builder.Services.AddKeyedSingleton<ISpeechTranscriptionService, DeepgramTranscriptionService>("deepgram");

// Settings service
builder.Services.AddSingleton<ITranscriptionSettingsService, TranscriptionSettingsService>();

// Primary registration — delegates to active provider via RoutingTranscriptionService
builder.Services.AddSingleton<ISpeechTranscriptionService, RoutingTranscriptionService>();

// Background job processor
builder.Services.AddHostedService<JobWorker>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Job endpoints
var jobs = app.MapGroup("/api/jobs");

jobs.MapPost("/", async (
    HttpContext context,
    IFormFile file,
    string title,
    IBlobStorageService blobStorage,
    IJobMetadataService jobMetadata,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "File is required" });
    if (string.IsNullOrWhiteSpace(title))
        return Results.BadRequest(new { error = "Title is required" });
    if (!file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "File must be a video" });

    var jobId = Guid.NewGuid().ToString();
    var ext = Path.GetExtension(file.FileName);
    var blobName = $"{jobId}{ext}";

    string blobUri;
    using (var stream = file.OpenReadStream())
        blobUri = await blobStorage.UploadVideoAsync(stream, blobName, ct);

    logger.LogInformation("Job {JobId} created for file {FileName} ({Size} bytes)",
        jobId, file.FileName, file.Length);

    var job = new ProcessingJob
    {
        PartitionKey = "jobs",
        RowKey = jobId,
        JobId = jobId,
        FileName = file.FileName,
        Status = JobStatus.Pending.ToString(),
        BlobUri = blobUri,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    await jobMetadata.UpdateJobAsync(job, ct);
    return Results.Created($"/api/jobs/{jobId}", MapToJobDto(job));
}).DisableAntiforgery();

jobs.MapGet("/", async (IJobMetadataService jobMetadata, CancellationToken ct) =>
{
    var allJobs = await jobMetadata.ListJobsAsync(ct);
    return Results.Ok(allJobs.Select(MapToJobDto).ToList());
});

jobs.MapGet("/{id}", async (string id, IJobMetadataService jobMetadata, CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    return job is null ? Results.NotFound() : Results.Ok(MapToJobDto(job));
});

jobs.MapGet("/{id}/transcript", async (string id, IJobMetadataService jobMetadata, IBlobStorageService blobStorage, CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job is null) return Results.NotFound();
    if (string.IsNullOrEmpty(job.TranscriptBlobUri))
        return Results.NotFound(new { error = "Transcript not ready" });
    var text = await blobStorage.DownloadTextAsync(job.TranscriptBlobUri, ct);
    return text is null ? Results.NotFound(new { error = "Transcript not found" }) : Results.Text(text, "text/plain");
});

jobs.MapGet("/{id}/summary", async (string id, IJobMetadataService jobMetadata, IBlobStorageService blobStorage, CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job is null) return Results.NotFound();
    if (string.IsNullOrEmpty(job.SummaryBlobUri))
        return Results.NotFound(new { error = "Summary not ready" });
    var json = await blobStorage.DownloadTextAsync(job.SummaryBlobUri, ct);
    if (json is null) return Results.NotFound(new { error = "Summary not found" });
    var summary = JsonSerializer.Deserialize<SummaryDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return Results.Ok(summary);
});

jobs.MapPut("/{id}/summary", async (
    string id,
    UpdateSummaryRequest request,
    IJobMetadataService jobMetadata,
    Azure.Storage.Blobs.BlobServiceClient blobServiceClient,
    CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job is null) return Results.NotFound();
    if (string.IsNullOrEmpty(job.SummaryBlobUri))
        return Results.NotFound(new { error = "Summary not ready" });

    var summary = new SummaryDto(request.Title, request.Attendees, request.KeyPoints, request.ActionItems, request.Decisions, request.DurationMinutes);
    var json = JsonSerializer.Serialize(summary);

    // GOOD — handles both Azurite and production Azure URIs
    var blobUri = new Azure.Storage.Blobs.BlobUriBuilder(new Uri(job.SummaryBlobUri));
    var container = blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName);
    var blob = container.GetBlobClient(blobUri.BlobName);
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
    return Results.NoContent();
});

// POST /api/jobs/{id}/summarize — user chose AI summarization
jobs.MapPost("/{id}/summarize", async (
    string id,
    IJobMetadataService jobMetadata,
    IBlobStorageService blobStorage,
    ISummarizationService summarizer,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job is null) return Results.NotFound();
    if (job.Status != JobStatus.Transcribed.ToString())
        return Results.BadRequest(new { error = "Job is not in Transcribed state" });
    if (string.IsNullOrEmpty(job.TranscriptBlobUri))
        return Results.BadRequest(new { error = "No transcript available" });

    await jobMetadata.UpdateStatusAsync(id, JobStatus.Summarizing, ct: ct);

    try
    {
        var transcriptText = await blobStorage.DownloadTextAsync(job.TranscriptBlobUri, ct);
        if (string.IsNullOrEmpty(transcriptText))
            return Results.Problem("Transcript text could not be read");

        var summaryDto = await summarizer.SummarizeAsync(transcriptText, ct);
        var summaryJson = JsonSerializer.Serialize(summaryDto, new JsonSerializerOptions { WriteIndented = true });
        var summaryBlobUri = await blobStorage.UploadSummaryAsync(summaryJson, $"{id}.json", ct);

        job = await jobMetadata.GetJobAsync(id, ct);
        if (job is not null)
        {
            job.SummaryBlobUri = summaryBlobUri;
            await jobMetadata.UpdateJobAsync(job, ct);
        }

        await jobMetadata.UpdateStatusAsync(id, JobStatus.Completed, ct: ct);
        logger.LogInformation("Job {JobId} summarized and completed via user action", id);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Summarization failed for job {JobId}", id);
        await jobMetadata.UpdateStatusAsync(id, JobStatus.Failed, ex.Message, ct);
        return Results.Problem(ex.Message);
    }
});

// POST /api/jobs/{id}/complete — user chose transcript-only (no AI)
jobs.MapPost("/{id}/complete", async (
    string id,
    IJobMetadataService jobMetadata,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job is null) return Results.NotFound();
    if (job.Status != JobStatus.Transcribed.ToString())
        return Results.BadRequest(new { error = "Job is not in Transcribed state" });

    await jobMetadata.UpdateStatusAsync(id, JobStatus.Completed, ct: ct);
    logger.LogInformation("Job {JobId} completed as transcript-only via user action", id);
    return Results.NoContent();
});

static JobDto MapToJobDto(ProcessingJob job) => new(
    job.JobId, job.FileName, Enum.Parse<JobStatus>(job.Status),
    job.BlobUri, job.TranscriptBlobUri, job.SummaryBlobUri,
    job.ErrorMessage, job.CreatedAt, job.UpdatedAt
);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

