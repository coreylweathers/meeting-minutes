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
using MeetingMinutes.Web.Pages;
using MeetingMinutes.Web.Services;
using MeetingMinutes.Shared.DTOs;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.ListJobsAsync(default))
            .Returns(async () => { await Task.Delay(10000); return (IReadOnlyList<ProcessingJob>)new List<ProcessingJob>(); });
        Services.AddSingleton(mockJobMetadata.Object);
        
        // Act
        var cut = RenderComponent<Jobs>();
        
        // Assert - before initialization completes
        cut.Markup.Should().Contain("Loading");
    }
    
    [Fact]
    public async Task JobsPage_Shows_EmptyState_WhenNoJobs()
    {
        // Arrange
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.ListJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob>());
        Services.AddSingleton(mockJobMetadata.Object);
        
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
        var jobs = new List<ProcessingJob>
        {
            new ProcessingJob { JobId = "job1", FileName = "meeting1.mp4", Status = JobStatus.Completed.ToString(), PartitionKey = "jobs", RowKey = "job1", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1), UpdatedAt = DateTimeOffset.UtcNow },
            new ProcessingJob { JobId = "job2", FileName = "meeting2.mp4", Status = JobStatus.Pending.ToString(), PartitionKey = "jobs", RowKey = "job2", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30), UpdatedAt = DateTimeOffset.UtcNow }
        };
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.ListJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);
        Services.AddSingleton(mockJobMetadata.Object);
        
        // Act
        var cut = RenderComponent<Jobs>();
        await Task.Delay(100); // Wait for OnInitializedAsync
        cut.Render();
        
        // Assert
        cut.Markup.Should().Contain("meeting1.mp4");
        cut.Markup.Should().Contain("meeting2.mp4");
        cut.Markup.Should().Contain("Completed");
        cut.Markup.Should().Contain("Processing");
    }
    
    [Fact]
    public async Task JobsPage_Shows_ViewDetailsButtons()
    {
        // Arrange
        var jobs = new List<ProcessingJob>
        {
            new ProcessingJob { JobId = "job1", FileName = "meeting1.mp4", Status = JobStatus.Completed.ToString(), PartitionKey = "jobs", RowKey = "job1", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        var mockJobMetadata = new Mock<IJobMetadataService>();
        mockJobMetadata.Setup(s => s.ListJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);
        Services.AddSingleton(mockJobMetadata.Object);
        
        // Act
        var cut = RenderComponent<Jobs>();
        await Task.Delay(100); // Wait for OnInitializedAsync
        cut.Render();
        
        // Assert
        var detailsLink = cut.Find("a[href='/jobs/job1']");
        detailsLink.Should().NotBeNull();
        detailsLink.TextContent.Should().Contain("View Insights");
    }
}

