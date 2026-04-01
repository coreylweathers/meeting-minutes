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

### 2025 — Scaffold Fix (Miller's Review)
- `AddMicrosoftIdentityWebApp()` returns `MicrosoftIdentityWebAppAuthenticationBuilder`, NOT `AuthenticationBuilder`. Cannot chain `.AddGoogle()` off it — use `AddMicrosoftAccount` (lightweight OAuth2) instead, which chains cleanly off `AuthenticationBuilder`.
- `AddMicrosoftAccount` requires the `Microsoft.AspNetCore.Authentication.MicrosoftAccount` NuGet package (v10.0.0 for net10.0). Not included transitively via `Microsoft.Identity.Web`.
- `Microsoft.AspNetCore.Components.WebAssembly.Server` must be explicitly referenced in the API project to serve the Blazor WASM app in hosted mode.
- Always add `using Microsoft.AspNetCore.Authentication;` when using `SignOutAsync` extension methods.
- Auth scheme name changes: when switching from `AddMicrosoftIdentityWebApp` to `AddMicrosoftAccount`, the scheme name changes from `"MicrosoftIdentityWebApp"` to `"MicrosoftAccount"` — update all `Results.Challenge` calls accordingly.

### 2025 — FFmpegHelper CancellationToken Fix (Corey Weathers / Miller rejection)
- FFMpegCore 5.1.0: `ProcessAsynchronously()` does NOT accept a `cancellationToken` parameter. The correct API is `.CancellableThrough(ct)` chained before `.ProcessAsynchronously()`.
- The task originally specified `.ProcessAsynchronously(cancellationToken: ct)` but that parameter name does not exist in this version. Used reflection to confirm actual method signature before applying the fix.
- Fix: inserted `.CancellableThrough(ct)` into the fluent chain in `FFmpegHelper.cs`; build confirmed 0 errors.

## 2026-03-31 18:03:58 - BlobStorageService 404 Handling Fix

- Fixed DownloadTextAsync method to return null on 404 instead of throwing exception

- Added try-catch block for Azure.RequestFailedException when Status == 404

- File: src/MeetingMinutes.Api/Services/BlobStorageService.cs

- Status: Complete

## 2025-01-29 - OpenAI Service Pre-Release Package Fix (Miller Rejection)

**Problem:** Miller rejected Naomi's openai-service PR due to pre-release packages:
- `Aspire.Azure.AI.OpenAI` v13.2.1-preview.1.26180.6 (preview)
- `Azure.AI.OpenAI` v2.5.0-beta.1 (newer beta than baseline)

**Solution:**
- Removed `Aspire.Azure.AI.OpenAI` package entirely from MeetingMinutes.Api.csproj
- Reverted `Azure.AI.OpenAI` from 2.5.0-beta.1 → 2.2.0-beta.4 (project baseline)
- Replaced `builder.AddAzureOpenAIClient("openai")` with manual registration in Program.cs:
  ```csharp
  var openAiEndpoint = builder.Configuration.GetConnectionString("openai") 
      ?? builder.Configuration["AZURE_OPENAI_ENDPOINT"]
      ?? throw new InvalidOperationException("OpenAI connection string not configured.");
  builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential()));
  ```
- Added usings: `Azure.AI.OpenAI` and `Azure.Identity`
- Build verified successful

**Files changed:**
- src/MeetingMinutes.Api/MeetingMinutes.Api.csproj
- src/MeetingMinutes.Api/Program.cs

**Status:** Complete, ready for Miller's review

## 2026-03-31 - Blazor Auth UI Implementation (blazor-auth)

**Task:** Implement authentication UI for Blazor WASM client using BFF cookie auth pattern

**Implementation:**
- Added `CookieAuthenticationStateProvider` that calls `GET /api/auth/user` and parses `{"name":"...", "email":"..."}` into ClaimsPrincipal
- Created `LoginDisplay` component with `AuthorizeView` showing user greeting/logout when authenticated, sign-in buttons when not
- Created `RedirectToLogin` component for unauthorized access (redirects to `/api/auth/login/microsoft`)
- Updated `App.razor` to use `AuthorizeRouteView` with `NotAuthorized` section
- Updated `MainLayout.razor` to include `<LoginDisplay />` in navbar
- Updated `Program.cs` with `AddAuthorizationCore()`, `AddCascadingAuthenticationState()`, and registered custom provider
- Added `Microsoft.AspNetCore.Components.Authorization` v10.0.0 package

**Files created:**
- `src/MeetingMinutes.Web/Auth/CookieAuthenticationStateProvider.cs`
- `src/MeetingMinutes.Web/Auth/RedirectToLogin.razor`
- `src/MeetingMinutes.Web/Shared/LoginDisplay.razor`

**Files modified:**
- `src/MeetingMinutes.Web/Program.cs`
- `src/MeetingMinutes.Web/MeetingMinutes.Web.csproj`
- `src/MeetingMinutes.Web/App.razor`
- `src/MeetingMinutes.Web/Layout/MainLayout.razor`
- `src/MeetingMinutes.Web/_Imports.razor`

**Build status:** ✅ Succeeded (0 errors)

**Status:** Complete, ready for Miller's review


## 2026-03-31 18:33:02 - Blazor Job Detail Page (blazor-jobdetail)

**Task:** Implement comprehensive job detail page for viewing and editing meeting processing jobs

**Implementation:**
- Created `src/MeetingMinutes.Web/Pages/JobDetail.razor` with route `/jobs/{Id}`
- Requires authentication with `[Authorize]` attribute

