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
