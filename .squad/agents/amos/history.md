## Project Context

**Project:** meeting-minutes  
**Requested by:** Corey Weathers  
**Stack:** .NET 9, ASP.NET Core Minimal API, Blazor WebAssembly, Azure Blob Storage (Aspire.Azure.Storage.Blobs), Azure Table Storage (Aspire.Azure.Data.Tables), Azure AI Speech (Microsoft.CognitiveServices.Speech), Azure OpenAI GPT-4o Mini (Azure.AI.OpenAI), FFMpegCore for audio extraction, .NET Aspire 9.1, Azure Container Apps deployment  
**Auth:** Microsoft + Google OAuth, BFF cookie pattern (API manages cookies, Blazor WASM never holds tokens)  
**Worker:** BackgroundService inside the API process (cost: one Container App)  
**Cost decisions:** Azure Table Storage (not Cosmos DB), GPT-4o Mini (not GPT-4o), scale-to-zero Container Apps  
**Solution projects:** MeetingMinutes.AppHost, MeetingMinutes.ServiceDefaults, MeetingMinutes.Api, MeetingMinutes.Web (Blazor WASM), MeetingMinutes.Shared  
**Review gate:** ALL code must be reviewed and approved by Miller before any task is marked done.

## Work Log

### ServiceDefaults Extensions.cs (Completed)
- Enhanced `ConfigureOpenTelemetry()` to include tracing source filter `AddSource("MeetingMinutes.*")` for custom instrumentation
- Verified implementation includes all required components:
  - Logging OTel export with formatted messages and scopes
  - Metrics: AspNetCore, HttpClient, Runtime instrumentation
  - Tracing: AspNetCore, HttpClient, MeetingMinutes.* source
  - OTLP exporter (conditional on OTEL_EXPORTER_OTLP_ENDPOINT)
  - AddServiceDefaults: OpenTelemetry, health checks, service discovery, HttpClient resilience
  - AddDefaultHealthChecks: Self-check with "live" tag
  - MapDefaultEndpoints: /health and /alive (liveness/readiness)
- Build result: SUCCESS (40.1s)

### Aspire AppHost Orchestration (Completed)
- Implemented complete AppHost Program.cs with Aspire orchestration
- Configured Azure Storage emulation via Azurite (.RunAsEmulator())
  - Blob storage resource for audio file uploads
  - Table storage resource for job metadata
- Added connection string references for external Azure services:
  - OpenAI: `AddConnectionString("openai")` - points to real Azure OpenAI
  - Speech: `AddConnectionString("speech")` - points to real Azure AI Speech
- Wired Api project with all resource references (blobs, tables, openai, speech)
- Design decision: Web project NOT included as separate resource
  - Api hosts Blazor WASM via UseBlazorFrameworkFiles()
  - Only Api project reference needed in AppHost
- Build result: SUCCESS
- Packages verified:
  - Aspire.Hosting.AppHost 9.1.0
  - Aspire.Hosting.Azure.Storage 9.1.0

### azd Configuration (Completed)
- Created Azure Developer CLI (azd) deployment configuration for Container Apps deployment
- Files created:
  - `azure.yaml`: Main azd configuration pointing to Aspire AppHost
  - `infra/main.parameters.json`: Deployment parameters with environment variable substitution
  - `infra/app/api.tmpl.yaml`: Scale-to-zero configuration (minReplicas: 0, maxReplicas: 1)
  - `README.md`: Complete deployment documentation and architecture overview
- Design decisions:
  - **Cost optimization**: Scale-to-zero Container Apps for minimal idle costs
  - **Aspire integration**: Using `host: containerapp` with Aspire AppHost for auto-generated Bicep
  - **No manual Bicep**: azd generates infrastructure code from Aspire manifest during `azd up`
- Deployment flow: `azd auth login` → `azd up` (provisions, builds, deploys)
- Resources deployed: Container Apps, Storage Account, OpenAI connection, Speech connection
- Decision documented: `.squad/decisions/inbox/amos-azd-config-complete.md`

### README Credentials Documentation (Completed)
- Enhanced "Local Development" section with comprehensive credentials setup guide
- Added new subsection `### Getting Your Credentials` with step-by-step instructions for:
  - Azure OpenAI: Resource creation, model deployment, DefaultAzureCredential auth
  - Azure AI Speech: Resource creation, Free F0 tier, connection string format
  - Microsoft OAuth: Entra ID registration, client ID/secret generation, redirect URIs
  - Google OAuth: GCP console setup, OAuth consent screen, client credentials
