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

using Bunit;
using FluentAssertions;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using MeetingMinutes.Web.Pages;
using MeetingMinutes.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class JobDetailPageTests : TestContext
{
    private void RegisterServices(Mock<IJobMetadataService> mockJobMetadata, Mock<IBlobStorageService>? mockBlob = null)
    {
        Services.AddLogging();
        Services.AddSingleton(mockJobMetadata.Object);
        Services.AddSingleton((mockBlob ?? new Mock<IBlobStorageService>()).Object);
        Services.AddSingleton(new Mock<ISummarizationService>().Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ProcessingJob MakeJob(string jobId, string fileName, JobStatus status, string? errorMessage = null) => new()
    {
        JobId = jobId,
        FileName = fileName,
        Status = status.ToString(),
        ErrorMessage = errorMessage,
        PartitionKey = "jobs",
        RowKey = jobId,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void JobDetailPage_DoesNotRequire_Authorization()
    {
        var authorizeAttribute = typeof(JobDetail)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .FirstOrDefault();
        authorizeAttribute.Should().BeNull("JobDetail page should NOT have [Authorize] attribute after auth removal");
    }

    [Fact]
    public void JobDetailPage_Shows_LoadingSpinner_Initially()
    {
        var tcs = new TaskCompletionSource<ProcessingJob?>();
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        RegisterServices(mockJobMetadata);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "job1"));

        cut.Markup.Should().Contain("Loading");
    }

    [Fact]
    public async Task JobDetailPage_Shows_JobNotFound_WhenJobDoesNotExist()
    {
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJob?)null);
        RegisterServices(mockJobMetadata);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "nonexistent"));
        await Task.Delay(100);
        cut.Render();

        cut.Markup.Should().Contain("Job Not Found");
    }

    [Fact]
    public async Task JobDetailPage_Displays_JobFileName()
    {
        var entity = MakeJob("job1", "test-meeting.mp4", JobStatus.Completed);
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        RegisterServices(mockJobMetadata);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "job1"));
        await Task.Delay(100);
        cut.Render();

        cut.Markup.Should().Contain("test-meeting.mp4");
    }

    [Fact]
    public async Task JobDetailPage_Shows_ProcessingSpinner_ForPendingJob()
    {
        var entity = MakeJob("job1", "processing.mp4", JobStatus.Transcribing);
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        RegisterServices(mockJobMetadata);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "job1"));
        await Task.Delay(100);
        cut.Render();

        cut.Markup.Should().Contain("Processing");
    }

    [Fact]
    public async Task JobDetailPage_Shows_ErrorMessage_ForFailedJob()
    {
        var entity = MakeJob("job1", "failed.mp4", JobStatus.Failed, "Transcription failed");
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        RegisterServices(mockJobMetadata);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "job1"));
        await Task.Delay(100);
        cut.Render();

        cut.Markup.Should().Contain("Error");
        cut.Markup.Should().Contain("Transcription failed");
    }

    [Fact]
    public async Task JobDetailPage_Shows_TranscriptAndSummary_ForCompletedJob()
    {
        var transcriptUri = "http://fake/transcripts/job1.txt";
        var summaryUri = "http://fake/summaries/job1.json";
        var entity = MakeJob("job1", "completed.mp4", JobStatus.Completed);
        entity.TranscriptBlobUri = transcriptUri;
        entity.SummaryBlobUri = summaryUri;

        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var mockBlob = new Mock<IBlobStorageService>();
        mockBlob.Setup(b => b.DownloadTextAsync(transcriptUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is the meeting transcript.");
        var summary = new SummaryDto("Test Meeting", new[] { "Alice", "Bob" },
            new[] { "Point 1" }, new[] { "Action 1" }, new[] { "Decision 1" }, 30);
        mockBlob.Setup(b => b.DownloadTextAsync(summaryUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(summary));

        RegisterServices(mockJobMetadata, mockBlob);

        var cut = RenderComponent<JobDetail>(p => p.Add(x => x.Id, "job1"));
        await Task.Delay(200);
        cut.Render();

        cut.Markup.Should().Contain("Full Transcript");
        cut.Markup.Should().Contain("Executive Intelligence");
        cut.Markup.Should().Contain("Test Meeting");
    }
}
