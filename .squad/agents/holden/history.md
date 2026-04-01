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

### 2025-07-11: Major NuGet Package Upgrades (holden-major-updates)
**Status:** ✅ Complete — build clean, 0 errors, 0 warnings

**Packages Updated:**
- `Aspire.Hosting.AppHost` 9.1.0 → 13.2.1 (AppHost)
- `Aspire.Hosting.Azure.Storage` 9.1.0 → 13.2.1 (AppHost)
- `Aspire.Azure.Data.Tables` 9.1.0 → 13.2.1 (Api)
- `Aspire.Azure.Storage.Blobs` 9.1.0 → 13.2.1 (Api)
- `Microsoft.Identity.Web` 3.8.2 → 4.6.0 (Api)
- `Microsoft.Extensions.Http.Resilience` 9.4.0 → 10.4.0 (ServiceDefaults)
- `Microsoft.Extensions.ServiceDiscovery` 9.1.0 → 10.4.0 (ServiceDefaults)

**Breaking Changes Fixed:**
- `Aspire.Azure.Storage.Blobs` 13.x deprecated `AddAzureBlobClient()` → replaced with `AddAzureBlobServiceClient()` in Api/Program.cs
- `Aspire.Azure.Data.Tables` 13.x deprecated `AddAzureTableClient()` → replaced with `AddAzureTableServiceClient()` in Api/Program.cs
- No changes needed in AppHost/Program.cs, ServiceDefaults/Extensions.cs, or auth setup

**Key Learnings:**
- Aspire 13.x renamed client registration methods for blob/table storage; method signatures are compatible, just renamed
- `Microsoft.Identity.Web` 4.x had no breaking changes to the `AddMicrosoftAccount()` pattern used here
- `Microsoft.Extensions.Http.Resilience` 10.x and `Microsoft.Extensions.ServiceDiscovery` 10.x APIs remained stable

**Final Build:** `dotnet build MeetingMinutes.sln` — Build succeeded, 0 Warning(s), 0 Error(s)



### 2025-01-29: API Endpoints Implementation (api-endpoints)
**Status:** ✅ Complete (pending Miller review)

Implemented all 6 REST API endpoints in Program.cs:
- POST /api/jobs - Upload video and create job with multipart form
- GET /api/jobs - List all jobs (currently returns all, needs user filtering)
- GET /api/jobs/{id} - Get single job details
- GET /api/jobs/{id}/transcript - Download transcript as plain text
- GET /api/jobs/{id}/summary - Get summary JSON as SummaryDto
- PUT /api/jobs/{id}/summary - Update summary with UpdateSummaryRequest

**Key Technical Decisions:**
- All endpoints require authentication via `.RequireAuthorization()`
- Added `AddAntiforgery()` service for form upload support
- POST endpoint uses `.DisableAntiforgery()` for multipart compatibility
- PUT summary endpoint uses BlobServiceClient directly to upload to correct container
- Created helper method `MapToJobDto()` for ProcessingJob → JobDto conversion

**Known Limitations:**
- ProcessingJob entity lacks UserId field, so user ownership filtering not implemented yet
- All users can currently see all jobs (security issue to address)
- PUT summary endpoint accesses BlobServiceClient directly (consider adding service method)

**Build Status:** All API code compiles successfully with no C# errors

**Testing Needed:** Manual testing of all endpoints with authentication, file upload validation, and edge cases

### 2025-01-27: API Auth Endpoints (api-auth)
**Status:** ✅ Complete (pending Miller review)

Finalized BFF cookie authentication endpoints for Blazor client:
- GET /api/auth/user - Returns authenticated user info (name, email) or 401
- GET /api/auth/login/{provider} - Triggers OAuth challenge for "microsoft" or "google"
- GET /api/auth/logout - Signs out and redirects to home

**Changes Made:**
- Added `using Microsoft.AspNetCore.Authentication.MicrosoftAccount;`
- Migrated endpoints from `/auth` to `/api/auth` base path
- Updated `/user` endpoint to return 401 instead of `{ isAuthenticated: false }`
- Implemented dynamic provider selection in `/login/{provider}`
- Changed logout from POST to GET for consistency

**Build Status:** ✅ Project builds successfully with no errors

**Auth Flow:**
- Cookie-based authentication with 7-day expiration + sliding window
- Microsoft and Google OAuth providers configured
- Blazor client calls these API endpoints, never handles tokens directly