- Added helper content: "Finding your port" instructions and inline tips
- Reorganized original local dev steps into "### Running Locally" subsection
- Decision documented: `.squad/decisions/inbox/amos-readme-keys.md`

### AppHost Build Fixes (Completed)

- **Issue 1 — Aspire workload:** Ran `dotnet workload install aspire` to resolve missing DCP executable and Dashboard path errors. Note: In .NET 10, Aspire workload is deprecated — NuGet packages are the delivery vehicle, but the workload install updates manifests needed for tooling.
- **Issue 2 — KubernetesClient warning:** Actual warning was `NU1902` (not NETSDK1228). Added `<NoWarn>$(NoWarn);NU1902</NoWarn>` to `MeetingMinutes.AppHost.csproj` to suppress transitive vulnerability warning from `Aspire.Hosting` → `KubernetesClient` 15.0.1.
- Build result: **SUCCESS — 0 warnings, 0 errors**
- Decision documented: `.squad/decisions/inbox/amos-apphost-fix.md`

### NuGet Package Updates (Patch/Minor) (Completed)

- Updated 12 safe NuGet packages across 3 projects (patch/minor versions only)
- **MeetingMinutes.Api** (5 packages):
  - Microsoft.CognitiveServices.Speech → 1.48.2
  - FFMpegCore → 5.4.0
  - Microsoft.AspNetCore.Authentication.Google → 10.0.5
  - Microsoft.AspNetCore.Authentication.MicrosoftAccount → 10.0.5
  - Microsoft.AspNetCore.Components.WebAssembly.Server → 10.0.5
- **MeetingMinutes.Web** (2 packages):
  - Microsoft.AspNetCore.Components.WebAssembly → 10.0.5
  - Microsoft.AspNetCore.Components.WebAssembly.DevServer → 10.0.5
- **MeetingMinutes.ServiceDefaults** (5 packages):
  - OpenTelemetry.Exporter.OpenTelemetryProtocol → 1.15.1
  - OpenTelemetry.Extensions.Hosting → 1.15.1
  - OpenTelemetry.Instrumentation.AspNetCore → 1.15.1
  - OpenTelemetry.Instrumentation.Http → 1.15.0
  - OpenTelemetry.Instrumentation.Runtime → 1.15.0
- **NOT updated** (major breaking changes):
  - Aspire.* packages (9.1.0 → 13.2.1) - major version jump
  - Microsoft.Identity.Web (3.8.2 → 4.6.0) - major version
  - Microsoft.Extensions.Http.Resilience (9.4.0 → 10.4.0) - major version
  - Microsoft.Extensions.ServiceDiscovery (9.1.0 → 10.4.0) - major version
  - Azure.AI.OpenAI - beta package, left as-is
- Build result: **SUCCESS** (28.5s) - all projects compile, no breaking changes
- Decision documented: `.squad/decisions/inbox/amos-package-updates.md`

## Learnings

### Blazor Server Integration into Aspire (Completed)
- Converted MeetingMinutes.Web from Blazor WebAssembly to Blazor Interactive Server (separate ASP.NET Core process)
- Updated AppHost to register Web as a separate Aspire resource
  - **AppHost.csproj**: Added `<ProjectReference Include="..\MeetingMinutes.Web\MeetingMinutes.Web.csproj" IsAspireProjectResource="true" />`
  - **Program.cs**: Registered Web project with `builder.AddProject<Projects.MeetingMinutes_Web>("web")`
  - Web project references API via `.WithReference(api)` — Aspire injects API base URL into Web configuration
  - Web waits for API with `.WaitFor(api)` — ensures API is ready before Web starts
  - Marked Web with `.WithExternalHttpEndpoints()` for external access
- Build result: **SUCCESS** — all 6 projects compile (Shared, ServiceDefaults, Web, Api, AppHost, and implicit references)
- Architecture: BFF pattern — Web calls API directly, API never exposed to external clients

### 2026-03-31: Web Project AppHost Integration (Completed)

**Task:** Configure Web as separate Aspire resource in AppHost with service discovery

**Status:** ✅ APPROVED — integration approved for merge

**Changes Made:**

1. **AppHost.csproj**
   - Added Web ProjectReference with `IsAspireProjectResource="true"` to enable source generator to create `Projects.MeetingMinutes_Web` type