**Features implemented:**
1. **Job Header Section**
   - Displays job filename, status badge (color-coded: success/danger/warning), created date
   - Shows processing spinner for in-progress jobs (Pending, ExtractingAudio, Transcribing, Summarizing)
   - Displays error messages for failed jobs

2. **Transcript Section** (Completed jobs only)
   - Fetches via GET `/api/jobs/{Id}/transcript`
   - Displays in scrollable pre-formatted box with custom styling
   - Copy button using JavaScript clipboard API

3. **Summary Section** (Completed jobs only)
   - Fetches via GET `/api/jobs/{Id}/summary`
   - Displays structured summary: Title, Duration, Attendees, Key Points, Action Items, Decisions
   - All lists displayed as bulleted lists

4. **Edit Summary**
   - Toggle edit mode with "Edit" button
   - Inline editing for all summary fields
   - Attendees: comma-separated textarea
   - Key Points/Action Items/Decisions: one per line in textarea
   - Save sends PUT `/api/jobs/{Id}/summary` with UpdateSummaryRequest
   - Cancel reverts changes
   - Loading indicator during save

5. **Auto-refresh**
   - Polls GET `/api/jobs/{Id}` every 5 seconds while processing
   - Stops when Completed or Failed
   - Automatically loads transcript/summary when completed
   - Implements IDisposable for proper timer cleanup

6. **Navigation**
   - Back link to `/jobs` list page

**Technical details:**
- Uses System.Timers.Timer for polling
- Error handling with try-catch and console logging
- Bootstrap styling for responsive layout
- Custom CSS for transcript box styling
- Properly disposes resources

**Build status:** ✅ Succeeded (0 errors, build time: 38.4s)

**Status:** Complete, ready for Miller's review



## 2026-03-31 18:33:03 - Blazor Job List Page Implementation (blazor-joblist)

**Task:** Implement comprehensive job listing page with auto-refresh and status indicators

**Implementation:**
- Created full-featured /jobs page with table-based job list
- Integrated with GET /api/jobs API endpoint using HttpClient.GetFromJsonAsync<List<JobDto>>()
- Status badges with color coding and icons:
  - Pending (gray), ExtractingAudio/Transcribing/Summarizing (blue/info with spinner), Completed (green), Failed (red)
- Smart auto-refresh polling:
  - Polls every 5 seconds when any job is in non-terminal state
  - Stops automatically when all jobs are Completed or Failed
  - Uses System.Threading.Timer with proper IDisposable cleanup
- State management for loading, empty, error, and data states
- Relative date formatting ("X minutes ago") for recent jobs
- Navigation links to job detail pages (/jobs/{id})
- Authorization protected with [Authorize] attribute

**Files created:**
- src/MeetingMinutes.Web/Pages/Jobs.razor (replaced placeholder)

**Files modified:**
- src/MeetingMinutes.Web/_Imports.razor (added Microsoft.AspNetCore.Authorization using)

**Build status:** ✅ Succeeded (0 errors)

**Technical details:**
- Implements IDisposable for timer cleanup
- Uses InvokeAsync(StateHasChanged) for thread-safe UI updates from timer callback
- Bootstrap classes for responsive design
- Links to existing NavMenu entry

**Status:** Complete, ready for Miller's review

## 2026-03-31 - Blazor Upload Page Implementation (blazor-upload)

**Task:** Implement comprehensive video upload page with multipart form handling

**Implementation:**
- Replaced placeholder `src/MeetingMinutes.Web/Pages/Upload.razor` with full implementation
- Protected with `[Authorize]` attribute for authenticated access only

**Features implemented:**
1. **Form Fields**
   - Title text input (required, with DataAnnotations validation)
   - File picker restricted to video files (`accept="video/*"`)
   - Client-side validation for both fields
   - Visual feedback showing selected file name and formatted file size

2. **Upload Logic**
   - Uses injected `HttpClient` to POST to `/api/jobs`
   - Constructs `multipart/form-data` with title and file stream
   - Properly handles `StreamContent` with correct content type headers
   - Configurable max file size limit (500 MB)
   - Uses `IBrowserFile.OpenReadStream()` for efficient file streaming

3. **State Machine**
   - **Idle**: Form ready for input
   - **Uploading**: Shows spinner, disables all controls
   - **Success**: Displays success message with link to job status page (`/jobs/{jobId}`)
   - **Error**: Shows error message with "Try Again" button to reset form

4. **User Experience**
   - Real-time file size formatting (B, KB, MB, GB)
   - Disabled state prevents double-submission
   - Clear error messages for validation failures
   - Success flow includes parsed `JobDto` response for navigation
   - Bootstrap styling for responsive, professional UI

**Files modified:**
- `src/MeetingMinutes.Web/Pages/Upload.razor` (complete rewrite from placeholder)
- `src/MeetingMinutes.Web/_Imports.razor` (added `System.ComponentModel.DataAnnotations` and `Microsoft.AspNetCore.Authorization`)

**Technical details:**
- Uses `EditForm` with `DataAnnotationsValidator` for form validation
- Custom `UploadModel` class with `[Required]` attribute
- Enum-based state management for clean UI logic
- Parses API response to extract `JobId` for navigation
- Proper error handling with try-catch
- Form reset functionality for retry attempts

**Build status:** ✅ Succeeded (0 errors, build time: 43.4s)

**Navigation:** Upload link already exists in `Layout/NavMenu.razor`

**Status:** Complete, ready for Miller's review
