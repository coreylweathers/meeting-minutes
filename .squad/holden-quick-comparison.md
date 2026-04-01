# Quick Comparison: What Changes Where

## Web Project Changes

| File | Current (WASM) | New (Interactive Server) | Action |
|------|---------------|-------------------------|---------|
| **MeetingMinutes.Web.csproj** | SDK: `BlazorWebAssembly` | SDK: `Web` | ✏️ Edit |
| | `Components.WebAssembly` package | Remove | ❌ Delete |
| | No ServiceDefaults reference | Add `ServiceDefaults` reference | ➕ Add |
| | No auth packages | Add `Microsoft.Identity.Web`, Google, MSAccount | ➕ Add |
| **Program.cs** | `WebAssemblyHostBuilder` | `WebApplication.CreateBuilder` | 🔄 Replace |
| | `builder.RootComponents.Add<App>("#app")` | `app.MapRazorComponents<App>()` | 🔄 Replace |
| | HttpClient = browser | HttpClient = Aspire service discovery | 🔄 Replace |
| | No auth code | Full OAuth + cookie setup | ➕ Add |
| | No endpoints | `/auth/login`, `/auth/logout`, `/auth/user` | ➕ Add |
| **App.razor** | Just `<Router>` | Full HTML document with `<head>`, `<body>` | 🔄 Replace |
| **Routes.razor** | Doesn't exist | Router logic from old App.razor | ➕ Create |
| **_Imports.razor** | Has `WebAssembly.Http` using | Remove WASM using | ✏️ Edit |
| **Auth/CookieAuthenticationStateProvider.cs** | Calls `/api/auth/user` | Calls `/auth/user` | ✏️ Edit |
| **wwwroot/index.html** | Exists (entry point) | Delete (App.razor replaces it) | ❌ Delete |
| **Pages/*.razor** | Inject HttpClient | No changes needed | ✅ Keep |

---

## API Project Changes

| File | Current | New | Action |
|------|---------|-----|---------|
| **MeetingMinutes.Api.csproj** | Has `WebAssembly.Server` package | Remove | ❌ Delete |
| | Has auth packages | Remove (moved to Web) | ❌ Delete |
| **Program.cs** | Has `UseBlazorFrameworkFiles()` | Remove | ❌ Delete |
| | Has `MapFallbackToFile("index.html")` | Remove | ❌ Delete |
| | Has `AddAuthentication().AddCookie()...` | Remove (moved to Web) | ❌ Delete |
| | Has `UseAuthentication()` / `UseAuthorization()` | Remove | ❌ Delete |
| | Has `/api/auth/*` endpoints | Remove (moved to Web) | ❌ Delete |
| | Has `.RequireAuthorization()` on jobs | Remove (internal API now) | ❌ Delete |
| | Has CORS configuration | Keep for now | ✅ Keep |
| | Has `/api/jobs/*` endpoints | Keep (still needed) | ✅ Keep |
| | Has services, workers | No changes | ✅ Keep |

---

## AppHost Changes

| File | Current | New | Action |
|------|---------|-----|---------|
| **MeetingMinutes.AppHost.csproj** | Has Web project reference | No changes | ✅ Already correct |
| **Program.cs** | Has `AddProject<Projects.MeetingMinutes_Web>("web")` | No changes | ✅ Already correct |
| | Has `.WithReference(api).WaitFor(api)` | No changes | ✅ Already correct |

**Result:** AppHost needs ZERO changes! 🎉

---

## Package Changes Summary

### Web Project
```diff
- Microsoft.AspNetCore.Components.WebAssembly (10.0.5)
- Microsoft.AspNetCore.Components.WebAssembly.DevServer (10.0.5)
+ Microsoft.AspNetCore.Components.Web (10.0.5)
+ Microsoft.Identity.Web (4.6.0)
+ Microsoft.AspNetCore.Authentication.Google (10.0.5)
+ Microsoft.AspNetCore.Authentication.MicrosoftAccount (10.0.5)
  Microsoft.AspNetCore.Components.Authorization (10.0.5)  [KEEP]
+ ProjectReference: MeetingMinutes.ServiceDefaults
  ProjectReference: MeetingMinutes.Shared  [KEEP]
```

### API Project
```diff
- Microsoft.AspNetCore.Components.WebAssembly.Server (10.0.5)
- Microsoft.Identity.Web (4.6.0)
- Microsoft.AspNetCore.Authentication.Google (10.0.5)
- Microsoft.AspNetCore.Authentication.MicrosoftAccount (10.0.5)
  [All other packages unchanged]
```

---

## Endpoint Changes

| Endpoint | Before | After |
|----------|--------|-------|
| `/auth/login/{provider}` | In API (`/api/auth/login/{provider}`) | In Web (`/auth/login/{provider}`) |
| `/auth/logout` | In API (`/api/auth/logout`) | In Web (`/auth/logout`) |
| `/auth/user` | In API (`/api/auth/user`) | In Web (`/auth/user`) |
| `/api/jobs` | In API with `.RequireAuthorization()` | In API without auth (internal) |
| `/api/jobs/{id}` | In API with `.RequireAuthorization()` | In API without auth (internal) |
| `/api/jobs/{id}/transcript` | In API with `.RequireAuthorization()` | In API without auth (internal) |
| `/api/jobs/{id}/summary` | In API with `.RequireAuthorization()` | In API without auth (internal) |

---

## Code Patterns to Update

### HttpClient Registration

**Before (WASM):**
```csharp
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
```

**After (Interactive Server):**
```csharp
var apiBaseUrl = builder.Configuration["services:api:https:0"] 
    ?? builder.Configuration["services:api:http:0"]
    ?? throw new InvalidOperationException("API service not found");

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddStandardResilienceHandler();

builder.Services.AddScoped(sp => 
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));
```

### Component Rendering

**Before (WASM):**
```csharp
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
await builder.Build().RunAsync();
```

**After (Interactive Server):**
```csharp
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
```

### App.razor Structure

**Before (WASM):**
```razor
<Router AppAssembly="@typeof(App).Assembly">
    <!-- router logic -->
</Router>
```

**After (Interactive Server):**
```razor
<!DOCTYPE html>
<html>
<head>
    <!-- head content -->
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

---

## Testing Focus Areas

| Area | What to Test | Expected Behavior |
|------|-------------|-------------------|
| **Initial Load** | Open home page | Fast load, no WASM download (~5-10KB instead of 2MB) |
| **SignalR** | Check DevTools Network tab | WebSocket connection to `/_blazor` |
| **Auth** | Click login → Microsoft | OAuth flow, redirect back to home |
| **Auth** | Click login → Google | OAuth flow, redirect back to home |
| **Protected Routes** | Navigate to `/upload` without auth | Redirect to login |
| **Upload** | Upload test video | Form submission works, redirect to job detail |
| **Jobs List** | Navigate to `/jobs` | Jobs load, polling works |
| **Job Detail** | Click job from list | Detail page loads with transcript/summary |
| **Logout** | Click logout | Redirect to home, no longer authenticated |
| **API Calls** | Check DevTools Network tab | Web → API calls succeed (no CORS preflight) |

---

## Common Pitfalls

❌ **Forgetting to move auth packages to Web**
→ Build will fail with missing types

❌ **Not removing auth middleware from API**
→ `.RequireAuthorization()` will fail (no auth configured)

❌ **Not updating endpoint paths in CookieAuthenticationStateProvider**
→ Auth state checks will fail (404 on `/api/auth/user`)

❌ **Not deleting wwwroot/index.html**
→ May cause routing confusion

❌ **Not adding ServiceDefaults reference to Web**
→ Aspire service discovery won't work, HttpClient can't find API

❌ **Keeping `UseBlazorFrameworkFiles()` in API**
→ API will try to serve non-existent WASM files

---

**Ready to implement?** Follow `.squad/holden-migration-checklist.md` step-by-step!
