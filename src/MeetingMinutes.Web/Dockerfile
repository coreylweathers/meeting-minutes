FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MeetingMinutes.sln .
COPY src/ src/
COPY tests/ tests/
COPY global.json .
COPY Directory.Build.props .
RUN dotnet restore MeetingMinutes.sln
RUN dotnet publish src/MeetingMinutes.Web/MeetingMinutes.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MeetingMinutes.Web.dll"]
