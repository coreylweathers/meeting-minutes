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

public interface IBlobStorageService
{
    Task<string> UploadVideoAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<string> UploadTextAsync(string content, string blobName, CancellationToken ct = default);
    Task<string> UploadSummaryAsync(string content, string blobName, CancellationToken ct = default);
    Task<string?> DownloadTextAsync(string blobUri, CancellationToken ct = default);
    Task<string> GetSasUrlAsync(string blobUri, TimeSpan expiry, CancellationToken ct = default);
}
