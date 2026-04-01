using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Text;

namespace MeetingMinutes.Api.Services;

public sealed class BlobStorageService(BlobServiceClient blobServiceClient) : IBlobStorageService
{
    private const string VideosContainer = "videos";
    private const string TranscriptsContainer = "transcripts";

    public async Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(VideosContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = container.GetBlobClient(fileName);
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        return blob.Uri.ToString();
    }

    public async Task<string> UploadTextAsync(string content, string blobName, CancellationToken ct = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(TranscriptsContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        return blob.Uri.ToString();
    }

    public async Task<string?> DownloadTextAsync(string blobUri, CancellationToken ct = default)
    {
        var uri = new Uri(blobUri);
        // URI path is /<container>/<blobName...>
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
