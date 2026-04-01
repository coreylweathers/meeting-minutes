# Meeting Minutes

AI-powered meeting transcription and summarization.

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for Aspire Azurite emulation)
- Azure subscription

## Local Development

### Getting Your Credentials

#### OpenAI API Key (`ConnectionStrings:openai`)
1. Go to [OpenAI Platform](https://platform.openai.com) → **API Keys** → **Create new secret key**
2. Copy the key (starts with `sk-`)
3. Set the secret on the **AppHost** project:
   ```bash
   cd src/MeetingMinutes.AppHost
   dotnet user-secrets set "ConnectionStrings:openai" "sk-<your-key>"
   ```
   > 💡 The app uses the `gpt-4o-mini` model. Make sure your OpenAI account has API access enabled.

#### Azure AI Speech (`ConnectionStrings:speech`)
1. Go to [Azure Portal](https://portal.azure.com) → Create a resource → **Azure AI Speech** (under Azure AI services)
2. Choose a region and **Free F0** tier (500 minutes/month free)
3. Once deployed, go to **Keys and Endpoint** — copy **Key 1** and the **Location/Region** (e.g. `eastus`)
4. Set the secret:
   ```bash
   dotnet user-secrets set "ConnectionStrings:speech" "Endpoint=https://<region>.api.cognitive.microsoft.com/;Key=<key1>"
   ```

#### Microsoft OAuth (`Authentication:Microsoft:ClientId` / `ClientSecret`)
1. Go to [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `Meeting Minutes (local)`, Supported account types: **Accounts in any organizational directory and personal Microsoft accounts**
3. Redirect URI: `Web` → `https://localhost:<port>/signin-microsoft` (check your Aspire dashboard for the **web** resource port — the Web UI and API run as separate processes)
4. After registration, copy the **Application (client) ID**
5. Go to **Certificates & secrets** → **New client secret** → copy the value immediately
6. Set the secrets:
   ```bash
   dotnet user-secrets set "Authentication:Microsoft:ClientId" "<application-id>"
   dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<client-secret-value>"
   ```

#### Google OAuth (`Authentication:Google:ClientId` / `ClientSecret`)
1. Go to [Google Cloud Console](https://console.cloud.google.com) → **APIs & Services** → **Credentials**
2. Click **Create Credentials** → **OAuth client ID**
3. Application type: **Web application**, Name: `Meeting Minutes (local)`
4. Authorized redirect URIs: `https://localhost:<port>/signin-google` (use the **web** resource port from the Aspire dashboard, not the API port)
5. Copy the **Client ID** and **Client Secret**
6. Set the secrets:
   ```bash
   dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>"
   dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>"
   ```
   > 💡 You may need to configure the **OAuth consent screen** first (External, test mode is fine for dev).

> **Finding your port:** Run `dotnet run` from `src/MeetingMinutes.AppHost` — the Aspire dashboard URL is printed to the console. Both the **web** and **api** resource ports are shown there. OAuth redirect URIs should use the **web** port.

### Running Locally

1. After setting user secrets for the API and Web projects, run with Aspire:
   ```bash
   cd src/MeetingMinutes.AppHost
   dotnet run
   ```
   This starts three services: **Azurite** (local Azure Storage via Docker), the **API** (`MeetingMinutes.Api`), and the **Web UI** (`MeetingMinutes.Web` — Blazor Interactive Server). Each gets its own port visible in the Aspire dashboard. The Web UI communicates with the API via server-to-server calls using Aspire service discovery.

## Testing

Three test suites are included:

- **Unit tests** (`tests/MeetingMinutes.Tests/`) — xUnit service-layer tests. Run with `dotnet test tests/MeetingMinutes.Tests/`.
- **Component tests** (`tests/MeetingMinutes.Web.Tests/`) — bUnit Blazor component tests. Run with `dotnet test tests/MeetingMinutes.Web.Tests/`.
- **E2E tests** (`tests/MeetingMinutes.E2E/`) — Playwright end-to-end tests. Requires a running app. See `tests/MeetingMinutes.E2E/README.md` for setup.

Run all unit and component tests:
```bash
dotnet test
```

## Deploy to Azure

```bash
azd auth login
azd up
```

The deployment uses Azure Developer CLI (azd) with .NET Aspire integration. The `azure.yaml` configuration tells azd to use the Aspire AppHost to generate the deployment manifest, which creates:
- Azure Container Apps (with scale-to-zero for cost optimization)
- Azure Storage Account (Blob + Table storage)
- Azure OpenAI connection
- Azure AI Speech connection

Container Apps will scale to zero replicas when idle, minimizing costs.

## Architecture
- **Frontend**: Blazor Interactive Server (standalone ASP.NET Core process, SignalR-based)
- **Backend**: ASP.NET Core Minimal API + BackgroundService worker (separate process from the Web app)
- **Storage**: Azure Blob Storage (videos, transcripts, summaries) + Table Storage (job metadata)
- **AI**: Azure AI Speech (transcription) + Azure OpenAI GPT-4o Mini (summarization)
- **Orchestration**: .NET Aspire manages both Web and API as separate resources (local dashboard + Azure Container Apps in production, scale-to-zero)
