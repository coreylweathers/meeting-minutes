using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MeetingMinutes.Web.Auth;

public class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private AuthenticationState? _cachedState;

    public CookieAuthenticationStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cachedState != null)
        {
            return _cachedState;
        }

        try
        {
            var response = await _httpClient.GetAsync("/api/auth/user");
            
            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
                
                if (userInfo != null && !string.IsNullOrEmpty(userInfo.Email))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, userInfo.Name ?? userInfo.Email),
                        new Claim(ClaimTypes.Email, userInfo.Email)
                    };
                    
                    var identity = new ClaimsIdentity(claims, "BFF");
                    var user = new ClaimsPrincipal(identity);
                    _cachedState = new AuthenticationState(user);
                    return _cachedState;
                }
            }
        }
        catch
        {
            // Return anonymous on any error
        }

        _cachedState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        return _cachedState;
    }

    public void NotifyAuthenticationStateChanged()
    {
        _cachedState = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private class UserInfo
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
