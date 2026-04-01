using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MeetingMinutes.Web.Auth;

/// <summary>
/// Server-side authentication state provider that reads user info from HttpContext.
/// In Interactive Server mode, auth is already handled by cookie middleware on the server.
/// </summary>
public class ServerAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult(new AuthenticationState(httpContext.User));
        }

        // Return anonymous user
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(anonymous));
    }
}
