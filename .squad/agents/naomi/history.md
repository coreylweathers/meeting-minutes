## Project Context

**Project:** meeting-minutes  
**Requested by:** Corey Weathers  
**Stack:** .NET 9, ASP.NET Core Minimal API, Blazor WebAssembly, Azure Blob Storage (Aspire.Azure.Storage.Blobs), Azure Table Storage (Aspire.Azure.Data.Tables), Azure AI Speech (Microsoft.CognitiveServices.Speech), Azure OpenAI GPT-4o Mini (Azure.AI.OpenAI), FFMpegCore for audio extraction, .NET Aspire 9.1, Azure Container Apps deployment  
**Auth:** Microsoft + Google OAuth, BFF cookie pattern (API manages cookies, Blazor WASM never holds tokens)  
**Worker:** BackgroundService inside the API process (cost: one Container App)  
**Cost decisions:** Azure Table Storage (not Cosmos DB), GPT-4o Mini (not GPT-4o), scale-to-zero Container Apps  
**Solution projects:** MeetingMinutes.AppHost, MeetingMinutes.ServiceDefaults, MeetingMinutes.Api, MeetingMinutes.Web (Blazor WASM), MeetingMinutes.Shared  
**Review gate:** ALL code must be reviewed and approved by Miller before any task is marked done.

## Learnings

### 2026-03-31 — Shared Models (Task: naomi-shared-models)
- Created `Enums/JobStatus.cs`, `Entities/ProcessingJob.cs`, `DTOs/JobDto.cs`, `DTOs/CreateJobRequest.cs`, `DTOs/SummaryDto.cs`, `DTOs/UpdateSummaryRequest.cs` in `MeetingMinutes.Shared`.
- `ProcessingJob` implements `ITableEntity` from `Azure.Data.Tables`; `Status` stored as string for Table Storage readability.
- `JobDto` carries the `JobStatus` enum so the Blazor client gets a typed status value.
- Added `Azure.Data.Tables` version `12.*` package reference to the .csproj; build succeeded with 0 errors/warnings.

### 2026-03-31 — FFmpegHelper (Task: naomi-ffmpeg-helper)
- Created `Services/IFFmpegHelper.cs` and `Services/FFmpegHelper.cs` in `MeetingMinutes.Api`.
- `FFmpegHelper` uses primary constructor injection for `ILogger<FFmpegHelper>`.
- `AudioCodec.Pcm16BitLittleEndianSigned` does not exist in FFMpegCore 5.1.0's `AudioCodec` enum; the `Codec` class also has no public constructors. Use the string overload `WithAudioCodec("pcm_s16le")` instead.
- Registered as `AddSingleton<IFFmpegHelper, FFmpegHelper>()` in `Program.cs`.
- Build succeeded with 0 errors/warnings.

### 2026-03-31 — JobMetadataService (Task: naomi-table-service)
- Created `Services/IJobMetadataService.cs` and `Services/JobMetadataService.cs` in `MeetingMinutes.Api`.
- `JobMetadataService` accepts `TableServiceClient` via constructor injection (provided by `builder.AddAzureTableClient("tables")` in Aspire).
- Table name is `"jobs"`; `CreateIfNotExistsAsync` is called lazily on first use via a `_tableInitialized` bool guard.
- `CreateJobAsync` generates a `Guid.NewGuid().ToString()` as both `JobId` and `RowKey`, sets `Status = JobStatus.Pending.ToString()`, and upserts.
- `GetJobAsync` catches `RequestFailedException` with status 404 and returns null (not-found pattern).
- `ListJobsAsync` uses `QueryAsync<ProcessingJob>` with `PartitionKey eq 'jobs'` filter.
- `UpdateStatusAsync` delegates to `GetJobAsync` + `UpdateJobAsync` (which stamps `UpdatedAt`).
- Also fixed a pre-existing build error in `FFmpegHelper.cs`: `AudioCodec.pcm_s16le` does not exist in FFMpegCore 5.x; replaced with `.WithAudioCodec("pcm_s16le")` (string overload).
- Registered `IJobMetadataService` → `JobMetadataService` as singleton in `Program.cs`.
- Build: **0 errors, 0 warnings**.

