using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E")]
public class AuthFlowTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Fact]
    public async Task JobsPage_RedirectsOrShowsLogin_WhenUnauthenticated()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync($"{BaseUrl}/jobs");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var currentUrl = page.Url;
        
        // Either redirected away from /jobs, or still on /jobs but showing login UI
        var isRedirected = !currentUrl.Contains("/jobs");
        var hasLoginButton = await page.Locator("a[href*='login'], a[href*='signin']").CountAsync() > 0;
        
        (isRedirected || hasLoginButton).Should().BeTrue("unauthenticated users should be redirected or shown login");
    }
    
    [Fact]
    public async Task UploadPage_RedirectsOrShowsLogin_WhenUnauthenticated()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync($"{BaseUrl}/upload");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var currentUrl = page.Url;
        
        var isRedirected = !currentUrl.Contains("/upload");
        var hasLoginButton = await page.Locator("a[href*='login'], a[href*='signin']").CountAsync() > 0;
        
        (isRedirected || hasLoginButton).Should().BeTrue("unauthenticated users should be redirected or shown login");
    }
    
    [Fact]
    public async Task HomePage_ShowsLoginButtons()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Should see login buttons in the nav
        var loginButtons = page.Locator("a[href*='/api/auth/login']");
        var count = await loginButtons.CountAsync();
        count.Should().BeGreaterThan(0, "should show login options for unauthenticated users");
    }
}
