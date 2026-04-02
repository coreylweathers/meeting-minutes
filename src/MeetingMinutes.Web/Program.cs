using MeetingMinutes.Web;
using MeetingMinutes.Web.Components;
using MeetingMinutes.Web.Services;
using MeetingMinutes.Web.Workers;
using OpenAI;
using System.ClientModel;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<ISpeechTranscriptionService, SpeechTranscriptionService>();
builder.Services.AddSingleton<IFFmpegHelper, FFmpegHelper>();

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

    var uri = new Uri(job.SummaryBlobUri);
    var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
    if (segments.Length < 2) return Results.Problem("Invalid summary blob URI");

    var container = blobServiceClient.GetBlobContainerClient(segments[0]);
    var blob = container.GetBlobClient(segments[1]);
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
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