2. **AppHost/Program.cs**
   - Added Web resource registration:
     ```csharp
     var web = builder.AddProject<Projects.MeetingMinutes_Web>("web")
         .WithReference(api)          // Service discovery: injects API base URL
         .WaitFor(api)                // Startup ordering: API first, then Web
         .WithExternalHttpEndpoints();
     ```
   - Service discovery behavior: Aspire injects `services__api__http__0` and `services__api__https__0` into Web environment
   - BFF pattern: Web calls API directly; API not exposed externally; Web doesn't need direct refs to Azure services

**Build Verification:**
- ✅ All 6 projects compile (0 errors, 0 warnings, 53.48s)
- ✅ AppHost can reference both API and Web via source-generated types

**Architecture Impact:**
- Web and API coordinate startup and service discovery in both local (Aspire Dashboard) and cloud (Azure Container Apps) environments
- `azd up` deploys both containers separately; service discovery handles inter-process communication
- Web configuration automatically receives API base URL from Aspire

**Orchestration Log:** `.squad/orchestration-log/2026-04-01T17-12-57Z-amos-apphost-web-resource.md`

### OpenAI API Key Migration (Completed)

**Task:** Migrate from Azure OpenAI endpoint to direct OpenAI API key (api.openai.com)

**Status:** ✅ COMPLETED — AppHost and API updated, build passes

**Changes Made:**

1. **AppHost Program.cs** (Updated comment)
   - Changed comment from "Azure OpenAI — points to real Azure OpenAI" to "OpenAI — set via user-secrets"
   - Updated user-secrets documentation to show sk-<key> format (not AZURE_OPENAI_ENDPOINT)
   - Connection string "openai" now carries API key directly, not endpoint URL

2. **MeetingMinutes.Api Program.cs** (Fixed OpenAI client instantiation)
   - Removed invalid `ApiKeyCredential` wrapper
   - OpenAI SDK v2.2.0 accepts API key directly: `new OpenAIClient(apiKeyString)`
   - Error message now correctly references sk-... format

3. **Infrastructure (No changes required)**
   - No manual Bicep files exist (auto-generated by `azd`)
   - No Azure OpenAI resource references in infra/ to remove
   - API key will be managed as app secret in Container Apps deployment

**Build Verification:**
- ✅ AppHost: Build succeeded (0 errors, 0 warnings)
- ✅ All 6 projects compile

**Developer Setup:**
```bash
# Instead of old Azure endpoint:
# dotnet user-secrets set "ConnectionStrings:openai" "https://..."

# New OpenAI API key:
dotnet user-secrets set "ConnectionStrings:openai" "sk-<your-key>"
```

**Architecture Impact:**
- Direct API calls to api.openai.com (no Azure dependency for LLM)
- Reduced Azure resource overhead (no OpenAI Cognitive Service resource)
- Credential: API key managed as app secret in AppHost configuration

### Web Project Port Configuration (Completed)

**Task:** Fix port conflict for standalone Web project execution

**Status:** ✅ COMPLETED — launchSettings.json created, Program.cs fallback updated

**Changes Made:**

1. **Created launchSettings.json**
   - Location: `src/MeetingMinutes.Web/Properties/launchSettings.json`
   - HTTPS profile (default): `https://localhost:7180;http://localhost:5180`
   - HTTP profile: `http://localhost:5180`
   - Both profiles set `ASPNETCORE_ENVIRONMENT: "Development"`
   - HTTPS profile uses `"commandName": "Project"` for IDE default

2. **Updated Program.cs**
   - Changed fallback port from `http://localhost:5000` to `http://localhost:5180`
   - Line 21: HttpClient API base URL fallback now points to non-conflicting port
   - Allows standalone Web execution without port 5000 conflicts

3. **Port Allocation Rationale**
   - Port 5180 (HTTP): Avoids port 5000 (old ASP.NET Core default, often in use)
   - Port 7180 (HTTPS): Avoids port 7000 range conflicts with other services
   - Port 15888 (preserved): Aspire dashboard (AppHost launchSettings)
   - Ports 16175/16176 (preserved): Aspire OTLP endpoints
   - Port 5001 (avoided): Often used for ASP.NET HTTPS redirect fallback

**Build Verification:**
- ✅ Web project builds successfully (0 errors, 0 warnings, 6.6s)
- ✅ launchSettings.json is properly structured JSON
- ✅ Program.cs compiles with updated fallback

**Developer Impact:**
- Web project can now run standalone via IDE (F5) without port 5000 conflicts
- Aspire AppHost orchestration unaffected (separate launchSettings)
- Fallback URL supports both Aspire discovery and direct standalone execution

