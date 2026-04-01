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

// Api project — references all dependencies
// The Api serves the Blazor WASM Web project via UseBlazorFrameworkFiles()
var api = builder.AddProject<Projects.MeetingMinutes_Api>("api")
    .WithReference(blobs)
    .WithReference(tables)
    .WithReference(openai)
    .WithReference(speech)
    .WithExternalHttpEndpoints();

builder.Build().Run();
