using FFMpegCore;
using FFMpegCore.Enums;

namespace MeetingMinutes.Api.Services;

public sealed class FFmpegHelper(ILogger<FFmpegHelper> logger) : IFFmpegHelper
{
    public async Task<string> ExtractAudioAsync(string videoPath, CancellationToken ct = default)
    {
        var outputPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");

        logger.LogInformation("Extracting audio from {VideoPath} to {OutputPath}", videoPath, outputPath);

        try
        {
            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(outputPath, true, options => options
                    .WithCustomArgument("-acodec pcm_s16le")
                    .WithAudioSamplingRate(16000)
                    .DisableChannel(Channel.Video))
                .CancellableThrough(ct)
                .ProcessAsynchronously();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FFmpeg audio extraction failed for {VideoPath}", videoPath);
            throw new InvalidOperationException("Audio extraction failed", ex);
        }

        logger.LogInformation("Audio extraction complete: {OutputPath}", outputPath);

        return outputPath;
    }
}
