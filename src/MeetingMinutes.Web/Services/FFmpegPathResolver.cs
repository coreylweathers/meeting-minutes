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

namespace MeetingMinutes.Web.Services;

/// <summary>
/// Resolves the directory containing the ffmpeg binary.
/// Priority: FFMPEG_BINARY_PATH env var → Windows winget install → empty (PATH fallback).
/// </summary>
internal static class FFmpegPathResolver
{
    /// <summary>
    /// Returns the folder to pass to GlobalFFOptions.BinaryFolder,
    /// or an empty string to let FFMpegCore search PATH (container/macOS behaviour).
    /// </summary>
    public static string Resolve()
    {
        // P1: explicit env var override — works everywhere
        var envPath = Environment.GetEnvironmentVariable("FFMPEG_BINARY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            return envPath;

        // P2: Windows winget install (Gyan.FFmpeg) — not added to PATH by winget
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");

            if (Directory.Exists(wingetPackages))
            {
                // Pattern: Gyan.FFmpeg_*/ffmpeg-*/bin
                var binDir = Directory
                    .GetDirectories(wingetPackages, "Gyan.FFmpeg*")
                    .SelectMany(d => Directory.GetDirectories(d, "ffmpeg-*"))
                    .Select(d => Path.Combine(d, "bin"))
                    .FirstOrDefault(Directory.Exists);

                if (binDir is not null)
                    return binDir;
            }
        }

        // P3: empty → FFMpegCore uses PATH (correct for Linux containers via apt-get, macOS via brew)
        return string.Empty;
    }
}
