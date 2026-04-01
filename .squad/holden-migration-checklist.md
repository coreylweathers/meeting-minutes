# Interactive Server Migration — Implementation Checklist

**Owner:** Alex (Web), Amos (API/Testing)  
**Review:** Miller  
**Estimated Time:** 2-4 hours

---

## ✅ Pre-Flight

- [ ] **Backup:** Create git branch `feature/interactive-server`
- [ ] **Build baseline:** `dotnet build MeetingMinutes.sln` (verify clean)
- [ ] **Test baseline:** Run AppHost, test auth + upload + jobs
- [ ] **Screenshot:** Capture current working state

---

## 📦 Phase 1: Web Project (Alex)

### 1.1 Update Project File
**File:** `src/MeetingMinutes.Web/MeetingMinutes.Web.csproj`

- [ ] Change SDK: `Microsoft.NET.Sdk.BlazorWebAssembly` → `Microsoft.NET.Sdk.Web`
- [ ] Remove: `Microsoft.AspNetCore.Components.WebAssembly` package
- [ ] Remove: `Microsoft.AspNetCore.Components.WebAssembly.DevServer` package
- [ ] Keep: `Microsoft.AspNetCore.Components.Authorization` (10.0.5)
- [ ] Add: `Microsoft.AspNetCore.Components.Web` (10.0.5)
- [ ] Add: `Microsoft.Identity.Web` (4.6.0)
- [ ] Add: `Microsoft.AspNetCore.Authentication.Google` (10.0.5)
- [ ] Add: `Microsoft.AspNetCore.Authentication.MicrosoftAccount` (10.0.5)
- [ ] Add project reference: `MeetingMinutes.ServiceDefaults`
- [ ] Keep project reference: `MeetingMinutes.Shared`
- [ ] **Test:** `dotnet build src/MeetingMinutes.Web` (should fail — Program.cs needs update)

### 1.2 Replace Program.cs
**File:** `src/MeetingMinutes.Web/Program.cs`

