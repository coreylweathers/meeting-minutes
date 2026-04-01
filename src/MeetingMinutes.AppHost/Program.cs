var builder = DistributedApplication.CreateBuilder(args);

// Local Azure Storage emulation via Azurite
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");
var tables = storage.AddTables("tables");

// OpenAI — set via user-secrets on the AppHost project:
// dotnet user-secrets set "ConnectionStrings:openai" "sk-<your-key>"
var openai = builder.AddConnectionString("openai");

// Azure Speech — connection string for local dev
var speech = builder.AddConnectionString("speech");

// Web project — Blazor Interactive Server with all services embedded
var web = builder.AddProject<Projects.MeetingMinutes_Web>("web")
    .WithReference(blobs)
    .WithReference(tables)
    .WithReference(openai)
    .WithReference(speech)
    .WithExternalHttpEndpoints();

builder.Build().Run();
