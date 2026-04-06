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
        var uriBuilder = new BlobUriBuilder(new Uri(blobUri));
        if (string.IsNullOrEmpty(uriBuilder.BlobContainerName) || string.IsNullOrEmpty(uriBuilder.BlobName))
            return null;

        var blob = blobServiceClient
            .GetBlobContainerClient(uriBuilder.BlobContainerName)
            .GetBlobClient(uriBuilder.BlobName);

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
        var uriBuilder = new BlobUriBuilder(new Uri(blobUri));
        var blob = blobServiceClient
            .GetBlobContainerClient(uriBuilder.BlobContainerName)
            .GetBlobClient(uriBuilder.BlobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = uriBuilder.BlobContainerName,
            BlobName = uriBuilder.BlobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blob.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }
}
