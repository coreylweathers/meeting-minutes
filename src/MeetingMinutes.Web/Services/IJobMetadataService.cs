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

using MeetingMinutes.Shared.Entities;
using MeetingMinutes.Shared.Enums;

namespace MeetingMinutes.Web.Services;

public interface IJobMetadataService
{
    Task<ProcessingJob> CreateJobAsync(string fileName, CancellationToken ct = default);
    Task<ProcessingJob?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessingJob>> ListJobsAsync(CancellationToken ct = default);
    Task UpdateJobAsync(ProcessingJob job, CancellationToken ct = default);
    Task UpdateStatusAsync(string jobId, JobStatus status, string? errorMessage = null, CancellationToken ct = default);
}
