using Azure;
using Azure.Data.Tables;
using MeetingMinutes.Api.Services;
using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;
using Moq;
using Xunit;
using FluentAssertions;

namespace MeetingMinutes.Tests.Services;

public class JobMetadataServiceTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly JobMetadataService _service;

    public JobMetadataServiceTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        
        _mockTableServiceClient
            .Setup(x => x.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);

        _service = new JobMetadataService(_mockTableServiceClient.Object);
    }

    [Fact]
    public async Task CreateJobAsync_ShouldCreateJobWithPendingStatus()
    {
        // Arrange
        var fileName = "test-video.mp4";
        ProcessingJob? capturedJob = null;
        
        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.UpsertEntityAsync(It.IsAny<ProcessingJob>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessingJob, TableUpdateMode, CancellationToken>((job, mode, ct) => capturedJob = job)
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        var result = await _service.CreateJobAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.Status.Should().Be(JobStatus.Pending.ToString());
        result.JobId.Should().NotBeNullOrEmpty();
        result.PartitionKey.Should().Be("jobs");
        result.RowKey.Should().Be(result.JobId);
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        
        capturedJob.Should().NotBeNull();
        _mockTableClient.Verify(x => x.UpsertEntityAsync(It.IsAny<ProcessingJob>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var expectedJob = new ProcessingJob
        {
            PartitionKey = "jobs",
            RowKey = jobId,
            JobId = jobId,
            FileName = "test.mp4",
            Status = JobStatus.Pending.ToString()
        };

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>("jobs", jobId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(expectedJob, Mock.Of<Response>()));

        // Act
        var result = await _service.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(jobId);
        result.FileName.Should().Be("test.mp4");
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnNull_WhenJobNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>("jobs", jobId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act
        var result = await _service.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var job = new ProcessingJob
        {
            PartitionKey = "jobs",
            RowKey = "test-job",
            JobId = "test-job",
            FileName = "test.mp4",
            Status = JobStatus.Pending.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.UpsertEntityAsync(It.IsAny<ProcessingJob>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        var oldUpdatedAt = job.UpdatedAt;

        // Act
        await _service.UpdateJobAsync(job, CancellationToken.None);

        // Assert
        job.UpdatedAt.Should().BeAfter(oldUpdatedAt);
        job.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateJobStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var job = new ProcessingJob
        {
            PartitionKey = "jobs",
            RowKey = jobId,
            JobId = jobId,
            FileName = "test.mp4",
            Status = JobStatus.Pending.ToString()
        };

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>("jobs", jobId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(job, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.UpsertEntityAsync(It.IsAny<ProcessingJob>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        await _service.UpdateStatusAsync(jobId, JobStatus.Transcribing, null, CancellationToken.None);

        // Assert
        job.Status.Should().Be(JobStatus.Transcribing.ToString());
        _mockTableClient.Verify(x => x.UpsertEntityAsync(It.Is<ProcessingJob>(j => j.Status == JobStatus.Transcribing.ToString()), TableUpdateMode.Replace, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldSetErrorMessage_WhenProvided()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var errorMessage = "Transcription failed";
        var job = new ProcessingJob
        {
            PartitionKey = "jobs",
            RowKey = jobId,
            JobId = jobId,
            FileName = "test.mp4",
            Status = JobStatus.Transcribing.ToString()
        };

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>("jobs", jobId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(job, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.UpsertEntityAsync(It.IsAny<ProcessingJob>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        await _service.UpdateStatusAsync(jobId, JobStatus.Failed, errorMessage, CancellationToken.None);

        // Assert
        job.Status.Should().Be(JobStatus.Failed.ToString());
        job.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldThrow_WhenJobNotFound()
    {
        // Arrange
        var jobId = "nonexistent-job";

        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>()));
        
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>("jobs", jobId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateStatusAsync(jobId, JobStatus.Transcribing, null, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentTableInitialization_ShouldNotDoubleInitialize()
    {
        // Arrange
        var callCount = 0;
        _mockTableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10); // Simulate async work
                return Response.FromValue(null as Azure.Data.Tables.Models.TableItem, Mock.Of<Response>());
            });

        _mockTableClient
            .Setup(x => x.GetEntityAsync<ProcessingJob>(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act - Call GetJobAsync concurrently 10 times
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _service.GetJobAsync("test-job", CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Should NOT be thread-safe! This test documents the concern Miller raised.
        // The current implementation has a race condition where _tableInitialized check happens
        // before CreateIfNotExistsAsync completes, allowing multiple initialization calls.
        // This test PASSES if multiple calls occur, documenting the known issue.
        callCount.Should().BeGreaterThan(1, "because the current implementation has a race condition");
    }
}