### 2026-03-31 — SpeechTranscriptionService (Task: naomi-speech-service)
- Created `Services/ISpeechTranscriptionService.cs` (interface) and `Services/SpeechTranscriptionService.cs` (implementation) in `MeetingMinutes.Api`.
- Uses `Microsoft.CognitiveServices.Speech` 1.43.0: `SpeechConfig.FromSubscription`, `AudioConfig.FromWavFileInput`, `SpeechRecognizer` with continuous recognition and `TaskCompletionSource<string>` pattern.
- Throws `InvalidOperationException` when `AzureSpeech:Key` or `AzureSpeech:Region` config is missing.
- Respects `CancellationToken` via `ct.Register` callback that stops continuous recognition and cancels the TCS.
- Fixed a pre-existing compile error in `FFmpegHelper.cs`: `AudioCodec.Pcm16BitLittleEndianSigned` (invalid enum) → `"pcm_s16le"` string overload (FFMpegCore 5.x `Codec` is a class, not an enum; `WithAudioCodec(string)` overload used instead).
- Registered `ISpeechTranscriptionService` / `SpeechTranscriptionService` as singleton in `Program.cs`.
- Build: 0 errors, 0 warnings.

### 2026-03-31 — BlobStorageService (Task: naomi-blob-service)
- Created `Services/IBlobStorageService.cs` and `Services/BlobStorageService.cs` in `MeetingMinutes.Api`.
- `BlobStorageService` uses primary constructor injection for `BlobServiceClient` (provided by `builder.AddAzureBlobClient("blobs")` Aspire wiring).
- Container names: `"videos"` for `UploadVideoAsync`, `"transcripts"` for `UploadTextAsync`; both call `CreateIfNotExistsAsync(PublicAccessType.None)`.
- `DownloadTextAsync` parses container/blob from URI path (`AbsolutePath.Split('/', 2)`), downloads via `DownloadContentAsync`.
- `GetSasUrlAsync` uses `BlobSasBuilder` with `BlobSasPermissions.Read` and calls `blob.GenerateSasUri(sasBuilder)` — requires `BlobServiceClient` to be constructed with a `StorageSharedKeyCredential` or `BlobServiceClient` that supports SAS generation (Aspire wires this up).
- Fixed pre-existing compile error in `FFmpegHelper.cs`: `WithAudioCodec("pcm_s16le")` (string) is not a valid overload; replaced with `.WithCustomArgument("-acodec pcm_s16le")`.
- Registered `IBlobStorageService` → `BlobStorageService` as singleton in `Program.cs`.
- Build: **0 errors, 0 warnings**.


## 2025-03-31: Implemented SummarizationService (OpenAI Integration)

- Created ISummarizationService interface and SummarizationService implementation
- Integrated Azure OpenAI Client with GPT-4o Mini model
- Configured structured JSON prompting for meeting summaries
- Added Aspire.Azure.AI.OpenAI package and updated Azure.AI.OpenAI to 2.5.0-beta.1
- Registered services in Program.cs
- Build successful - service ready for use

### 2026-03-31 — JobWorker (Task: job-worker)
- Created `Workers/JobWorker.cs` as a `BackgroundService` that orchestrates the full meeting processing pipeline.
- Polls for pending jobs every 10 seconds using `Task.Delay(TimeSpan.FromSeconds(10))`.
- Uses `IServiceScopeFactory` to create scopes for scoped/singleton services per job processing cycle.
- Pipeline stages: ExtractingAudio → Transcribing → Summarizing → Completed.
- Downloads video blobs to temp files using `BlobServiceClient.GetBlobContainerClient().GetBlobClient().DownloadToAsync()`.
- Extracts audio via `IFFmpegHelper.ExtractAudioAsync`, transcribes via `ISpeechTranscriptionService.TranscribeAsync`, summarizes via `ISummarizationService.SummarizeAsync`.
- Uploads transcripts to "transcripts" container (`{jobId}.txt`) and summaries to "summaries" container (`{jobId}.json`) using dedicated helper methods.
- Serializes `SummaryDto` to JSON with `System.Text.Json.JsonSerializer.Serialize` (indented formatting).
- Error handling: catches all exceptions per-job, updates status to Failed with error message, logs with `_logger.LogError`.
- Temp file cleanup: always deletes video and audio temp files in finally block.
- Filtered pending jobs manually from `ListJobsAsync()` results since `GetJobsByStatusAsync` doesn't exist.
- Registered as hosted service in `Program.cs` with `builder.Services.AddHostedService<JobWorker>()`.
- Added `using MeetingMinutes.Api.Workers;` directive to `Program.cs`.
- Build: **0 errors, 0 warnings** (initial cache error resolved on clean rebuild).

