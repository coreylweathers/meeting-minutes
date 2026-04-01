using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Text;

namespace MeetingMinutes.Web.Services;

public sealed class BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger) : IBlobStorageService
{
    private const string VideosContainer = "videos";
    private const string TranscriptsContainer = "transcripts";

    public async Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        logger.LogInformation("Uploading video blob {BlobName} to container {Container}", fileName, VideosContainer);
        var container = blobServiceClient.GetBlobContainerClient(VideosContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = container.GetBlobClient(fileName);
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        logger.LogInformation("Video blob {BlobName} uploaded to {Container}", fileName, VideosContainer);
        return blob.Uri.ToString();
    }

    public async Task<string> UploadTextAsync(string content, string blobName, CancellationToken ct = default)
    {
        logger.LogInformation("Uploading text blob {BlobName} to container {Container}", blobName, TranscriptsContainer);
        var container = blobServiceClient.GetBlobContainerClient(TranscriptsContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        logger.LogInformation("Text blob {BlobName} uploaded ({Size} bytes)", blobName, content.Length);
        return blob.Uri.ToString();
    }

    public async Task<string?> DownloadTextAsync(string blobUri, CancellationToken ct = default)
    {
        logger.LogInformation("Downloading text blob from {BlobUri}", blobUri);
        var uri = new Uri(blobUri);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        if (segments.Length < 2)
            return null;

        var containerName = segments[0];
        var blobName = segments[1];

        var blob = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);

        try
        {
            var response = await blob.DownloadContentAsync(ct);
            return response.Value.Content.ToString();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogError(ex, "Blob not found at {BlobUri}", blobUri);
            return null;
        }
    }

    public Task<string> GetSasUrlAsync(string blobUri, TimeSpan expiry, CancellationToken ct = default)
    {
        var uri = new Uri(blobUri);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);

        var containerName = segments[0];
        var blobName = segments.Length > 1 ? segments[1] : string.Empty;

        var blob = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blob.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }
}
