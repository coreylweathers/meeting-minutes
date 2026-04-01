using MeetingMinutes.Api.Services;
using MeetingMinutes.Api.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Azure.AI.OpenAI;
using Azure.Identity;
using System.Security.Claims;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureTableServiceClient("tables");

// Manual AzureOpenAIClient registration (no Aspire preview package needed)
var openAiEndpoint = builder.Configuration.GetConnectionString("openai") 
    ?? builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("OpenAI connection string not configured. Set 'ConnectionStrings:openai' or 'AZURE_OPENAI_ENDPOINT'.");
builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));

builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IJobMetadataService, JobMetadataService>();
builder.Services.AddSingleton<ISummarizationService, SummarizationService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "";
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/signin-google";
    });

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();

builder.Services.AddSingleton<ISpeechTranscriptionService, SpeechTranscriptionService>();

builder.Services.AddSingleton<IFFmpegHelper, FFmpegHelper>();
builder.Services.AddHostedService<JobWorker>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapDefaultEndpoints();

var jobs = app.MapGroup("/api/jobs").RequireAuthorization();

// POST /api/jobs - Upload video and create job
jobs.MapPost("/", async (HttpContext context, IFormFile file, string title, 
    IBlobStorageService blobStorage, IJobMetadataService jobMetadata, CancellationToken ct) =>
{
    // Validate inputs
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "File is required" });

    if (string.IsNullOrWhiteSpace(title))
        return Results.BadRequest(new { error = "Title is required" });

    if (!file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "File must be a video" });

    // Generate job ID and blob name
    var jobId = Guid.NewGuid().ToString();
    var ext = Path.GetExtension(file.FileName);
    var blobName = $"{jobId}{ext}";

    // Upload video to blob storage
    string blobUri;
    using (var stream = file.OpenReadStream())
    {
        blobUri = await blobStorage.UploadVideoAsync(stream, blobName, ct);
    }

    // Get current user ID
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    // Create ProcessingJob entity
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

    // Save to metadata service
    await jobMetadata.UpdateJobAsync(job, ct);

    // Map to DTO
    var dto = MapToJobDto(job);

    return Results.Created($"/api/jobs/{jobId}", dto);
}).DisableAntiforgery();

// GET /api/jobs - List all jobs for current user
jobs.MapGet("/", async (HttpContext context, IJobMetadataService jobMetadata, CancellationToken ct) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    
    // Get all jobs and filter by userId (since there's no GetJobsByUserAsync method)
    var allJobs = await jobMetadata.ListJobsAsync(ct);
    
    // For now, return all jobs since ProcessingJob doesn't have UserId field
    // TODO: Add UserId field to ProcessingJob entity and filter properly
    var jobDtos = allJobs.Select(MapToJobDto).ToList();

    return Results.Ok(jobDtos);
});

// GET /api/jobs/{id} - Get single job
jobs.MapGet("/{id}", async (string id, HttpContext context, IJobMetadataService jobMetadata, CancellationToken ct) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job == null)
        return Results.NotFound();

    // TODO: Check if job belongs to current user once UserId field is added
    var dto = MapToJobDto(job);
    return Results.Ok(dto);
});

// GET /api/jobs/{id}/transcript - Get transcript text
jobs.MapGet("/{id}/transcript", async (string id, HttpContext context, 
    IJobMetadataService jobMetadata, IBlobStorageService blobStorage, CancellationToken ct) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job == null)
        return Results.NotFound();

    // TODO: Check if job belongs to current user once UserId field is added

    if (string.IsNullOrEmpty(job.TranscriptBlobUri))
        return Results.NotFound(new { error = "Transcript not ready" });

    var transcriptText = await blobStorage.DownloadTextAsync(job.TranscriptBlobUri, ct);
    if (transcriptText == null)
        return Results.NotFound(new { error = "Transcript not found" });

    return Results.Text(transcriptText, "text/plain");
});

// GET /api/jobs/{id}/summary - Get summary JSON
jobs.MapGet("/{id}/summary", async (string id, HttpContext context,
    IJobMetadataService jobMetadata, IBlobStorageService blobStorage, CancellationToken ct) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job == null)
        return Results.NotFound();

    // TODO: Check if job belongs to current user once UserId field is added

    if (string.IsNullOrEmpty(job.SummaryBlobUri))
        return Results.NotFound(new { error = "Summary not ready" });

    var summaryJson = await blobStorage.DownloadTextAsync(job.SummaryBlobUri, ct);
    if (summaryJson == null)
        return Results.NotFound(new { error = "Summary not found" });

    var summary = JsonSerializer.Deserialize<SummaryDto>(summaryJson, new JsonSerializerOptions 
    { 
        PropertyNameCaseInsensitive = true 
    });

    return Results.Ok(summary);
});

// PUT /api/jobs/{id}/summary - Update summary
jobs.MapPut("/{id}/summary", async (string id, UpdateSummaryRequest request, HttpContext context,
    IJobMetadataService jobMetadata, Azure.Storage.Blobs.BlobServiceClient blobServiceClient, CancellationToken ct) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    
    var job = await jobMetadata.GetJobAsync(id, ct);
    if (job == null)
        return Results.NotFound();

    // TODO: Check if job belongs to current user once UserId field is added

    if (string.IsNullOrEmpty(job.SummaryBlobUri))
        return Results.NotFound(new { error = "Summary not ready" });

    // Create SummaryDto from request
    var summary = new SummaryDto(
        request.Title,
        request.Attendees,
        request.KeyPoints,
        request.ActionItems,
        request.Decisions,
        request.DurationMinutes
    );

    // Serialize and upload
    var summaryJson = JsonSerializer.Serialize(summary);
    
    // Extract container and blob name from URI
    var uri = new Uri(job.SummaryBlobUri);
    var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
    if (segments.Length < 2)
        return Results.Problem("Invalid summary blob URI");
    
    var containerName = segments[0];
    var blobName = segments[1];
    
    // Upload to blob storage
    var container = blobServiceClient.GetBlobContainerClient(containerName);
    var blob = container.GetBlobClient(blobName);
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(summaryJson));
    await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);

    return Results.NoContent();
});

// Helper method to map ProcessingJob to JobDto
static JobDto MapToJobDto(ProcessingJob job)
{
    return new JobDto(
        job.JobId,
        job.FileName,
        Enum.Parse<JobStatus>(job.Status),
        job.BlobUri,
        job.TranscriptBlobUri,
        job.SummaryBlobUri,
        job.ErrorMessage,
        job.CreatedAt,
        job.UpdatedAt
    );
}

var auth = app.MapGroup("/api/auth");

// GET /api/auth/user - Returns current user info or 401
auth.MapGet("/user", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    return Results.Ok(new
    {
        name = ctx.User.Identity.Name,
        email = ctx.User.FindFirstValue(ClaimTypes.Email) 
                ?? ctx.User.FindFirstValue("email")
                ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
    });
});

// GET /api/auth/login/{provider} - Trigger OAuth challenge
auth.MapGet("/login/{provider}", async (string provider, HttpContext ctx) =>
{
    var scheme = provider.ToLower() switch
    {
        "microsoft" => MicrosoftAccountDefaults.AuthenticationScheme,
        "google" => GoogleDefaults.AuthenticationScheme,
        _ => null
    };
    if (scheme is null) return Results.BadRequest("Unknown provider");
    
    await ctx.ChallengeAsync(scheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
    return Results.Empty;
});

// GET /api/auth/logout - Sign out and redirect
auth.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapFallbackToFile("index.html");

app.Run();
