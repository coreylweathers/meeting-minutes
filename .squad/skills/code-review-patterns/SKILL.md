# Code Review Patterns — .NET Aspire + Blazor

Reusable review checklist patterns for MeetingMinutes-style solutions.

## Quick Security Scan

```bash
# Hardcoded secrets (run from repo root)
grep -rn "password\|secret\|apikey" src/ --include="*.cs" --include="*.json" | grep -v "Configuration\|IConfiguration\|ClientSecret\": \"\""

# Connection strings in code
grep -rn "Server=\|AccountKey=\|SharedAccessSignature=" src/

# Hardcoded Azure resource names/keys
grep -rn "https://.*\.blob\.core\.windows\.net\|https://.*\.openai\.azure\.com" src/
```

## Aspire AppHost Checklist

- [ ] `IsAspireProjectResource="true"` on Web ProjectReference
- [ ] `.WithReference()` for all service dependencies
- [ ] `.WaitFor()` for startup ordering
- [ ] `.WithExternalHttpEndpoints()` for external-facing services
- [ ] `AddConnectionString()` for secrets (not inline)
- [ ] `RunAsEmulator()` only in dev profile

## Blazor Interactive Server Checklist

Post-WASM migration:
- [ ] SDK is `Microsoft.NET.Sdk.Web` (not BlazorWebAssembly)
- [ ] `AddRazorComponents().AddInteractiveServerComponents()`
- [ ] `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- [ ] `@rendermode="InteractiveServer"` on HeadOutlet and Routes
- [ ] `blazor.web.js` reference (not blazor.webassembly.js)
- [ ] No `UseBlazorFrameworkFiles()` or `MapFallbackToFile()`
- [ ] No `wwwroot/index.html`
- [ ] `ServerAuthenticationStateProvider` reads from `HttpContext.User`

## Service Lifetime Patterns

Correct lifetimes:
| Service Type | Lifetime | Why |
|--------------|----------|-----|
| `BlobServiceClient`, `TableServiceClient` | Singleton | Thread-safe, reuse connections |
| Custom services wrapping SDK clients | Singleton | No per-request state |
| `AuthenticationStateProvider` | Scoped | Per-circuit state |
| `HttpClient` (via factory) | Transient | Factory manages pooling |
| `BackgroundService` | Hosted | Registered via `AddHostedService` |

## Async/Await Red Flags

```csharp
// ❌ Bad — blocks thread
var result = asyncMethod().Result;
asyncMethod().Wait();
Task.Run(() => asyncMethod()).Result;

// ✅ Good
var result = await asyncMethod();
```

## CancellationToken Threading

All async boundaries should thread through:
```csharp
// ✅ Thread through
public async Task DoWorkAsync(CancellationToken ct = default)
{
    await _service.CallAsync(ct);  // Pass ct
    await Task.Delay(1000, ct);    // Pass ct
}
```

## BackgroundService Pattern

```csharp
// ✅ Correct pattern
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
            await service.ProcessAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in worker loop");
        }
        
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }
}
```

## Temp File Cleanup

```csharp
// ✅ Always cleanup in finally
string? tempPath = null;
try
{
    tempPath = Path.GetTempFileName();
    // ... use file
}
finally
{
    if (tempPath != null && File.Exists(tempPath))
    {
        try { File.Delete(tempPath); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cleanup failed"); }
    }
}
```

## Null Safety Patterns

```csharp
// ✅ Null-safe chain
if (httpContext?.User?.Identity?.IsAuthenticated == true)

// ✅ Null coalescing with validation
_key = configuration["Key"] ?? throw new InvalidOperationException("Key not configured");

// ✅ Pattern matching
if (result is not null and { IsValid: true })
```

---

*Miller — Code Reviewer*
