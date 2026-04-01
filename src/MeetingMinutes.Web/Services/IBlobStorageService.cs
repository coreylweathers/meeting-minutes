namespace MeetingMinutes.Web.Services;

public interface IBlobStorageService
{
    Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<string> UploadTextAsync(string content, string blobName, CancellationToken ct = default);
    Task<string?> DownloadTextAsync(string blobUri, CancellationToken ct = default);
    Task<string> GetSasUrlAsync(string blobUri, TimeSpan expiry, CancellationToken ct = default);
}
