namespace MeetingMinutes.Web.Services;

public interface IFFmpegHelper
{
    Task<string> ExtractAudioAsync(string videoPath, CancellationToken ct = default);
}