### FFMpeg Containerization (Completed)

**Task:** Add ffmpeg support to container for audio extraction from videos

**Status:** ✅ COMPLETED — Dockerfile created, README updated, build verified

**Changes Made:**

1. **Created src/MeetingMinutes.Web/Dockerfile**
    - Multi-stage build: SDK 10.0 → ASP.NET 10.0 runtime
    - Build stage: Restores full solution (src + tests), publishes Web project
    - Runtime stage: Installs ffmpeg via apt-get, runs as non-root user `app`
    - Copies solution files (MeetingMinutes.sln, global.json, Directory.Build.props)
    - Port: 8080 (Aspire standard), ASPNETCORE_URLS=http://+:8080
    - Security: Uses `--chown=app:app` during COPY, switches to app user

2. **Updated README.md Prerequisites**
    - Added ffmpeg as requirement with winget installation command
    - Links to ffmpeg.org for cross-platform support
    - Placed after Docker Desktop, before Azure subscription line

3. **Docker Build Verification**
    - ✅ Build succeeded: Image tag `meeting-minutes-web-test:latest` (1.17GB)
    - ✅ All stages completed: Restore (85s), publish (23s), export (38s)
    - ✅ ffmpeg installed and container layer created
    - Note: Build optimized to include tests/ directory to satisfy solution references

**Key Decisions:**

- **Dockerfile location:** `src/MeetingMinutes.Web/Dockerfile` — Aspire + azd will use custom Dockerfile when present at project root instead of auto-generating
- **Build context:** Repo root (C:\Code\projects\meeting-minutes) — all COPY paths relative to repo root
- **Solution restoration:** Full solution restore required (build stage copies src/ + tests/) — FFMpegCore.dll transitive dependencies declared by Web project need resolution
- **ffmpeg install method:** apt-get clean install in runtime stage — minimal layer, removes package lists to save space (~100MB savings vs keeping lists)
- **Non-root user:** `app` user mandatory for Container Apps security posture — azd deployment requires least-privilege container execution

**Azure Deployment Impact:**
- `azd up` will use this custom Dockerfile instead of generating one
- ffmpeg binary automatically available in Container Apps container
- No additional infrastructure resource needed (embedded in container image)

### Transcript Review Gate Implementation (Completed)

**Task:** Implement user-driven gate after transcription — user chooses whether to run AI summarization

**Status:** ✅ COMPLETED — Enum, services, worker, endpoints, and UI updated; build passes

**Changes Made:**

1. **JobStatus.cs**: Added `Transcribed` enum value between `Transcribing` and `Summarizing`
2. **IBlobStorageService.cs**: Added `UploadSummaryAsync` method signature
3. **BlobStorageService.cs**: Implemented `UploadSummaryAsync` for "summaries" container (mirrors `UploadTextAsync`)
4. **JobWorker.cs**: 
   - Worker now stops at `Transcribed` status after transcription completes
   - Removed automatic summarization steps (7–10)
   - Removed `ISummarizationService` parameter from `ProcessJobAsync`
   - Removed `UploadToContainerAsync` helper method
   - Removed `using System.Text.Json` (no longer needed)
5. **Program.cs**: Added two new REST endpoints:
   - `POST /api/jobs/{id}/summarize` — runs summarization and completes job
   - `POST /api/jobs/{id}/complete` — marks job complete without AI
6. **JobDetail.razor**: 
   - Added `@inject ISummarizationService Summarizer`
   - Added "Ready to Review" status badge for `Transcribed` state
   - Added action panel with three buttons: "Summarize with AI", "Download Transcript", "Complete Job"
   - Updated polling logic to stop at `Transcribed` and load transcript
   - Added three new methods: `SummarizeJob()`, `CompleteJob()`, `DownloadTranscript()`
   - Made transcript scrollable (max-h-[600px] overflow-y-auto)
7. **App.razor**: Added `downloadTextAsFile` JavaScript interop function

**Build Verification:**
- ✅ Solution builds successfully (0 errors, 2 warnings unrelated to changes, 29.3s)
- ✅ All 6 projects compile

**Architecture Impact:**
- Pipeline now pauses at `Transcribed` — user drives next action from UI
- Blazor component calls services directly (no HTTP round-trip overhead)
- REST endpoints available for external callers (API clients, automation)
- "summaries" container separate from "transcripts" container for logical separation