- [ ] Copy template from architecture doc (lines 48-156)
- [ ] Verify `builder.AddServiceDefaults()` present
- [ ] Verify `AddRazorComponents().AddInteractiveServerComponents()` present
- [ ] Verify auth setup (AddAuthentication, AddCookie, AddMicrosoftAccount, AddGoogle)
- [ ] Verify HttpClient config with Aspire service discovery
- [ ] Verify `/auth/*` endpoints (login, logout, user)
- [ ] Verify `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- [ ] **Test:** `dotnet build src/MeetingMinutes.Web` (should build now)

### 1.3 Convert App.razor to Root Document
**File:** `src/MeetingMinutes.Web/App.razor`

- [ ] Replace with HTML document template (lines 170-182 in arch doc)
- [ ] Add `<!DOCTYPE html>`, `<html>`, `<head>`, `<body>` tags
- [ ] Add Bootstrap CSS link
- [ ] Add Bootstrap Icons link
- [ ] Add `<HeadOutlet @rendermode="InteractiveServer" />`
- [ ] Add `<Routes @rendermode="InteractiveServer" />`
- [ ] Add `<script src="_framework/blazor.web.js"></script>`

### 1.4 Create Routes.razor
**File:** `src/MeetingMinutes.Web/Routes.razor` (NEW)

- [ ] Create new file in `src/MeetingMinutes.Web/`
- [ ] Copy current App.razor `<Router>` content (lines 1-16)
- [ ] Change `AppAssembly="@typeof(App).Assembly"` → `@typeof(Program).Assembly`
- [ ] Keep `<AuthorizeRouteView>`, `<RedirectToLogin>`, etc.

### 1.5 Update _Imports.razor
**File:** `src/MeetingMinutes.Web/_Imports.razor`

- [ ] Remove: `@using Microsoft.AspNetCore.Components.WebAssembly.Http`
- [ ] Keep all other usings
- [ ] **Test:** `dotnet build src/MeetingMinutes.Web`

### 1.6 Fix Auth Provider
**File:** `src/MeetingMinutes.Web/Auth/CookieAuthenticationStateProvider.cs`

- [ ] Change `await _httpClient.GetAsync("/api/auth/user")` → `"/auth/user"`
- [ ] No other changes needed

### 1.7 Fix RedirectToLogin (if needed)
**File:** `src/MeetingMinutes.Web/Auth/RedirectToLogin.razor`

- [ ] Check if it references `/api/auth/login`
- [ ] If yes, change to `/auth/login`
- [ ] If no references, skip this step

### 1.8 Delete WASM Files
- [ ] Delete: `src/MeetingMinutes.Web/wwwroot/index.html`
- [ ] Keep: Other wwwroot files (if any exist)

### 1.9 Final Web Build
- [ ] **Test:** `dotnet build src/MeetingMinutes.Web` (0 errors, 0 warnings)

---

## 🧹 Phase 2: API Cleanup (Amos)

### 2.1 Update API Project File
**File:** `src/MeetingMinutes.Api/MeetingMinutes.Api.csproj`

- [ ] Remove: `Microsoft.AspNetCore.Components.WebAssembly.Server` package
- [ ] Remove: `Microsoft.Identity.Web` package
- [ ] Remove: `Microsoft.AspNetCore.Authentication.Google` package
- [ ] Remove: `Microsoft.AspNetCore.Authentication.MicrosoftAccount` package
- [ ] Keep: All other packages (Aspire, OpenAI, Speech, FFMpeg, etc.)

### 2.2 Clean API Program.cs
**File:** `src/MeetingMinutes.Api/Program.cs`

- [ ] Remove imports: `Microsoft.AspNetCore.Authentication.*`, `Microsoft.Identity.Web`
- [ ] Remove: `AddAuthentication()` block (lines ~33-51)
- [ ] Remove: `AddAuthorization()` (line ~53)
- [ ] Remove: `AddAntiforgery()` (line ~54)
- [ ] Remove: `app.UseAuthentication()` (line ~78)
- [ ] Remove: `app.UseAuthorization()` (line ~79)
- [ ] Remove: `app.UseBlazorFrameworkFiles()` (line ~81)
- [ ] Remove: `app.MapFallbackToFile("index.html")` (line ~321)
- [ ] Remove: `/api/auth` group endpoints (lines ~279-319)
- [ ] Change: `var jobs = app.MapGroup("/api/jobs").RequireAuthorization();` → remove `.RequireAuthorization()`
- [ ] Keep: CORS configuration (for now)
- [ ] Keep: All `/api/jobs/*` endpoints
- [ ] **Test:** `dotnet build src/MeetingMinutes.Api` (0 errors, 0 warnings)

---

## 🏗️ Phase 3: Verify AppHost (Amos)

**File:** `src/MeetingMinutes.AppHost/Program.cs`

- [ ] ✅ Verify line 29-32 has `builder.AddProject<Projects.MeetingMinutes_Web>("web")`
- [ ] ✅ Verify `.WithReference(api)` present
- [ ] ✅ Verify `.WaitFor(api)` present
- [ ] ✅ Verify `.WithExternalHttpEndpoints()` present
- [ ] **No changes needed** — already correct!

**File:** `src/MeetingMinutes.AppHost/MeetingMinutes.AppHost.csproj`

- [ ] ✅ Verify `<ProjectReference Include="..\MeetingMinutes.Api\...">` exists (line 14)
- [ ] ✅ Verify `<ProjectReference Include="..\MeetingMinutes.Web\...">` exists (line 15) — **ALREADY PRESENT!**
- [ ] **Test:** `dotnet build src/MeetingMinutes.AppHost`

---

## 🧪 Phase 4: Integration Testing (Amos + Alex)

### 4.1 Build Verification
- [ ] `dotnet build MeetingMinutes.sln` — 0 errors, 0 warnings
- [ ] Review build output for any unexpected package warnings

### 4.2 Start AppHost
- [ ] `dotnet run --project src/MeetingMinutes.AppHost`
- [ ] Verify both `api` and `web` resources start
- [ ] Check Aspire dashboard: http://localhost:15888
- [ ] Verify no startup errors in logs

### 4.3 Manual Testing

**Public Pages (No Auth):**
- [ ] Navigate to Web URL (e.g., https://localhost:7001)
- [ ] Verify home page loads
- [ ] Check browser DevTools → Network tab: NO WASM files downloaded
- [ ] Check browser DevTools → Network tab: SignalR connection established

**Authentication:**
- [ ] Click "Login" (or navigate to `/auth/login/microsoft`)
- [ ] Verify Microsoft OAuth flow works
- [ ] Verify redirect back to home page after login
- [ ] Verify user name displayed in nav bar
- [ ] Try Google login as well

**Authenticated Pages:**
- [ ] Navigate to `/upload`
- [ ] Verify page loads (should require auth)
- [ ] Upload a small test video
- [ ] Verify form submission works
- [ ] Verify redirect to job detail page

- [ ] Navigate to `/jobs`
- [ ] Verify jobs list loads
- [ ] Verify polling works (watch for status changes)
- [ ] Click job → verify detail page loads

**Logout:**
- [ ] Click "Logout"
- [ ] Verify redirect to home page
- [ ] Verify user no longer shown in nav
- [ ] Try accessing `/upload` → should redirect to login

### 4.4 Browser DevTools Checks
- [ ] **Console:** No JavaScript errors
- [ ] **Network:** SignalR WebSocket connection active (wss://...)
- [ ] **Network:** No WASM files downloaded (.dll, .wasm, .pdb)
- [ ] **Network:** API calls from Web → API succeed (200/201 responses)
- [ ] **Application → Cookies:** Auth cookie present after login

### 4.5 API Direct Testing (Optional)
- [ ] `curl https://localhost:7000/api/jobs` (should return 401 or empty array)
- [ ] Verify API still responds on its port
- [ ] Verify no fallback to index.html (should return 404 for unknown routes)

---

## 🎯 Success Criteria

✅ **Migration complete when ALL of these are true:**
1. Solution builds with 0 errors, 0 warnings
2. AppHost starts both Web and API without errors
3. Home page loads in browser
4. Microsoft + Google login both work
5. Upload page requires auth and works correctly
6. Jobs list page shows jobs and polls for updates
7. Job detail page displays correctly
8. Logout works and redirects
9. SignalR connection visible in browser DevTools
10. NO WASM files downloaded in browser Network tab

---

## 🚨 Rollback Plan

If any blocking issue occurs:

```bash
# Save progress
git add -A
git commit -m "WIP: Interactive Server migration (blocked)"

# Rollback
git checkout main
git branch -D feature/interactive-server

# Document blocker in .squad/decisions/inbox/holden-server-migration-blocked.md
```

---

## 📋 Post-Migration Tasks

- [ ] Git commit: `git commit -m "feat: migrate to Blazor Interactive Server"`
- [ ] Update `.squad/agents/holden/history.md` with completion notes
- [ ] Move `holden-server-migration-arch.md` from inbox → archive
- [ ] Update README.md architecture section
- [ ] **Request Miller review** before merging

---

## 📞 Help

**Stuck?** Ping Holden or reference:
- Full architecture: `.squad/decisions/inbox/holden-server-migration-arch.md`
- Quick guide: `.squad/holden-migration-guide.md`
- Comparison: `.squad/holden-architecture-comparison.md`
