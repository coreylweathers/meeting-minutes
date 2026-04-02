using Bunit;
using FluentAssertions;
using MeetingMinutes.Web.Pages;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class JobsPageTests : TestContext
{
    [Fact]
    public void JobsPage_DoesNotRequire_Authorization()
    {
        // Arrange - auth removed from project
        var authorizeAttribute = typeof(Jobs)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .FirstOrDefault();
        
        // Assert
        authorizeAttribute.Should().BeNull("Jobs page should NOT have [Authorize] attribute after auth removal");
    }
    
    [Fact]
    public async Task JobsPage_Shows_LoadingSpinner_Initially()
    {
        // Arrange
        var mockHandler = new DelayedMockHttpMessageHandler();
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Jobs>();
        
        // Assert - before initialization completes
        cut.Markup.Should().Contain("Loading jobs");
    }
    
    [Fact]
    public async Task JobsPage_Shows_EmptyState_WhenNoJobs()
    {
        // Arrange
        var emptyList = new List<JobDto>();
        var mockHandler = new MockJobsHttpMessageHandler(emptyList);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Jobs>();
        await Task.Delay(100); // Wait for OnInitializedAsync
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("No meetings yet");
        cut.Markup.Should().Contain("Upload Meeting");
    }
    
    [Fact]
    public async Task JobsPage_Displays_JobList_WhenJobsExist()
    {
        // Arrange
        var jobs = new List<JobDto>
        {
            new JobDto(
                JobId: "job1",
                FileName: "meeting1.mp4",
                Status: JobStatus.Completed,
                BlobUri: null,
                TranscriptBlobUri: null,
                SummaryBlobUri: null,
                ErrorMessage: null,
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAt: DateTimeOffset.UtcNow
            ),
            new JobDto(
                JobId: "job2",
                FileName: "meeting2.mp4",
                Status: JobStatus.Pending,
                BlobUri: null,
                TranscriptBlobUri: null,
                SummaryBlobUri: null,
                ErrorMessage: null,
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-30),
                UpdatedAt: DateTimeOffset.UtcNow
            )
        };
        var mockHandler = new MockJobsHttpMessageHandler(jobs);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Jobs>();
        await Task.Delay(100); // Wait for OnInitializedAsync
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("meeting1.mp4");
        cut.Markup.Should().Contain("meeting2.mp4");
        cut.Markup.Should().Contain("Completed");
        cut.Markup.Should().Contain("Pending");
    }
    
    [Fact]
    public async Task JobsPage_Shows_ViewDetailsButtons()
    {
        // Arrange
        var jobs = new List<JobDto>
        {
            new JobDto(
                JobId: "job1",
                FileName: "meeting1.mp4",
                Status: JobStatus.Completed,
                BlobUri: null,
                TranscriptBlobUri: null,
                SummaryBlobUri: null,
                ErrorMessage: null,
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow
            )
        };
        var mockHandler = new MockJobsHttpMessageHandler(jobs);
        var mockHttpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Jobs>();
        await Task.Delay(100); // Wait for OnInitializedAsync
        cut.Render();
        
        // Assert
        var detailsLink = cut.Find("a[href='/jobs/job1']");
        detailsLink.Should().NotBeNull();
        detailsLink.TextContent.Should().Contain("View Details");
    }
}

internal class MockJobsHttpMessageHandler : HttpMessageHandler
{
    private readonly List<JobDto> _jobs;

    public MockJobsHttpMessageHandler(List<JobDto> jobs)
    {
        _jobs = jobs;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/jobs")
        {
            var json = JsonSerializer.Serialize(_jobs);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
        
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal class DelayedMockHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(10000, cancellationToken); // Long delay to test loading state
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };
    }
}
