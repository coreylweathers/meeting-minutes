var builder = DistributedApplication.CreateBuilder(args);

// Local Azure Storage emulation via Azurite
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();  // Uses Azurite in Docker for local dev

var blobs = storage.AddBlobs("blobs");
var tables = storage.AddTables("tables");

// Azure OpenAI — use AddConnectionString for local dev 
// (points to real Azure OpenAI; devs set env var AZURE_OPENAI_ENDPOINT or user-secrets)
var openai = builder.AddConnectionString("openai");

// Azure Speech — connection string for local dev
// Devs set AzureSpeech:Key and AzureSpeech:Region via user-secrets or env vars
var speech = builder.AddConnectionString("speech");

// Api project — references storage and AI services
var api = builder.AddProject<Projects.MeetingMinutes_Api>("api")
    .WithReference(blobs)
    .WithReference(tables)
    .WithReference(openai)
    .WithReference(speech)
    .WithExternalHttpEndpoints();

// Web project — Blazor Interactive Server (separate ASP.NET Core server process)
// References the API via service discovery (injects API base URL into configuration)
// WaitFor(api) ensures API is ready before Web starts
var web = builder.AddProject<Projects.MeetingMinutes_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
