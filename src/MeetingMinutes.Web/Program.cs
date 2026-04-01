using Microsoft.AspNetCore.Components.Authorization;
using MeetingMinutes.Web;
using MeetingMinutes.Web.Auth;
using MeetingMinutes.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Razor components with Interactive Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient for API (server-to-server via Aspire service discovery)
builder.Services.AddHttpClient("api", client =>
{
    // Try Aspire service discovery first, then fallback to configuration
    var apiBaseUrl = builder.Configuration["services:api:http:0"] 
        ?? builder.Configuration["services:api:https:0"]
        ?? builder.Configuration["ApiBaseUrl"] 
        ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Register default HttpClient using the named client
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("api");
});

// HttpContextAccessor needed for ServerAuthenticationStateProvider
builder.Services.AddHttpContextAccessor();

// Auth — using cookie-based auth that reads from HttpContext
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
