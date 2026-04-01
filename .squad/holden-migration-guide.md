# Interactive Server Migration — Quick Reference

**For:** Alex (Frontend), Amos (DevOps)  
**Full Details:** See `.squad/decisions/inbox/holden-server-migration-arch.md`

---

## 🎯 Goal
Convert `MeetingMinutes.Web` from **Blazor WASM** → **Blazor Interactive Server**

---

## 📦 Package Changes

### Web.csproj
```xml
<!-- REMOVE -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" />
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" />

<!-- ADD -->
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.5" />
<PackageReference Include="Microsoft.Identity.Web" Version="4.6.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.5" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.MicrosoftAccount" Version="10.0.5" />

<!-- ADD PROJECT REFERENCE -->
<ProjectReference Include="..\MeetingMinutes.ServiceDefaults\MeetingMinutes.ServiceDefaults.csproj" />
```

### Api.csproj
```xml
<!-- REMOVE -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" />
<PackageReference Include="Microsoft.Identity.Web" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.MicrosoftAccount" />
```

---

## 🔧 Code Changes

### Web/Program.cs
**Replace entire file** with ASP.NET Core setup:
- `WebApplication.CreateBuilder(args)` (not WebAssemblyHostBuilder)
- `AddRazorComponents().AddInteractiveServerComponents()`
- Add auth setup (moved from API)
- Add HttpClient with Aspire service discovery
- Add `/auth/*` endpoints (moved from API)
- `MapRazorComponents<App>().AddInteractiveServerRenderMode()`

### Web/App.razor
**Replace** with root HTML document:
```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="bootstrap.min.css" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

### Web/Routes.razor (NEW FILE)
**Create** with router logic (copy current App.razor content):
```razor
<Router AppAssembly="@typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)">
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>...</NotFound>
</Router>
```

### Web/_Imports.razor
**Remove:** `@using Microsoft.AspNetCore.Components.WebAssembly.Http`

### Web/Auth/CookieAuthenticationStateProvider.cs
**Change endpoint:** `/api/auth/user` → `/auth/user`

### Web/wwwroot/index.html
**DELETE** — App.razor replaces it

---

## 🛠️ API Cleanup

### Api/Program.cs
**REMOVE:**
- `app.UseBlazorFrameworkFiles()`
- `app.MapFallbackToFile("index.html")`
- All `AddAuthentication().AddCookie()...` code
- `app.UseAuthentication()` / `app.UseAuthorization()`
- `/api/auth/*` endpoints
- `.RequireAuthorization()` from `/api/jobs` group (API is now internal)

**KEEP:**
- CORS (for now)
- All `/api/jobs` endpoints

---

## 🧪 Testing Checklist

1. Build: `dotnet build MeetingMinutes.sln`
2. Run: `dotnet run --project src/MeetingMinutes.AppHost`
3. Verify Web + API both start
4. Test home page (public)
5. Test login (Microsoft/Google)
6. Test upload page (auth required)
7. Test jobs list + detail
8. Verify SignalR in DevTools Network tab
9. Verify no WASM downloads

---

## 🚨 Common Issues

**"API service not found in configuration"**
→ Ensure AppHost has `WithReference(api)` on Web project

**"401 Unauthorized on /api/jobs"**
→ Remove `.RequireAuthorization()` from API (or add JWT validation)

**"Components not interactive"**
→ Verify `@rendermode="InteractiveServer"` in App.razor

**"OAuth redirect fails"**
→ Update redirect URIs in Azure/Google console to Web URL

---

**Questions?** Ping Holden or see full architecture doc.
