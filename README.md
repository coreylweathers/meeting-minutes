# Meeting Minutes

AI-powered meeting transcription and summarization.

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for Aspire Azurite emulation)
- Azure subscription

## Local Development

### Getting Your Credentials

#### Azure OpenAI (`ConnectionStrings:openai`)
1. Go to [Azure Portal](https://portal.azure.com) → Create a resource → **Azure OpenAI**
2. Select a region (e.g. East US), pricing tier: **Standard S0**
3. Once deployed, go to **Keys and Endpoint** — copy the **Endpoint URL** (format: `https://<name>.openai.azure.com/`)
4. Go to **Azure OpenAI Studio** → Deployments → **New deployment** → choose model **gpt-4o-mini**, name it `gpt-4o-mini`
5. Set the secret to the endpoint URL (the key itself is not needed — the app uses `DefaultAzureCredential`):
   ```bash
   dotnet user-secrets set "ConnectionStrings:openai" "https://<your-resource>.openai.azure.com/"
   ```
   > 💡 For local dev, run `az login` first so `DefaultAzureCredential` can authenticate. Assign yourself the **Cognitive Services OpenAI User** role on the resource.

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
3. Redirect URI: `Web` → `https://localhost:<port>/signin-microsoft` (check your Aspire dashboard for the port)
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
4. Authorized redirect URIs: `https://localhost:<port>/signin-google`
5. Copy the **Client ID** and **Client Secret**
6. Set the secrets:
   ```bash
   dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>"
   dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>"
   ```
   > 💡 You may need to configure the **OAuth consent screen** first (External, test mode is fine for dev).

> **Finding your port:** Run `dotnet run` from `src/MeetingMinutes.AppHost` — the Aspire dashboard URL is printed to the console. The API port is shown there.

### Running Locally

1. After setting user secrets for the API project, run with Aspire:
   ```bash
   cd src/MeetingMinutes.AppHost
   dotnet run
   ```
   This starts Azurite (local Azure Storage) via Docker and the API (which serves the Blazor UI).

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
- **Frontend**: Blazor WebAssembly (served by API, BFF pattern)
- **Backend**: ASP.NET Core Minimal API + BackgroundService worker
- **Storage**: Azure Blob Storage (videos, transcripts, summaries) + Table Storage (job metadata)
- **AI**: Azure AI Speech (transcription) + Azure OpenAI GPT-4o Mini (summarization)
- **Orchestration**: .NET Aspire (local) + Azure Container Apps (production, scale-to-zero)
