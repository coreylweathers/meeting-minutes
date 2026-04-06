// Meeting Minutes - AI-powered meeting transcription and summarization.
// Copyright (C) 2026 Corey Weathers
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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

// Deepgram — set via user-secrets on the AppHost project:
// dotnet user-secrets set "ConnectionStrings:deepgram" "<your-deepgram-api-key>"
var deepgram = builder.AddConnectionString("deepgram");

// Web project — Blazor Interactive Server with all services embedded
var web = builder.AddProject<Projects.MeetingMinutes_Web>("web")
    .WithReference(blobs)
    .WithReference(tables)
    .WithReference(openai)
    .WithReference(speech)
    .WithReference(deepgram)
    .WithExternalHttpEndpoints();

builder.Build().Run();
