![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)
![Azure](https://img.shields.io/badge/Azure-Cloud-blue)
![Aspire](https://img.shields.io/badge/.NET-Aspire-512BD4)
![Blazor](https://img.shields.io/badge/Blazor-Interactive_Server-9A3FF5)


# Meeting Minutes

AI-powered meeting transcription and summarization.

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for Aspire Azurite emulation)
- [ffmpeg](https://ffmpeg.org/) — required for audio extraction
  - Windows: `winget install --id Gyan.FFmpeg`
  - macOS: `brew install ffmpeg`
  - Linux: `apt-get install ffmpeg` / `dnf install ffmpeg`
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



### Running Locally

1. After setting user secrets for the OpenAI and Azure AI Speech services, run with Aspire:
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
- **Frontend**: Blazor Interactive Server (standalone ASP.NET Core process, SignalR-based) — **all endpoints public, no authentication required**
- **Backend**: ASP.NET Core Minimal API + BackgroundService worker (separate process from the Web app)
- **Storage**: Azure Blob Storage (videos, transcripts, summaries) + Table Storage (job metadata)
- **AI**: Azure AI Speech (transcription) + Azure OpenAI GPT-4o Mini (summarization)
- **Orchestration**: .NET Aspire manages both Web and API as separate resources (local dashboard + Azure Container Apps in production, scale-to-zero)

## License

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

This project is licensed under the [GNU General Public License v3.0](LICENSE).
