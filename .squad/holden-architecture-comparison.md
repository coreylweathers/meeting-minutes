# Architecture Comparison: WASM vs Interactive Server

## Current Architecture (Blazor WASM)

```
┌─────────────────────────────────────────────────────────────┐
│                    Browser (Client)                         │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Blazor WASM App (Downloads ~2MB on first load)      │ │
│  │  - Runs .NET in WebAssembly                          │ │
│  │  - HttpClient calls API endpoints                    │ │
│  │  - CookieAuthenticationStateProvider                 │ │
│  └──────────────────┬────────────────────────────────────┘ │
└─────────────────────┼────────────────────────────────────────┘
                      │ HTTPS (Cross-origin)
                      │ CORS required
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              API Server (Container App)                     │
│  ┌────────────────────────────────────────────────────┐    │
│  │  ASP.NET Core Minimal API                          │    │
│  │  - UseBlazorFrameworkFiles() ← serves WASM         │    │
│  │  - MapFallbackToFile("index.html")                 │    │
│  │  - OAuth providers (Microsoft, Google)             │    │
│  │  - Cookie authentication                           │    │
│  │  - /api/auth/* endpoints                           │    │
│  │  - /api/jobs/* endpoints                           │    │
│  │  - BackgroundService worker                        │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                      │
                      ▼
            Azure Services (Blob, Table, OpenAI, Speech)
```

**Pros:**
- Offline-capable (after initial load)
- Client-side rendering

**Cons:**
- ❌ Large initial download (~2MB WASM runtime)
- ❌ Slow first load
- ❌ CORS complexity
- ❌ API must host static files
- ❌ Auth split across client/server (BFF pattern required)

---

## Target Architecture (Interactive Server)

```
┌─────────────────────────────────────────────────────────────┐
│                    Browser (Client)                         │
│  ┌───────────────────────────────────────────────────────┐ │
│  │  Blazor Interactive Server (Thin client)             │ │
│  │  - HTML rendered on server, sent to browser         │ │
│  │  - SignalR WebSocket for interactivity              │ │
│  │  - No .NET runtime download needed                  │ │
│  └──────────────────┬────────────────────────────────────┘ │
└─────────────────────┼────────────────────────────────────────┘
                      │ SignalR (WebSocket)
                      │ No CORS needed
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              Web Server (Container App)                     │
│  ┌────────────────────────────────────────────────────┐    │
│  │  ASP.NET Core with Razor Components                │    │
│  │  - AddRazorComponents()                            │    │
│  │  - AddInteractiveServerComponents()               │    │
│  │  - MapRazorComponents<App>()                      │    │
│  │  - OAuth providers (Microsoft, Google)             │    │
│  │  - Cookie authentication                           │    │
│  │  - /auth/* endpoints                               │    │
│  │  - Renders components server-side                  │    │
│  │  - Sends HTML diffs over SignalR                   │    │
│  └────────────────────┬───────────────────────────────┘    │
└─────────────────────┼─────────────────────────────────────┘
                      │ Server-to-Server HTTP
                      │ (Aspire service discovery)
                      │ No CORS needed
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              API Server (Container App)                     │
│  ┌────────────────────────────────────────────────────┐    │
│  │  ASP.NET Core Minimal API                          │    │
│  │  - /api/jobs/* endpoints (no auth needed)          │    │
│  │  - BackgroundService worker                        │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                      │
                      ▼
            Azure Services (Blob, Table, OpenAI, Speech)
```

**Pros:**
- ✅ Fast initial load (no WASM download)
- ✅ Server-side rendering (better SEO, accessibility)
- ✅ No CORS complexity
- ✅ Simpler auth (all in Web project)
- ✅ API is pure REST service
- ✅ Server-to-server calls via Aspire

**Cons:**
- ❌ Requires WebSocket/SignalR connection
- ❌ Not offline-capable
- ❌ Server memory per connected user

---

## Migration Impact

### Web Project
| Aspect | WASM | Interactive Server |
|--------|------|-------------------|
| **SDK** | `Microsoft.NET.Sdk.BlazorWebAssembly` | `Microsoft.NET.Sdk.Web` |
| **Builder** | `WebAssemblyHostBuilder` | `WebApplication.CreateBuilder` |
| **HttpClient** | Browser, `BaseAddress = HostEnvironment.BaseAddress` | Server-side, Aspire service discovery |
| **Auth** | Calls API `/api/auth/user` | Hosts auth directly `/auth/login` |
| **Hosting** | Served by API as static files | Standalone ASP.NET Core app |
| **Interactivity** | WASM runtime | SignalR WebSocket |

### API Project
| Aspect | Before | After |
|--------|--------|-------|
| **WASM Hosting** | ✅ `UseBlazorFrameworkFiles()` | ❌ Removed |
| **Static Files** | ✅ `MapFallbackToFile("index.html")` | ❌ Removed |
| **Auth** | ✅ OAuth + cookies | ❌ Moved to Web |
| **Auth Endpoints** | ✅ `/api/auth/*` | ❌ Moved to Web |
| **Job Endpoints** | ✅ `/api/jobs/*` with `.RequireAuthorization()` | ✅ `/api/jobs/*` (no auth needed, internal) |
| **CORS** | ✅ Required for WASM | ⚠️ Optional (kept for now) |

### AppHost
| Aspect | Status |
|--------|--------|
| **Web Project Reference** | ✅ Already configured |
| **API Reference** | ✅ Already configured |
| **Service Discovery** | ✅ Already wired up |
| **Changes Needed** | ✅ None! |

---

## Request Flow Comparison

### WASM: User clicks "Upload"
1. Browser → API: `GET /api/auth/user` (check auth via WASM HttpClient)
2. API → Browser: 200 OK with user info
3. WASM renders upload page
4. Browser → API: `POST /api/jobs` with video file (multipart form)
5. API saves to blob storage
6. API → Browser: 201 Created with job ID
7. WASM navigates to job detail page

**Round-trips:** 2 HTTP requests before rendering, CORS preflight on each request

### Interactive Server: User clicks "Upload"
1. Browser → Web: SignalR message "navigate to /upload"
2. Web checks auth state (server-side)
3. Web renders upload page server-side
4. Web → Browser: HTML diff over SignalR
5. Browser submits form → Web
6. Web → API: `POST /api/jobs` with video file (server-to-server)
7. API saves to blob storage
8. API → Web: 201 Created with job ID
9. Web → Browser: Navigate to job detail (SignalR)

**Round-trips:** 1 SignalR connection (persistent), no CORS, faster rendering

---

**Conclusion:** Interactive Server simplifies architecture, improves performance, and reduces deployment complexity at the cost of offline capability (not needed for this app).
