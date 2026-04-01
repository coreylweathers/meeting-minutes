You are helping me build a full-stack C# application using:

- ASP.NET Core Minimal API (backend)
- Blazor WebAssembly (frontend)
- Azure Blob Storage for all file storage
- Azure Table Storage or Cosmos DB Serverless for job metadata
- Azure Speech-to-Text for transcription
- Azure OpenAI (GPT-4o or GPT-4o Mini) for summarization
- A background processing service for long-running jobs
- FFmpeg for audio extraction

The app should support:
1. Uploading a video file from the Blazor WASM client.
2. Storing the uploaded file in Azure Blob Storage under an `uploads/` container.
3. Creating a “processing job” record in a metadata store with fields:
   - JobId (GUID)
   - FileName
   - BlobUrl
   - Status (Pending, ExtractingAudio, Transcribing, Summarizing, Completed, Failed)
   - TranscriptBlobUrl
   - SummaryBlobUrl
   - CreatedAt, UpdatedAt
4. A background worker that:
   - Watches for Pending jobs
   - Downloads the video from Blob Storage
   - Extracts audio using FFmpeg
   - Uploads audio to Blob Storage
   - Sends audio to Azure Speech-to-Text
   - Saves transcript to Blob Storage
   - Sends transcript to Azure OpenAI for summarization
   - Saves summary JSON to Blob Storage
   - Updates job status throughout each step
5. API endpoints:
   - POST /api/jobs → create a job from an uploaded file
   - GET /api/jobs → list all jobs (history)
   - GET /api/jobs/{id} → get job details + status
   - GET /api/jobs/{id}/transcript → download transcript
   - GET /api/jobs/{id}/summary → download summary
   - PUT /api/jobs/{id}/summary → update/edit summary
6. Blazor WASM UI pages:
   - Upload page: choose video, submit job
   - Jobs list page: show history with status badges
   - Job detail page: show transcript, summary, allow editing
   - Export options: download transcript or summary as files
7. Use async/await everywhere.
8. Use dependency injection for all services.
9. Use environment variables for all Azure keys and endpoints.
10. Keep everything minimal, clean, and idiomatic.

Generate:
- The full solution folder structure
- Backend Program.cs
- Background worker implementation
- Blob storage service
- Table storage or Cosmos DB metadata service
- Speech-to-Text service

- OpenAI summarization service
- FFmpeg helper
- Blazor WASM pages and components
- Shared DTOs
- Any necessary configuration files

Do not include placeholder lorem ipsum. Use realistic names and structure.
Start by generating the solution layout, then produce backend code, then frontend code.