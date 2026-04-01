using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MeetingMinutes.Web.Pages;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class JobDetailPageTests : TestContext
{
    [Fact]
    public void JobDetailPage_Requires_Authorization()
    {
        // Arrange
        var authorizeAttribute = typeof(JobDetail)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .FirstOrDefault();
        
        // Assert
        authorizeAttribute.Should().NotBeNull("JobDetail page should have [Authorize] attribute");
    }
    
    [Fact]
    public async Task JobDetailPage_Shows_LoadingSpinner_Initially()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var mockHandler = new DelayedJobDetailMockHandler();
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "job1"));
        
        // Assert - before initialization completes
        cut.Markup.Should().Contain("Loading");
    }
    
    [Fact]
    public async Task JobDetailPage_Shows_JobNotFound_WhenJobDoesNotExist()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var mockHandler = new JobDetailNotFoundMockHandler();
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "nonexistent"));
        await Task.Delay(100);
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("Job not found");
    }
    
    [Fact]
    public async Task JobDetailPage_Displays_JobFileName()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var job = new JobDto(
            JobId: "job1",
            FileName: "test-meeting.mp4",
            Status: JobStatus.Completed,
            BlobUri: null,
            TranscriptBlobUri: null,
            SummaryBlobUri: null,
            ErrorMessage: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        var mockHandler = new JobDetailMockHandler(job, null, null);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "job1"));
        await Task.Delay(100);
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("test-meeting.mp4");
    }
    
    [Fact]
    public async Task JobDetailPage_Shows_ProcessingSpinner_ForPendingJob()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var job = new JobDto(
            JobId: "job1",
            FileName: "processing.mp4",
            Status: JobStatus.Transcribing,
            BlobUri: null,
            TranscriptBlobUri: null,
            SummaryBlobUri: null,
            ErrorMessage: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        var mockHandler = new JobDetailMockHandler(job, null, null);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "job1"));
        await Task.Delay(100);
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("Processing");
    }
    
    [Fact]
    public async Task JobDetailPage_Shows_ErrorMessage_ForFailedJob()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var job = new JobDto(
            JobId: "job1",
            FileName: "failed.mp4",
            Status: JobStatus.Failed,
            BlobUri: null,
            TranscriptBlobUri: null,
            SummaryBlobUri: null,
            ErrorMessage: "Transcription failed",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        var mockHandler = new JobDetailMockHandler(job, null, null);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "job1"));
        await Task.Delay(100);
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("Error:");
        cut.Markup.Should().Contain("Transcription failed");
    }
    
    [Fact]
    public async Task JobDetailPage_Shows_TranscriptAndSummary_ForCompletedJob()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        var job = new JobDto(
            JobId: "job1",
            FileName: "completed.mp4",
            Status: JobStatus.Completed,
            BlobUri: null,
            TranscriptBlobUri: null,
            SummaryBlobUri: null,
            ErrorMessage: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        var transcript = "This is the meeting transcript.";
        var summary = new SummaryDto(
            Title: "Test Meeting",
            Attendees: new[] { "Alice", "Bob" },
            KeyPoints: new[] { "Point 1", "Point 2" },
            ActionItems: new[] { "Action 1" },
            Decisions: new[] { "Decision 1" },
            DurationMinutes: 30
        );
        var mockHandler = new JobDetailMockHandler(job, transcript, summary);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Act
        var cut = RenderComponent<JobDetail>(parameters => parameters
            .Add(p => p.Id, "job1"));
        await Task.Delay(200);
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("Transcript");
        cut.Markup.Should().Contain("Summary");
        cut.Markup.Should().Contain("Test Meeting");
    }
}

internal class JobDetailMockHandler : HttpMessageHandler
{
    private readonly JobDto? _job;
    private readonly string? _transcript;
    private readonly SummaryDto? _summary;

    public JobDetailMockHandler(JobDto? job, string? transcript, SummaryDto? summary)
    {
        _job = job;
        _transcript = transcript;
        _summary = summary;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        
        if (path.StartsWith("/api/jobs/") && path.EndsWith("/transcript"))
        {
            if (_transcript != null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_transcript, System.Text.Encoding.UTF8, "text/plain")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        
        if (path.StartsWith("/api/jobs/") && path.EndsWith("/summary"))
        {
            if (_summary != null)
            {
                var json = JsonSerializer.Serialize(_summary);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        
        if (path.StartsWith("/api/jobs/"))
        {
            if (_job != null)
            {
                var json = JsonSerializer.Serialize(_job);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal class DelayedJobDetailMockHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(10000, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

internal class JobDetailNotFoundMockHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
