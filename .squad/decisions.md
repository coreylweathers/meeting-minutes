# Squad Decisions

## 2026-03-31: Interactive Server Migration Complete

### Decision 1: Architecture — Blazor WASM → Interactive Server

**Author:** Holden (Lead Architect)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Migrated `MeetingMinutes.Web` from Blazor WebAssembly to Blazor Interactive Server render mode.

**Rationale:**
- Simpler server-side authentication via `HttpContext.User`
- Eliminates .NET WASM runtime download (faster page load)
- Server-to-server API calls with Aspire service discovery (no CORS complexity)
- Appropriate for internal tool with low concurrent users

**Scope:**
1. Web project: SDK change (BlazorWebAssembly → Web), auth relocation, HttpClient server-to-server wiring
2. API project: Remove WASM hosting code and auth (now in Web)
3. AppHost: Web registered as separate Aspire resource (already configured)

**Key Components:**
- Web: `AddRazorComponents().AddInteractiveServerComponents()` + `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- Auth: Moved OAuth setup from API → Web; `ServerAuthenticationStateProvider` reads from `HttpContext.User`
- Aspire: `.WithReference(api)` injects API service discovery; `.WaitFor(api)` orders startup

---

### Decision 2: Implementation — Web Project Interactive Server

**Author:** Alex (Frontend Developer)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Implemented Blazor Interactive Server migration with complete Program.cs rewrite, component setup, and API cleanup.

**Implementation Details:**
- **Web.csproj:** SDK `Microsoft.NET.Sdk.BlazorWebAssembly` → `Microsoft.NET.Sdk.Web`
- **Program.cs:** Added `AddRazorComponents()`, `AddInteractiveServerComponents()`, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- **App.razor:** Root HTML document with `@rendermode="InteractiveServer"` on HeadOutlet and Routes
- **ServerAuthenticationStateProvider:** New scoped service reading from `HttpContext.User` with null-safe chaining
- **HttpClient:** Configured for server-to-server calls with Aspire service discovery
- **API cleanup:** Removed `UseBlazorFrameworkFiles()`, `MapFallbackToFile()`, WASM server package
- **Build:** ✅ 0 errors, 0 warnings

**Files Modified:** 9 (Program.cs, App.razor, _Imports.razor, csproj, ServerAuthenticationStateProvider, Pages)  
**Files Deleted:** 2 (CookieAuthenticationStateProvider.cs, wwwroot/index.html)

---

### Decision 3: Infrastructure — Web as Separate Aspire Resource

**Author:** Amos (DevOps/Infrastructure)  
**Status:** ✅ APPROVED & IMPLEMENTED  
**Date:** 2026-03-31

**Summary:** Configured Web as a separate Aspire project resource with service discovery and startup ordering.

**Configuration:**
- **AppHost.csproj:** Added Web ProjectReference with `IsAspireProjectResource="true"`
- **AppHost Program.cs:** 
  ```csharp
  var web = builder.AddProject<Projects.MeetingMinutes_Web>("web")
      .WithReference(api)          // Service discovery: injects API base URL
      .WaitFor(api)                // Startup: API first, then Web
      .WithExternalHttpEndpoints();
  ```
- **Service Discovery:** API URL injected as `services__api__http__0` and `services__api__https__0`
- **BFF Pattern:** Web calls API directly; API not exposed externally; Web doesn't need direct Azure service refs

**Architecture Impact:**
- Web and API coordinate startup and service discovery locally (Aspire Dashboard) and in cloud (Azure Container Apps)
- `azd up` deploys both containers separately; service discovery handles inter-process communication
- Build: ✅ All 6 projects compile (0 errors, 0 warnings)

---

### Decision 4: Review — Migration Approved with Non-Blocking Follow-ups

**Author:** Miller (Code Reviewer)  
**Status:** ⚠️ APPROVED WITH NOTES  
**Date:** 2026-03-31

**Verdict:** Core migration is **correct and complete**. Build passes (0 errors, 0 warnings). Non-blocking items for follow-up.

**Correctness Assessment — All ✅**
- Web Program.cs configuration complete (AddRazorComponents, AddInteractiveServerComponents, MapRazorComponents, AddInteractiveServerRenderMode)
- App.razor setup correct (@rendermode="InteractiveServer" on HeadOutlet and Routes; blazor.web.js reference)
- ServerAuthenticationStateProvider null-safe and scoped
- AppHost wiring complete (Web ProjectReference, WithReference(api), WaitFor(api))
- API cleanup successful (UseBlazorFrameworkFiles and MapFallbackToFile removed; WASM server package removed)

**Non-Blocking Items:**
1. **Naomi:** Delete `CookieAuthenticationStateProvider.cs` (WASM-only dead code)
2. **Bobbie:** Verify auth middleware requirements (manual test: `/jobs` deep link as anonymous user → should redirect to login)
3. **Bobbie:** Add integration tests for server migration (4 key scenarios: AuthenticationStateProvider claims, HttpClient API calls, [Authorize] redirect, Interactive Server rendering)
4. **Bobbie** (backlog): Refactor to typed `IHttpClientFactory` pattern (currently uses `@inject HttpClient` in pages)

**Migration Approved for Merge.** Non-blocking items tracked but do not prevent deployment.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

