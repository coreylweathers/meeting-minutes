# Squad Decisions

## 2026-03-31: Interactive Server Migration Complete

### Decision 1: Architecture — Blazor WASM → Interactive Server

**Author:** Holden (Lead Architect)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Migrated MeetingMinutes.Web from Blazor WebAssembly to Blazor Interactive Server render mode.

**Rationale:**
- Simpler server-side authentication via HttpContext.User
- Eliminates .NET WASM runtime download (faster page load)
- Server-to-server API calls with Aspire service discovery (no CORS complexity)
- Appropriate for internal tool with low concurrent users

**Scope:**
1. Web project: SDK change (BlazorWebAssembly → Web), auth relocation, HttpClient server-to-server wiring
2. API project: Remove WASM hosting code and auth (now in Web)
3. AppHost: Web registered as separate Aspire resource (already configured)

**Key Components:**
- Web: AddRazorComponents().AddInteractiveServerComponents() + MapRazorComponents<App>().AddInteractiveServerRenderMode()
- Auth: Moved OAuth setup from API → Web; ServerAuthenticationStateProvider reads from HttpContext.User
- Aspire: .WithReference(api) injects API service discovery; .WaitFor(api) orders startup

---

### Decision 2: Implementation — Web Project Interactive Server

**Author:** Alex (Frontend Developer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Implemented Blazor Interactive Server migration with complete Program.cs rewrite, component setup, and API cleanup.

**Implementation Details:**
- **Web.csproj:** SDK Microsoft.NET.Sdk.BlazorWebAssembly → Microsoft.NET.Sdk.Web
- **Program.cs:** Added AddRazorComponents(), AddInteractiveServerComponents(), MapRazorComponents<App>().AddInteractiveServerRenderMode()
- **App.razor:** Root HTML document with @rendermode="InteractiveServer" on HeadOutlet and Routes
- **ServerAuthenticationStateProvider:** New scoped service reading from HttpContext.User with null-safe chaining
- **HttpClient:** Configured for server-to-server calls with Aspire service discovery
- **API cleanup:** Removed UseBlazorFrameworkFiles(), MapFallbackToFile(), WASM server package
- **Build:** ✅ 0 errors, 0 warnings

**Files Modified:** 9 (Program.cs, App.razor, _Imports.razor, csproj, ServerAuthenticationStateProvider, Pages)  
**Files Deleted:** 2 (CookieAuthenticationStateProvider.cs, wwwroot/index.html)

---

## 2026-04-01: Backend Services Implementation

### Decision 3: Database Strategy — Azure Table Storage with ITableEntity

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Chose Azure Table Storage for job metadata (ProcessingJob entity).

**Rationale:**
- Lowest cost per operation (vs Cosmos DB)
- Sufficient for async job tracking (no complex queries needed)
- ITableEntity interface ensures built-in Timestamp, PartitionKey, RowKey columns

**Schema:** ProcessingJob implements ITableEntity, Status stored as string (for readability in Azure Portal)

---

### Decision 4: FFMpegCore Codec Selection — String overload for pcm_s16le

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** FFMpegCore v5.1.0's AudioCodec enum does not include Pcm16BitLittleEndianSigned; use .WithAudioCodec("pcm_s16le") string overload instead.

**Rationale:**
- FFMpegCore codec enums are limited
- String overload passes directly to ffmpeg CLI (supports any codec ffmpeg itself supports)
- WAV output requires PCM 16-bit signed LE for Azure Speech-to-Text

---

### Decision 5: Shared Models Library

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Created MeetingMinutes.Shared project containing DTOs and entities shared between API and Web projects.

**Rationale:**
- Single source of truth for data contracts
- Blazor WASM can deserialize JobDto directly from API responses
- Reduces duplication and serialization bugs

**Files:**
- Enums/JobStatus.cs (Pending, Extracting, Transcribing, Summarizing, Completed, Failed)
- Entities/ProcessingJob.cs (ITableEntity + status tracking)
- DTOs/JobDto.cs, CreateJobRequest.cs, UpdateSummaryRequest.cs, SummaryDto.cs

---

### Decision 6: OpenAI SDK v2.2.0 (not Azure)

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-01

**Summary:** Swapped Azure.AI.OpenAI → OpenAI v2.2.0 (official SDK) for cost and simplicity.

**Rationale:**
- Official OpenAI SDK cheaper than Azure OpenAI at scale
- No Azure OpenAI provisioning overhead
- API key sourced from user-secrets / environment (not Aspire ResourceBuilder)

**Files:** SummarizationService.cs (AzureOpenAIClient → OpenAIClient), Program.cs (ApiKeyCredential wiring)

---

## 2026-04-02: Authentication Removal & Antiforgery Fix

### Decision 7: Eliminate OAuth / Local Auth

**Author:** Corey Weathers (Product Lead)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-02

**Summary:** Removed all OAuth (Microsoft + Google) and local session auth. App is now public, no user tracking.

**Rationale:**
- Simpler initial deployment (no auth infrastructure)
- Meeting transcript privacy handled via link-sharing (not access control)
- Can re-add auth later if needed

**Scope:** All auth middleware, endpoints, claims extraction removed. /api/jobs endpoints now public.

---

### Decision 8: Antiforgery Middleware Always Required

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-02

**Summary:** Blazor Server (even without authentication) requires antiforgery middleware.

**Rationale:**
- Blazor adds antiforgery metadata to interactive endpoints by default
- Request-processing pipeline checks for antiforgery middleware
- Missing middleware throws InvalidOperationException at runtime

**Fix:** Added AddAntiforgery() and UseAntiforgery() back to Program.cs.

---

## 2026-04-07: FFmpeg Local Resolution

### Decision 9: FFmpegPathResolver — 3-Tier Resolution for winget / apt / brew

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-07

**Summary:** Created FFmpegPathResolver.cs to auto-discover ffmpeg binary at startup.

**Problem:** winget install --id Gyan.FFmpeg does NOT add binary to PATH. FFMpegCore v5.4.0 only searches PATH by default, causing Win32Exception (2): An error occurred trying to start process 'ffmpeg.exe'.

**Solution:** Static helper with 3-tier resolution:
1. FFMPEG_BINARY_PATH env var (escape hatch)
2. Windows winget glob: %LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_*\ffmpeg-*\bin
3. Empty string → FFMpegCore uses PATH (Linux apt, macOS brew fallback)

**Implementation:** GlobalFFOptions.Configure() called at startup in Program.cs before AddServiceDefaults().

---

### Decision 10: BlobUriBuilder for URI Parsing

**Author:** Naomi (Backend Engineer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-07

**Summary:** Replaced manual blob URI path splitting with Azure.Storage.Blobs.BlobUriBuilder in PUT /api/jobs/{id}/summary endpoint.

**Rationale:**
- BlobUriBuilder correctly parses both Azurite (http://127.0.0.1:10000/devstoreaccount1/container/blob) and production (https://account.blob.core.windows.net/container/blob) URIs
- Handles all edge cases (query params, SAS tokens, different storage account formats)
- 4th call site of the same bug — consistency across codebase

**Implementation:** Used fully-qualified Azure.Storage.Blobs.BlobUriBuilder in Program.cs.

---

## 2026-04-07: Container Deployment

### Decision 11: FFmpeg via Dockerfile apt-get + .dockerignore

**Author:** Amos (DevOps)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-04-07

**Summary:** FFMpegCore requires native ffmpeg binary. Install at container build time via apt-get in Dockerfile runtime stage.

**Rationale:**
- Standard Linux package manager install is idiomatic
- --no-install-recommends + apt cache cleanup keeps image minimal
- .dockerignore prevents node_modules/, .squad/, .git/ from bloating build context

**Dockerfile:** Multi-stage build (SDK restore/publish, aspnet runtime with ffmpeg).

**Local dev:** winget install --id Gyan.FFmpeg (Windows) or rew install ffmpeg (macOS)

---

